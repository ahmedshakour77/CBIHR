namespace API.HR.Node.Services.Images;

public sealed record ImageValidationResult(
    bool IsValid,
    string? ErrorMessage,
    int Width = 0,
    int Height = 0,
    string DetectedContentType = "");

public sealed record ProcessedImageResult(
    byte[] OptimizedOriginalBytes,
    byte[] ThumbnailBytes,
    int Width,
    int Height,
    string ContentType,
    string Sha256Hash);

public interface IImageProcessor
{
    Task<ImageValidationResult> ValidateImageAsync(
        Stream stream,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default);

    Task<ProcessedImageResult> ProcessAndGenerateThumbnailAsync(
        Stream stream,
        string contentType,
        CancellationToken cancellationToken = default);
}
