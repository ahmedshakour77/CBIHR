using API.HR.Node.Data.Notifications;

namespace API.HR.Node.Services.Notifications;

public record UserNotificationDto(
    long Id,
    string UserId,
    string Title,
    string Message,
    NotificationType Type,
    string? LinkUrl,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public interface INotificationService
{
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    Task<List<UserNotificationDto>> GetNotificationsAsync(
        string userId,
        int take = 20,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(long id, string userId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);

    Task DeleteNotificationAsync(long id, string userId, CancellationToken cancellationToken = default);

    Task<UserNotificationDto> SendAsync(
        string userId,
        string title,
        string message,
        NotificationType type = NotificationType.Info,
        string? linkUrl = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default);

    event Action<string>? OnNotificationChanged;
}
