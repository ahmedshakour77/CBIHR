using System.Security.Claims;
using API.HR.Node.Security;
using API.HR.Node.Services.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace API.HR.Node.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications")
            .WithTags("Account Notifications")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        group.MapGet("/", GetUserNotifications)
            .WithName("GetUserNotifications")
            .WithSummary("Retrieves notifications for the current authenticated user.");

        group.MapGet("/unread-count", GetUnreadCount)
            .WithName("GetUnreadNotificationCount")
            .WithSummary("Retrieves the count of unread notifications for the current user.");

        group.MapPost("/{id:long}/read", MarkAsRead)
            .WithName("MarkNotificationAsRead")
            .WithSummary("Marks a specific notification as read.");

        group.MapPost("/read-all", MarkAllAsRead)
            .WithName("MarkAllNotificationsAsRead")
            .WithSummary("Marks all notifications as read for the current user.");

        group.MapDelete("/{id:long}", DeleteNotification)
            .WithName("DeleteNotification")
            .WithSummary("Deletes a notification.");

        return endpoints;
    }

    private static async Task<IResult> GetUserNotifications(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int? take,
        ClaimsPrincipal principal,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var notifications = await notificationService.GetNotificationsAsync(
            userId: userId,
            take: take ?? 20,
            unreadOnly: unreadOnly ?? false,
            cancellationToken: cancellationToken);

        return Results.Ok(notifications);
    }

    private static async Task<IResult> GetUnreadCount(
        ClaimsPrincipal principal,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var count = await notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Results.Ok(new { unreadCount = count });
    }

    private static async Task<IResult> MarkAsRead(
        long id,
        ClaimsPrincipal principal,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        await notificationService.MarkAsReadAsync(id, userId, cancellationToken);
        return Results.Ok(new { message = "Notification marked as read." });
    }

    private static async Task<IResult> MarkAllAsRead(
        ClaimsPrincipal principal,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        await notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return Results.Ok(new { message = "All notifications marked as read." });
    }

    private static async Task<IResult> DeleteNotification(
        long id,
        ClaimsPrincipal principal,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        await notificationService.DeleteNotificationAsync(id, userId, cancellationToken);
        return Results.Ok(new { message = "Notification deleted." });
    }

    private static string GetUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? principal.FindFirstValue(ClaimTypes.Name)
               ?? principal.Identity?.Name
               ?? "systemadmin";
    }
}
