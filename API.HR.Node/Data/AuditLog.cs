using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

public sealed class AuditLog
{
    public long Id { get; set; }

    [MaxLength(256)]
    public string? UserId { get; set; }

    [MaxLength(100)]
    public required string EmployeeID { get; set; }

    public int? PhotoId { get; set; }

    [MaxLength(100)]
    public required string Action { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public bool Success { get; set; }

    [MaxLength(1000)]
    public string? FailureReason { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }
}
