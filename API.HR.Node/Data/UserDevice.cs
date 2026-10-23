using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

public sealed class UserDevice
{
    public long Id { get; set; }

    [MaxLength(256)]
    public required string UserId { get; set; }

    [MaxLength(100)]
    public required string EmployeeID { get; set; }

    [MaxLength(256)]
    public required string DeviceId { get; set; }

    [MaxLength(100)]
    public string? Platform { get; set; }

    [MaxLength(150)]
    public string? DeviceModel { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? OsVersion { get; set; }

    [MaxLength(100)]
    public string? AppVersion { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastLoginAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(64)]
    public string? LastIpAddress { get; set; }

    [MaxLength(1000)]
    public string? MetadataJson { get; set; }
}
