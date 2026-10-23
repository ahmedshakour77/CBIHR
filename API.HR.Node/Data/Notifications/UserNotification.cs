using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data.Notifications;

public enum NotificationType
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3,
    Workflow = 4,
    Security = 5
}

[Table("UserNotifications")]
public sealed class UserNotification
{
    public long Id { get; set; }

    [MaxLength(450)]
    public required string UserId { get; set; }

    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(1000)]
    public required string Message { get; set; }

    public NotificationType Type { get; set; } = NotificationType.Info;

    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReadAt { get; set; }

    public string? MetadataJson { get; set; }
}
