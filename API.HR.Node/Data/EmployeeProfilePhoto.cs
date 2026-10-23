using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

public sealed class EmployeeProfilePhoto
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string EmployeeID { get; set; }

    public Employee? Employee { get; set; }

    [MaxLength(500)]
    public required string StorageKey { get; set; }

    [MaxLength(500)]
    public required string ThumbnailStorageKey { get; set; }

    [MaxLength(255)]
    public required string OriginalFileName { get; set; }

    [MaxLength(100)]
    public required string ContentType { get; set; }

    public long FileSize { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    [MaxLength(64)]
    public required string Sha256 { get; set; }

    public int Version { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [MaxLength(256)]
    public required string CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    [MaxLength(256)]
    public string? UpdatedBy { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    [MaxLength(256)]
    public string? DeletedBy { get; set; }
}
