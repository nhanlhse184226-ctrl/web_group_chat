namespace web_group_chat.Repositories;

public interface IChatUserRepository
{
    string SetUsername(string connectionId, string username);
    bool TryGetUsername(string connectionId, out string username);
    string GetOrCreateGuestUsername(string connectionId);
    bool RemoveUser(string connectionId, out string username);
    IReadOnlyList<string> GetOnlineUsers();
}
