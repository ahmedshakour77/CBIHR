namespace API.HR.Node.Options;

public sealed class ProfilePhotoOptions
{
    public const string SectionName = "ProfilePhoto";

    public string StorageRoot { get; set; } = "App_Data/Storage/ProfilePhotos";

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB

    public int ThumbnailWidth { get; set; } = 150;

    public int ThumbnailHeight { get; set; } = 150;

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public string[] AllowedExtensions { get; set; } =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    ];

    public int MinDimension { get; set; } = 50;

    public int MaxDimension { get; set; } = 4096;

    public long MaxPixelCount { get; set; } = 25_000_000; // 25 Megapixels
}
