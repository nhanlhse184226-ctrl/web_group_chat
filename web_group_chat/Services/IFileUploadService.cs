using web_group_chat.Models;

namespace web_group_chat.Services;

public interface IFileUploadService
{
    Task<FileUploadResult> UploadAsync(IFormFile? file, CancellationToken cancellationToken = default);
}
