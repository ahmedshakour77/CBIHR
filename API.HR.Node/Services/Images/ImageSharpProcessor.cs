using System.Security.Cryptography;
using API.HR.Node.Options;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace API.HR.Node.Services.Images;

public sealed class ImageSharpProcessor(
    IOptions<ProfilePhotoOptions> options,
    ILogger<ImageSharpProcessor> logger)
    : IImageProcessor
{
    private readonly ProfilePhotoOptions _options = options.Value;

    public async Task<ImageValidationResult> ValidateImageAsync(
        Stream stream,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        // 1. Check file size
        if (fileSize > _options.MaxFileSizeBytes)
        {
            return new ImageValidationResult(
                false,
                $"File size exceeds maximum allowed limit of {_options.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        if (fileSize <= 0)
        {
            return new ImageValidationResult(false, "File is empty.");
        }

        // 2. Check extension
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension))
        {
            return new ImageValidationResult(
                false,
                $"File extension '{extension}' is not supported. Allowed extensions: {string.Join(", ", _options.AllowedExtensions)}.");
        }

        // 3. Magic Bytes / File Signature verification
        stream.Position = 0;
        var header = new byte[12];
        var bytesRead = await stream.ReadAsync(header, 0, header.Length, cancellationToken);
        stream.Position = 0;

        if (bytesRead < 12)
        {
            return new ImageValidationResult(false, "File is too small or corrupt.");
        }

        var detectedType = DetectContentTypeFromMagicBytes(header);
        if (string.IsNullOrEmpty(detectedType) || !_options.AllowedContentTypes.Contains(detectedType))
        {
            return new ImageValidationResult(
                false,
                "File header/signature (magic bytes) does not match a valid image format.");
        }

        // 4. Image Decoding & Dimension / Decompression Bomb protection
        try
        {
            var imageInfo = await Image.IdentifyAsync(stream, cancellationToken);
            stream.Position = 0;

            if (imageInfo == null)
            {
                return new ImageValidationResult(false, "Failed to decode image metadata.");
            }

            // Image Bomb Protection: verify pixel count
            long pixelCount = (long)imageInfo.Width * imageInfo.Height;
            if (pixelCount > _options.MaxPixelCount)
            {
                logger.LogWarning("Potential image bomb blocked. Dimensions: {Width}x{Height}, Pixels: {PixelCount}",
                    imageInfo.Width, imageInfo.Height, pixelCount);
                return new ImageValidationResult(
                    false,
                    "Image dimensions exceed maximum allowable pixel threshold.");
            }

            if (imageInfo.Width < _options.MinDimension || imageInfo.Height < _options.MinDimension)
            {
                return new ImageValidationResult(
                    false,
                    $"Image dimensions ({imageInfo.Width}x{imageInfo.Height}) are smaller than minimum allowed threshold of {_options.MinDimension}x{_options.MinDimension}.");
            }

            if (imageInfo.Width > _options.MaxDimension || imageInfo.Height > _options.MaxDimension)
            {
                return new ImageValidationResult(
                    false,
                    $"Image dimensions ({imageInfo.Width}x{imageInfo.Height}) exceed maximum allowed dimension of {_options.MaxDimension}x{_options.MaxDimension}.");
            }

            return new ImageValidationResult(
                true,
                null,
                imageInfo.Width,
                imageInfo.Height,
                detectedType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image decoding failed for file: {FileName}", fileName);
            return new ImageValidationResult(false, "The provided file is corrupted or not a valid image.");
        }
    }

    public async Task<ProcessedImageResult> ProcessAndGenerateThumbnailAsync(
        Stream stream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        stream.Position = 0;
        using var image = await Image.LoadAsync(stream, cancellationToken);

        int originalWidth = image.Width;
        int originalHeight = image.Height;

        // Strip EXIF metadata / sensitive camera location tags
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        IImageEncoder encoder = GetEncoder(contentType);

        // Normalize / Save optimized original
        using var originalMemoryStream = new MemoryStream();
        await image.SaveAsync(originalMemoryStream, encoder, cancellationToken);
        byte[] optimizedOriginalBytes = originalMemoryStream.ToArray();

        // Calculate SHA-256 hash of normalized image
        string sha256Hash = Convert.ToHexString(SHA256.HashData(optimizedOriginalBytes)).ToLowerInvariant();

        // Generate 150x150 Thumbnail
        using var thumbnailImage = image.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(_options.ThumbnailWidth, _options.ThumbnailHeight),
            Mode = ResizeMode.Crop
        }));

        thumbnailImage.Metadata.ExifProfile = null;
        thumbnailImage.Metadata.IptcProfile = null;
        thumbnailImage.Metadata.XmpProfile = null;

        using var thumbnailMemoryStream = new MemoryStream();
        await thumbnailImage.SaveAsync(thumbnailMemoryStream, encoder, cancellationToken);
        byte[] thumbnailBytes = thumbnailMemoryStream.ToArray();

        return new ProcessedImageResult(
            optimizedOriginalBytes,
            thumbnailBytes,
            originalWidth,
            originalHeight,
            contentType,
            sha256Hash);
    }

    private static string? DetectContentTypeFromMagicBytes(byte[] header)
    {
        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            return "image/png";
        }

        // WebP: RIFF ... WEBP
        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }

    private static IImageEncoder GetEncoder(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => new PngEncoder(),
            "image/webp" => new WebpEncoder { Quality = 85 },
            _ => new JpegEncoder { Quality = 85 }
        };
    }
}
