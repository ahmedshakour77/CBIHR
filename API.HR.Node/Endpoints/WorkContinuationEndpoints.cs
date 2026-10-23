using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.HR.Node.Endpoints;

public static class WorkContinuationEndpoints
{
    public record WorkContinuationRequestDto(string Purpose, string? DirectToEntity);

    public static IEndpointRouteBuilder MapWorkContinuationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/requests")
            .WithTags("Employment Continuation Requests")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        group.MapPost("/work-continuation", SubmitWorkContinuationRequest)
            .WithName("SubmitWorkContinuationRequest")
            .WithSummary("تقديم طلب تأييد استمرار بالعمل من الهاتف أو الويب وبدء المسار الإداري.");

        return endpoints;
    }

    private static async Task<IResult> SubmitWorkContinuationRequest(
        [FromBody] WorkContinuationRequestDto request,
        ClaimsPrincipal principal,
        IWorkflowEngine workflowEngine,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? principal.FindFirstValue(ClaimTypes.Name)
               ?? principal.Identity?.Name
               ?? "systemadmin";

        if (string.IsNullOrWhiteSpace(request.Purpose))
        {
            return Results.BadRequest(new { error = "يرجى تحديد الغرض أو الجهة المعنية بكتاب التأييد." });
        }

        var requestId = $"REQ-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

        var initialContext = new Dictionary<string, object>
        {
            { "Purpose", request.Purpose },
            { "DirectToEntity", request.DirectToEntity ?? "جهة عامة" },
            { "SubmittedAt", DateTime.UtcNow.ToString("O") }
        };

        var command = new StartWorkflowCommand(
            WorkflowCode: "WORK_CONTINUATION_REQUEST",
            EntityType: "WorkContinuationRequest",
            EntityId: requestId,
            InitiatorUserId: userId,
            InitialContext: initialContext);

        try
        {
            var result = await workflowEngine.StartAsync(command, cancellationToken);

            return Results.Created($"/api/workflow-instances/{result.WorkflowInstanceId}", new
            {
                message = "تم تقديم طلب تأييد الاستمرار بالعمل بنجاح وتم إرساله إلى مدير التشكيل للموافقات.",
                requestId = requestId,
                workflowInstanceId = result.WorkflowInstanceId,
                currentStep = result.CurrentStepName,
                status = result.Status
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"فشل إرسال طلب التأييد: {ex.Message}" });
        }
    }
}
