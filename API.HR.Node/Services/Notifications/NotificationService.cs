using API.HR.Node.Data;
using API.HR.Node.Data.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace API.HR.Node.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ApplicationDbContext? _directDbContext;
    private readonly ILogger<NotificationService> _logger;

    public event Action<string>? OnNotificationChanged;

    // Primary constructor for ASP.NET Core DI: uses scope factory to ensure an isolated DbContext per operation,
    // eliminating concurrency conflicts with active page components in Blazor Server.
    [ActivatorUtilitiesConstructor]
    public NotificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // Internal constructor for unit testing and direct DbContext injection
    internal NotificationService(
        ApplicationDbContext dbContext,
        ILogger<NotificationService> logger)
    {
        _directDbContext = dbContext;
        _logger = logger;
    }

    private async Task<T> ExecuteAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await action(db);
        }

        return await action(_directDbContext!);
    }

    private async Task ExecuteAsync(Func<ApplicationDbContext, Task> action)
    {
        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await action(db);
            return;
        }

        await action(_directDbContext!);
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return 0;

        return await ExecuteAsync(db => db.UserNotifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken));
    }

    public async Task<List<UserNotificationDto>> GetNotificationsAsync(
        string userId,
        int take = 20,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return [];

        return await ExecuteAsync(async db =>
        {
            var query = db.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == userId);

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(take > 0 && take <= 100 ? take : 20)
                .Select(n => new UserNotificationDto(
                    n.Id,
                    n.UserId,
                    n.Title,
                    n.Message,
                    n.Type,
                    n.LinkUrl,
                    n.IsRead,
                    n.CreatedAt,
                    n.ReadAt))
                .ToListAsync(cancellationToken);
        });
    }

    public async Task MarkAsReadAsync(long id, string userId, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async db =>
        {
            var notification = await db.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                NotifyChanged(userId);
            }
        });
    }

    public async Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        await ExecuteAsync(async db =>
        {
            var unreadNotifications = await db.UserNotifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync(cancellationToken);

            if (unreadNotifications.Count > 0)
            {
                var now = DateTimeOffset.UtcNow;
                foreach (var n in unreadNotifications)
                {
                    n.IsRead = true;
                    n.ReadAt = now;
                }

                await db.SaveChangesAsync(cancellationToken);
                NotifyChanged(userId);
            }
        });
    }

    public async Task DeleteNotificationAsync(long id, string userId, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async db =>
        {
            var notification = await db.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

            if (notification != null)
            {
                db.UserNotifications.Remove(notification);
                await db.SaveChangesAsync(cancellationToken);

                NotifyChanged(userId);
            }
        });
    }

    public async Task<UserNotificationDto> SendAsync(
        string userId,
        string title,
        string message,
        NotificationType type = NotificationType.Info,
        string? linkUrl = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
        }

        return await ExecuteAsync(async db =>
        {
            var notification = new UserNotification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                LinkUrl = linkUrl,
                IsRead = false,
                CreatedAt = DateTimeOffset.UtcNow,
                MetadataJson = metadataJson
            };

            db.UserNotifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Sent notification #{Id} to user {UserId}: {Title}", notification.Id, userId, title);

            NotifyChanged(userId);

            return new UserNotificationDto(
                notification.Id,
                notification.UserId,
                notification.Title,
                notification.Message,
                notification.Type,
                notification.LinkUrl,
                notification.IsRead,
                notification.CreatedAt,
                notification.ReadAt);
        });
    }

    private void NotifyChanged(string userId)
    {
        try
        {
            OnNotificationChanged?.Invoke(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error firing OnNotificationChanged event for user {UserId}", userId);
        }
    }
}
