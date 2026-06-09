using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace web_group_chat.Hubs;

public sealed class ChatHub : Hub
{
    static readonly ConcurrentDictionary<string, string> Users = new();
    static readonly HashSet<string> Stickers = new(StringComparer.OrdinalIgnoreCase)
    {
        "frog",
        "cute",
        "heart"
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

    public async Task SendMessage(string message)
    {
        string username = GetUsername();
        message = (message ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(message))
            return;

        await Clients.All.SendAsync("ReceiveMessage", new ChatMessageDto(
            Guid.NewGuid().ToString("N"),
            username,
            "text",
            message,
            string.Empty,
            0,
            string.Empty,
            DateTimeOffset.Now));
    }

    public async Task SendSticker(string sticker)
    {
        string username = GetUsername();
        sticker = (sticker ?? string.Empty).Trim();

        if (!Stickers.Contains(sticker))
            return;

        await Clients.All.SendAsync("ReceiveMessage", new ChatMessageDto(
            Guid.NewGuid().ToString("N"),
            username,
            "sticker",
            sticker,
            string.Empty,
            0,
            string.Empty,
            DateTimeOffset.Now));
    }

    public async Task SendFileMessage(string fileName, long fileSize, string fileUrl)
    {
        string username = GetUsername();
        fileName = Path.GetFileName(fileName ?? string.Empty);
        fileUrl = (fileUrl ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(fileUrl) || fileSize < 0)
            return;

        await Clients.All.SendAsync("ReceiveMessage", new ChatMessageDto(
            Guid.NewGuid().ToString("N"),
            username,
            "file",
            string.Empty,
            fileName,
            fileSize,
            fileUrl,
            DateTimeOffset.Now));
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

public sealed record ChatMessageDto(
    string Id,
    string Sender,
    string Type,
    string Content,
    string FileName,
    long FileSize,
    string FileUrl,
    DateTimeOffset SentAt);
