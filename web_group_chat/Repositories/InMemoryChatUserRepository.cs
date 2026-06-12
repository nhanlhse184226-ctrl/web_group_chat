using System.Collections.Concurrent;

namespace web_group_chat.Repositories;

public sealed class InMemoryChatUserRepository : IChatUserRepository
{
    readonly ConcurrentDictionary<string, string> users = new();

    public string SetUsername(string connectionId, string username)
    {
        username = NormalizeUsername(username);
        users[connectionId] = username;
        return username;
    }

    public bool TryGetUsername(string connectionId, out string username)
    {
        return users.TryGetValue(connectionId, out username!);
    }

    public string GetOrCreateGuestUsername(string connectionId)
    {
        return users.GetOrAdd(connectionId, id => $"Guest-{id[..4]}");
    }

    public bool RemoveUser(string connectionId, out string username)
    {
        return users.TryRemove(connectionId, out username!);
    }

    public IReadOnlyList<string> GetOnlineUsers()
    {
        return users.Values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();
    }

    static string NormalizeUsername(string username)
    {
        username = (username ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(username))
            username = "Guest";

        return username.Length <= 24 ? username : username[..24];
    }
}
