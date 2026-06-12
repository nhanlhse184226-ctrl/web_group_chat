namespace web_group_chat.Repositories;

public interface IFileStorageRepository
{
    Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);
}
