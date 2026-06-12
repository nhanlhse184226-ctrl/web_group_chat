using web_group_chat.Models;

namespace web_group_chat.Services;

public interface IChatService
{
    string SetUsername(string connectionId, string username, out bool changed);
    string GetUsername(string connectionId);
    IReadOnlyList<string> GetOnlineUsers();
    bool RemoveUser(string connectionId, out string username);
    ChatMessageDto? CreateTextMessage(string connectionId, string message);
    ChatMessageDto? CreateStickerMessage(string connectionId, string sticker);
    ChatMessageDto? CreateFileMessage(string connectionId, string fileName, long fileSize, string fileUrl);
}
