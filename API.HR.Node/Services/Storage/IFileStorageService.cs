namespace API.HR.Node.Services.Storage;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string storageKey, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
