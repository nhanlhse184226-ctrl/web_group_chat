using Microsoft.AspNetCore.SignalR;
using web_group_chat.Services;

namespace web_group_chat.Hubs;

public sealed class ChatHub : Hub
{
    readonly IChatService chatService;

    public ChatHub(IChatService chatService)
    {
        this.chatService = chatService;
    }

    public async Task SetUsername(string username)
    {
        string normalizedUsername = chatService.SetUsername(Context.ConnectionId, username, out bool changed);

        if (changed)
        {
            await Clients.Caller.SendAsync("ReceiveSystemMessage", $"You joined as {normalizedUsername}.");
            await Clients.Others.SendAsync("ReceiveSystemMessage", $"{normalizedUsername} joined the chat.");
        }

        await BroadcastUsersAsync();
    }

    public async Task SendMessage(string message)
    {
        var chatMessage = chatService.CreateTextMessage(Context.ConnectionId, message);

        if (chatMessage != null)
            await Clients.All.SendAsync("ReceiveMessage", chatMessage);
    }

    public async Task SendSticker(string sticker)
    {
        var chatMessage = chatService.CreateStickerMessage(Context.ConnectionId, sticker);

        if (chatMessage != null)
            await Clients.All.SendAsync("ReceiveMessage", chatMessage);
    }

    public async Task SendFileMessage(string fileName, long fileSize, string fileUrl)
    {
        var chatMessage = chatService.CreateFileMessage(Context.ConnectionId, fileName, fileSize, fileUrl);

        if (chatMessage != null)
            await Clients.All.SendAsync("ReceiveMessage", chatMessage);
    }

    public Task SendTyping(bool isTyping)
    {
        string username = chatService.GetUsername(Context.ConnectionId);
        return Clients.Others.SendAsync("ReceiveTyping", username, isTyping);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (chatService.RemoveUser(Context.ConnectionId, out var username))
        {
            await Clients.Others.SendAsync("ReceiveTyping", username, false);
            await Clients.Others.SendAsync("ReceiveSystemMessage", $"{username} left the chat.");
            await BroadcastUsersAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    Task BroadcastUsersAsync()
    {
        return Clients.All.SendAsync("ReceiveUserList", chatService.GetOnlineUsers());
    }
}
