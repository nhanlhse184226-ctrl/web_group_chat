using web_group_chat.Models;
using web_group_chat.Repositories;

namespace web_group_chat.Services;

public sealed class ChatService : IChatService
{
    static readonly HashSet<string> Stickers = new(StringComparer.OrdinalIgnoreCase)
    {
        "frog",
        "cute",
        "heart"
    };

    readonly IChatUserRepository userRepository;

    public ChatService(IChatUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    public string SetUsername(string connectionId, string username, out bool changed)
    {
        string? previousUsername = null;

        if (userRepository.TryGetUsername(connectionId, out var currentUsername))
            previousUsername = currentUsername;

        string normalizedUsername = userRepository.SetUsername(connectionId, username);
        changed = !string.Equals(previousUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase);
        return normalizedUsername;
    }

    public string GetUsername(string connectionId)
    {
        return userRepository.GetOrCreateGuestUsername(connectionId);
    }

    public IReadOnlyList<string> GetOnlineUsers()
    {
        return userRepository.GetOnlineUsers();
    }

    public bool RemoveUser(string connectionId, out string username)
    {
        return userRepository.RemoveUser(connectionId, out username);
    }

    public ChatMessageDto? CreateTextMessage(string connectionId, string message)
    {
        message = (message ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(message))
            return null;

        return CreateMessage(connectionId, ChatMessageType.Text, message);
    }

    public ChatMessageDto? CreateStickerMessage(string connectionId, string sticker)
    {
        sticker = (sticker ?? string.Empty).Trim();

        if (!Stickers.Contains(sticker))
            return null;

        return CreateMessage(connectionId, ChatMessageType.Sticker, sticker);
    }

    public ChatMessageDto? CreateFileMessage(string connectionId, string fileName, long fileSize, string fileUrl)
    {
        fileName = Path.GetFileName(fileName ?? string.Empty);
        fileUrl = (fileUrl ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(fileUrl) || fileSize < 0)
            return null;

        return CreateMessage(connectionId, ChatMessageType.File, string.Empty, fileName, fileSize, fileUrl);
    }

    ChatMessageDto CreateMessage(
        string connectionId,
        string type,
        string content,
        string fileName = "",
        long fileSize = 0,
        string fileUrl = "")
    {
        return new ChatMessageDto(
            Guid.NewGuid().ToString("N"),
            GetUsername(connectionId),
            type,
            content,
            fileName,
            fileSize,
            fileUrl,
            DateTimeOffset.Now);
    }
}
