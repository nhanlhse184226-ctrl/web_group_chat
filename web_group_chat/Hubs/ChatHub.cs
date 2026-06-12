using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace web_group_chat.Hubs;

public sealed class ChatHub : Hub
{
    // Strict cap for inline base64 payloads (images and files). Keep this well
    // below Program.cs's MaximumReceiveMessageSize: base64 is ~33% larger than
    // the raw bytes and the SignalR envelope adds more on top.
    const long MaxBytes = 5 * 1024 * 1024;
    const int MaxTextLength = 4000;
    const int MaxFileNameLength = 80;

    static readonly ConcurrentDictionary<string, string> Users = new();

    static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    static readonly HashSet<string> AllowedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
        "txt", "zip", "rar", "jpg", "jpeg", "png", "gif", "webp"
    };

    static readonly HashSet<string> AllowedStickerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg", "png", "webp", "gif", "jpg", "jpeg"
    };

    public async Task SetUsername(string username)
    {
        username = NormalizeUsername(username);
        string? previousUsername = null;

        if (Users.TryGetValue(Context.ConnectionId, out var currentUsername))
        {
            previousUsername = currentUsername;
        }

        Users[Context.ConnectionId] = username;

        if (!string.Equals(previousUsername, username, StringComparison.OrdinalIgnoreCase))
        {
            await Clients.Caller.SendAsync("ReceiveSystemMessage", $"You joined as {username}.");
            await Clients.Others.SendAsync("ReceiveSystemMessage", $"{username} joined the chat.");
        }

        await BroadcastUsersAsync();
    }

    // Single generic entry point for every message type. The server always sets
    // the authoritative User, Id and Timestamp so clients cannot spoof them, and
    // validates the payload per type before relaying it to everyone.
    public async Task SendChatMessage(ChatMessageDto message)
    {
        if (message is null)
            return;

        string user = GetUsername();
        string type = (message.Type ?? string.Empty).Trim().ToLowerInvariant();

        ChatMessageDto? outbound = type switch
        {
            "text" => BuildTextMessage(user, message),
            "image" => BuildImageMessage(user, message),
            "file" => BuildFileMessage(user, message),
            "sticker" => BuildStickerMessage(user, message),
            _ => null
        };

        if (outbound is null)
            return;

        await Clients.All.SendAsync("ReceiveMessage", outbound);
    }

    public Task SendTyping(bool isTyping)
    {
        string username = GetUsername();
        return Clients.Others.SendAsync("ReceiveTyping", username, isTyping);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Users.TryRemove(Context.ConnectionId, out var username))
        {
            await Clients.Others.SendAsync("ReceiveTyping", username, false);
            await Clients.Others.SendAsync("ReceiveSystemMessage", $"{username} left the chat.");
            await BroadcastUsersAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    static ChatMessageDto? BuildTextMessage(string user, ChatMessageDto message)
    {
        string text = (message.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (text.Length > MaxTextLength)
            text = text[..MaxTextLength];

        return Compose(user, "text", text);
    }

    static ChatMessageDto? BuildImageMessage(string user, ChatMessageDto message)
    {
        string mime = (message.MimeType ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedImageTypes.Contains(mime))
            return null;

        string data = (message.Content ?? string.Empty).Trim();
        if (!TryValidateDataUrl(data, mime, out long byteLength))
            return null;

        string fileName = SanitizeFileName(message.FileName, "image");
        return Compose(user, "image", data, fileName, mime, byteLength);
    }

    static ChatMessageDto? BuildFileMessage(string user, ChatMessageDto message)
    {
        string fileName = SanitizeFileName(message.FileName, "file");
        string extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (!AllowedFileExtensions.Contains(extension))
            return null;

        string data = (message.Content ?? string.Empty).Trim();
        if (!TryValidateDataUrl(data, expectedMime: null, out long byteLength))
            return null;

        string mime = (message.MimeType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mime))
            mime = "application/octet-stream";

        return Compose(user, "file", data, fileName, mime, byteLength);
    }

    static ChatMessageDto? BuildStickerMessage(string user, ChatMessageDto message)
    {
        string url = (message.Content ?? string.Empty).Trim();
        if (!IsAllowedStickerUrl(url))
            return null;

        string fileName = SanitizeFileName(message.FileName, "sticker");
        return Compose(user, "sticker", url, fileName);
    }

    static ChatMessageDto Compose(
        string user,
        string type,
        string content,
        string? fileName = null,
        string? mimeType = null,
        long? fileSize = null) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Type = type,
        User = user,
        Content = content,
        FileName = fileName,
        MimeType = mimeType,
        FileSize = fileSize,
        Timestamp = DateTimeOffset.Now
    };

    // Validates a "data:<mime>;base64,<payload>" URL and reports the approximate
    // decoded byte length. When expectedMime is supplied the declared mime must
    // match it exactly.
    static bool TryValidateDataUrl(string data, string? expectedMime, out long byteLength)
    {
        byteLength = 0;

        if (string.IsNullOrWhiteSpace(data) || !data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;

        int comma = data.IndexOf(',');
        if (comma < 0)
            return false;

        string header = data[5..comma];
        if (!header.Contains(";base64", StringComparison.OrdinalIgnoreCase))
            return false;

        if (expectedMime is not null)
        {
            string declaredMime = header.Split(';')[0].Trim();
            if (!string.Equals(declaredMime, expectedMime, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        string base64 = data[(comma + 1)..];
        if (string.IsNullOrWhiteSpace(base64))
            return false;

        long approxBytes = (long)base64.Length * 3 / 4;
        if (approxBytes <= 0 || approxBytes > MaxBytes)
            return false;

        byteLength = approxBytes;
        return true;
    }

    // Stickers must reference a local asset under /stickers/ with a safe name so
    // a client cannot inject an arbitrary external or javascript: URL.
    static bool IsAllowedStickerUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/stickers/", StringComparison.Ordinal))
            return false;

        string fileName = url["/stickers/".Length..];
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains(".."))
            return false;

        foreach (char c in fileName)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                return false;
        }

        string extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return AllowedStickerExtensions.Contains(extension);
    }

    static string SanitizeFileName(string? name, string fallback)
    {
        string clean = Path.GetFileName(name ?? string.Empty).Trim();
        if (clean.Length > MaxFileNameLength)
            clean = clean[..MaxFileNameLength];

        return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
    }

    static string NormalizeUsername(string username)
    {
        username = (username ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(username))
            username = "Guest";

        return username.Length <= 24 ? username : username[..24];
    }

    string GetUsername()
    {
        if (Users.TryGetValue(Context.ConnectionId, out var username))
            return username;

        username = $"Guest-{Context.ConnectionId[..4]}";
        Users[Context.ConnectionId] = username;
        return username;
    }

    Task BroadcastUsersAsync()
    {
        var usernames = Users.Values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();

        return Clients.All.SendAsync("ReceiveUserList", usernames);
    }
}

// Unified message model for every chat message type.
//   text    -> Content holds the message text
//   image   -> Content holds an inline base64 data URL
//   file    -> Content holds an inline base64 data URL (rendered as a file card)
//   sticker -> Content holds a local sticker URL under /stickers/
public sealed class ChatMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string User { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
