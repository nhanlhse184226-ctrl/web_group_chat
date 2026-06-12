namespace web_group_chat.Repositories;

public sealed class LocalFileStorageRepository : IFileStorageRepository
{
    readonly IWebHostEnvironment environment;

    public LocalFileStorageRepository(IWebHostEnvironment environment)
    {
        this.environment = environment;
    }

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        string uploadsRoot = Path.Combine(environment.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsRoot);

        string extension = Path.GetExtension(Path.GetFileName(file.FileName));
        string storedName = $"{Guid.NewGuid():N}{extension}";
        string storedPath = Path.Combine(uploadsRoot, storedName);

        await using var fileStream = File.Create(storedPath);
        await file.CopyToAsync(fileStream, cancellationToken);

        return storedName;
    }
}
