using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Feedback;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Endpoints;

public record MobileFeedbackRequest(string Title, string Description, string? FeedbackType = "فكرة");

public static class EmployeeFeedbackEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeFeedbackEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/feedback")
            .WithTags("Employee Feedback & Inquiries")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // Mobile: Submit feedback, inquiry, or suggestion
        group.MapPost("/", SubmitFeedback)
            .WithName("SubmitEmployeeFeedback")
            .WithSummary("تقديم فكرة، مقترح، استفسار أو شكوى مباشرة من تطبيق الهاتف.");

        // Mobile: Get feedback submitted by the current employee
        group.MapGet("/me", GetMyFeedbacks)
            .WithName("GetMyFeedbacks")
            .WithSummary("استرجاع سجل المقترحات والاستفسارات المقدمة من الموظف الحالي مع حالة وتفاصيل الرد.");

        // Download attachment
        group.MapGet("/attachment/{id:int}", DownloadAttachment)
            .WithName("DownloadFeedbackAttachment")
            .WithSummary("Downloads the supporting attachment associated with a feedback or inquiry.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> SubmitFeedback(
        [FromBody] MobileFeedbackRequest req,
        IEmployeeFeedbackService feedbackService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Description))
        {
            return Results.BadRequest(new { message = "عنوان وتفاصيل المقترح أو الاستفسار مطلوبة." });
        }

        var dto = new FeedbackSubmissionDto
        {
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            FeedbackType = !string.IsNullOrWhiteSpace(req.FeedbackType) ? req.FeedbackType.Trim() : "فكرة"
        };

        var result = await feedbackService.SubmitFeedbackAsync(dto, httpContext.User, cancellationToken);
        if (!result.Success)
        {
            return Results.BadRequest(new { message = result.ErrorMessage ?? "تعذر تقديم المقترح." });
        }

        return Results.Created($"/api/feedback/{result.Feedback?.Id}", new
        {
            message = "تم تقديم المقترح/الاستفسار بنجاح.",
            referenceNumber = result.ReferenceNumber,
            id = result.Feedback?.Id,
            status = result.Feedback?.Status.ToString()
        });
    }

    private static async Task<IResult> GetMyFeedbacks(
        IEmployeeFeedbackService feedbackService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        int? empDbId = null;
        if (!string.IsNullOrWhiteSpace(user.EmployeeID))
        {
            empDbId = await db.Employees
                .Where(e => e.EmployeeID == user.EmployeeID)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var items = await feedbackService.GetFeedbacksForEmployeeAsync(empDbId, user.Id, cancellationToken);

        var response = items.Select(f => new
        {
            f.Id,
            f.ReferenceNumber,
            f.FeedbackType,
            f.Title,
            f.Description,
            status = f.Status.ToString(),
            f.AdminNotes,
            hasAttachment = !string.IsNullOrWhiteSpace(f.AttachmentStorageKey),
            attachmentUrl = !string.IsNullOrWhiteSpace(f.AttachmentStorageKey) ? $"/api/feedback/attachment/{f.Id}" : null,
            f.CreatedAt,
            resolvedAt = f.RespondedAt
        });

        return Results.Ok(response);
    }

    private static async Task<IResult> DownloadAttachment(
        int id,
        IEmployeeFeedbackService feedbackService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await feedbackService.GetAttachmentStreamAsync(id, httpContext.User, cancellationToken);
        if (result == null || result.Value.Stream == null)
        {
            return Results.NotFound(new { message = "الملف غير موجود أو ليس لديك صلاحية للوصول إليه." });
        }

        return Results.File(
            result.Value.Stream,
            result.Value.ContentType,
            result.Value.FileName,
            enableRangeProcessing: true);
    }
}
