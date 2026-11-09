using System.Security.Claims;
using API.HR.Node.Services.Audit;
using Microsoft.AspNetCore.Mvc;

namespace API.HR.Node.Endpoints;

public static class UserActivityEndpoints
{
    public static IEndpointRouteBuilder MapUserActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/activity")
            .WithTags("UserActivity")
            .RequireAuthorization();

        group.MapPost("/batch", RecordClientActivityBatch)
            .DisableAntiforgery();

        return endpoints;
    }

    private static IResult RecordClientActivityBatch(
        HttpContext context,
        [FromServices] IUserActivityService activityService,
        [FromBody] List<ClientActivityBatchItem>? items)
    {
        if (items is null || items.Count == 0 || context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Ok(new { recorded = 0 });
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();
        var ua = context.Request.Headers.UserAgent.ToString();
        var count = 0;

        foreach (var item in items.Take(40))
        {
            if (string.IsNullOrWhiteSpace(item.ActionTitle)) continue;

            var cat = item.Category switch
            {
                ActivityCategories.Auth => ActivityCategories.Auth,
                ActivityCategories.Navigation => ActivityCategories.Navigation,
                ActivityCategories.Dropdown => ActivityCategories.Dropdown,
                ActivityCategories.Search => ActivityCategories.Search,
                ActivityCategories.Security => ActivityCategories.Security,
                _ => ActivityCategories.Click
            };

            activityService.RecordFromPrincipal(
                principal: context.User,
                category: cat,
                actionCode: item.ActionCode ?? "UI_EVENT",
                actionTitle: item.ActionTitle,
                pagePath: item.PagePath,
                pageTitle: item.PageTitle,
                elementLabel: item.ElementLabel,
                valueText: item.ValueText,
                durationSeconds: item.DurationSeconds,
                sessionId: item.SessionId,
                ipAddress: ip,
                userAgent: ua,
                success: true);
            count++;
        }

        return Results.Ok(new { recorded = count });
    }
}

