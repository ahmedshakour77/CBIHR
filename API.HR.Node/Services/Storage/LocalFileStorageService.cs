using API.HR.Node.Options;
using Microsoft.Extensions.Options;

namespace API.HR.Node.Services.Storage;

public sealed class LocalFileStorageService(
    IOptions<ProfilePhotoOptions> options,
    IWebHostEnvironment environment,
    ILogger<LocalFileStorageService> logger)
    : IFileStorageService
{
    private readonly string _storageRootPath = Path.IsPathRooted(options.Value.StorageRoot)
        ? options.Value.StorageRoot
        : Path.Combine(environment.ContentRootPath, options.Value.StorageRoot);

    public async Task<string> SaveAsync(Stream stream, string storageKey, CancellationToken cancellationToken = default)
    {
        var (safeRelativePath, fullPathNormalized) = Resolve(storageKey);

        var directory = Path.GetDirectoryName(fullPathNormalized);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var destinationStream = new FileStream(
            fullPathNormalized,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 8192,
            useAsync: true);

        stream.Position = 0;
        await stream.CopyToAsync(destinationStream, cancellationToken);

        logger.LogInformation("Saved physical file to private storage at: {StorageKey}", safeRelativePath);
        return safeRelativePath;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var (_, fullPathNormalized) = Resolve(storageKey);

        if (!File.Exists(fullPathNormalized))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream fileStream = new FileStream(
            fullPathNormalized,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 8192,
            useAsync: true);

        return Task.FromResult<Stream?>(fileStream);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var (_, fullPathNormalized) = Resolve(storageKey);

        return Task.FromResult(File.Exists(fullPathNormalized));
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var (safeRelativePath, fullPathNormalized) = Resolve(storageKey);

        if (File.Exists(fullPathNormalized))
        {
            File.Delete(fullPathNormalized);
            logger.LogInformation("Deleted physical file from private storage at: {StorageKey}", safeRelativePath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Maps a storage key to a full path and refuses anything that resolves
    /// outside the storage root. Every operation goes through here, including
    /// delete, so a key built from ".." can neither read nor remove other files.
    /// </summary>
    private (string SafeRelativePath, string FullPath) Resolve(string storageKey)
    {
        var safeRelativePath = NormalizeStorageKey(storageKey);
        var fullPathNormalized = Path.GetFullPath(Path.Combine(_storageRootPath, safeRelativePath));

        // Trailing separator: "ProfilePhotos2" must not pass as inside "ProfilePhotos".
        var rootNormalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_storageRootPath)) + Path.DirectorySeparatorChar;
        if (!fullPathNormalized.StartsWith(rootNormalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage key path.");
        }

        return (safeRelativePath, fullPathNormalized);
    }

    private static string NormalizeStorageKey(string storageKey)
    {
        return storageKey.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }
}
