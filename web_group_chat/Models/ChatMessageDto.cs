namespace web_group_chat.Models;

public sealed record ChatMessageDto(
    string Id,
    string Sender,
    string Type,
    string Content,
    string FileName,
    long FileSize,
    string FileUrl,
    DateTimeOffset SentAt);
