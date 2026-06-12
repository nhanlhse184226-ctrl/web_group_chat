using web_group_chat.Models;
using web_group_chat.Repositories;

namespace web_group_chat.Services;

public sealed class FileUploadService : IFileUploadService
{
    const long MaxUploadBytes = 25 * 1024 * 1024;

    readonly IFileStorageRepository fileStorageRepository;
    readonly ILogger<FileUploadService> logger;

    public FileUploadService(IFileStorageRepository fileStorageRepository, ILogger<FileUploadService> logger)
    {
        this.fileStorageRepository = fileStorageRepository;
        this.logger = logger;
    }

    public async Task<FileUploadResult> UploadAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Choose a file first.");

        if (file.Length > MaxUploadBytes)
            throw new InvalidOperationException("File must be 25 MB or smaller.");

        string originalName = Path.GetFileName(file.FileName);
        string storedName = await fileStorageRepository.SaveAsync(file, cancellationToken);

        logger.LogInformation("Uploaded chat file {FileName} ({FileSize} bytes).", originalName, file.Length);

        return new FileUploadResult(originalName, file.Length, $"/uploads/{storedName}");
    }
}
