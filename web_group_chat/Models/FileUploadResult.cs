namespace web_group_chat.Models;

public sealed record FileUploadResult(
    string FileName,
    long FileSize,
    string FileUrl);
