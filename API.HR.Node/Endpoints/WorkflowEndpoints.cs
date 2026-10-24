using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Data.Workflows;
using API.HR.Node.Security;
using API.HR.Node.Services.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Endpoints;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api")
            .WithTags("General Workflow Engine")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // 1. Start Workflow Instance
        group.MapPost("/workflows/{workflowCode}/start", StartWorkflow)
            .WithName("StartWorkflow")
            .WithSummary("Starts a new workflow instance for a given entity.");

        // 2. Execute Action on Workflow Instance
        group.MapPost("/workflow-instances/{id:long}/actions/{actionCode}", ExecuteWorkflowAction)
            .WithName("ExecuteWorkflowAction")
            .WithSummary("Executes a state transition action on an active workflow instance.");

        // 3. Get Available Actions for Instance
        group.MapGet("/workflow-instances/{id:long}/available-actions", GetAvailableActions)
            .WithName("GetWorkflowAvailableActions")
            .WithSummary("Retrieves actions the authenticated user is currently permitted to execute.");

        // 4. Get Workflow Instance Details
        group.MapGet("/workflow-instances/{id:long}", GetWorkflowInstance)
            .WithName("GetWorkflowInstance")
            .WithSummary("Retrieves detailed state and tasks of a workflow instance.");

        // 5. Get Workflow Timeline History
        group.MapGet("/workflow-instances/{id:long}/timeline", GetWorkflowTimeline)
            .WithName("GetWorkflowTimeline")
            .WithSummary("Retrieves append-only timeline event history for a workflow instance.");

        // 6. Get My Tasks
        group.MapGet("/workflow-tasks/my", GetMyTasks)
            .WithName("GetMyWorkflowTasks")
            .WithSummary("Retrieves pending workflow tasks assigned to the current user.");

        // 7. Get All Tasks (Admin / Supervisor)
        group.MapGet("/workflow-tasks", GetAllTasks)
            .WithName("GetAllWorkflowTasks")
            .WithSummary("Lists workflow tasks with filtering by status and assignment.")
            .RequireAuthorization(Permissions.WorkflowsView);

        // 8. Get Task Details
        group.MapGet("/workflow-tasks/{id:long}", GetTaskById)
            .WithName("GetWorkflowTaskById")
            .WithSummary("Retrieves details of a specific workflow task.");

        // 9. Reassign Task
        group.MapPost("/workflow-tasks/{id:long}/reassign", ReassignTask)
            .WithName("ReassignWorkflowTask")
            .WithSummary("Reassigns a workflow task to another user.")
            .RequireAuthorization(Permissions.WorkflowsEdit);

        // 10. Delegate Task
        group.MapPost("/workflow-tasks/{id:long}/delegate", DelegateTask)
            .WithName("DelegateWorkflowTask")
            .WithSummary("Delegates a workflow task.")
            .RequireAuthorization(Permissions.WorkflowsEdit);

        // 11. Workflow Definitions Management
        group.MapGet("/workflow-definitions", GetDefinitions)
            .WithName("GetWorkflowDefinitions")
            .WithSummary("Lists all workflow definitions and their published versions.")
            .RequireAuthorization(Permissions.WorkflowsView);

        group.MapPost("/workflow-definitions", CreateDefinition)
            .WithName("CreateWorkflowDefinition")
            .WithSummary("Creates a new database-driven workflow definition.")
            .RequireAuthorization(Permissions.WorkflowsEdit);

        group.MapPost("/workflow-definitions/{code}/versions", CreateVersion)
            .WithName("CreateWorkflowVersion")
            .WithSummary("Creates a new draft version for a workflow definition.")
            .RequireAuthorization(Permissions.WorkflowsEdit);

        group.MapPost("/workflow-versions/{versionId:long}/publish", PublishVersion)
            .WithName("PublishWorkflowVersion")
            .WithSummary("Publishes a draft workflow version, making it active for new instances.")
            .RequireAuthorization(Permissions.WorkflowsEdit);

        return endpoints;
    }

    public record StartWorkflowRequest(string EntityType, string EntityId, Dictionary<string, object>? InitialContext);
    public record ExecuteActionRequest(string? Comment, string? RowVersionBase64);
    public record ReassignTaskRequest(string TargetUserId, string? Reason);
    public record DelegateTaskRequest(string DelegateeUserId, int DurationDays, string? Reason);
    public record CreateDefinitionRequest(string Code, string Name, string? Description);

    private static async Task<IResult> StartWorkflow(
        string workflowCode,
        [FromBody] StartWorkflowRequest request,
        ClaimsPrincipal principal,
        IWorkflowEngine engine,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var command = new StartWorkflowCommand(
            WorkflowCode: workflowCode,
            EntityType: request.EntityType,
            EntityId: request.EntityId,
            InitiatorUserId: userId,
            InitialContext: request.InitialContext);

        try
        {
            var result = await engine.StartAsync(command, cancellationToken);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ExecuteWorkflowAction(
        long id,
        string actionCode,
        [FromBody] ExecuteActionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IWorkflowEngine engine,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        byte[]? rowVersion = null;
        if (!string.IsNullOrWhiteSpace(request.RowVersionBase64))
        {
            try { rowVersion = Convert.FromBase64String(request.RowVersionBase64); } catch { }
        }

        var command = new ExecuteWorkflowActionCommand(
            WorkflowInstanceId: id,
            ActionCode: actionCode,
            UserId: userId,
            Comment: request.Comment,
            RowVersion: rowVersion,
            IdempotencyKey: idempotencyKey,
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
            User: principal);

        try
        {
            var result = await engine.ExecuteActionAsync(command, cancellationToken);
            return Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("WORKFLOW_CONCURRENCY_CONFLICT"))
        {
            return Results.Conflict(new { code = "WORKFLOW_CONCURRENCY_CONFLICT", error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetAvailableActions(
        long id,
        ClaimsPrincipal principal,
        IWorkflowEngine engine,
        IWorkflowAuthorizationService authService,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.WorkflowInstances
            .Include(i => i.WorkflowVersion)
            .ThenInclude(v => v.WorkflowDefinition)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (instance == null)
        {
            return Results.NotFound(new { error = $"Instance #{id} not found." });
        }

        if (!await authService.CanViewWorkflowAsync(principal, instance, cancellationToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not authorized to access actions for this workflow instance.");
        }

        var userId = GetUserId(principal);
        var actions = await engine.GetAvailableActionsAsync(id, userId, principal, cancellationToken);
        return Results.Ok(new { workflowInstanceId = id, actions });
    }

    private static async Task<IResult> GetWorkflowInstance(
        long id,
        ClaimsPrincipal principal,
        IWorkflowEngine engine,
        IWorkflowAuthorizationService authService,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var instance = await dbContext.WorkflowInstances
                .Include(i => i.WorkflowVersion)
                .ThenInclude(v => v.WorkflowDefinition)
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

            if (instance == null)
            {
                return Results.NotFound(new { error = $"Instance #{id} not found." });
            }

            if (!await authService.CanViewWorkflowAsync(principal, instance, cancellationToken))
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not authorized to view this workflow instance.");
            }

            var dto = await engine.GetInstanceAsync(id, cancellationToken);
            return Results.Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetWorkflowTimeline(
        long id,
        ClaimsPrincipal principal,
        IWorkflowTimelineService timelineService,
        IWorkflowAuthorizationService authService,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.WorkflowInstances
            .Include(i => i.WorkflowVersion)
            .ThenInclude(v => v.WorkflowDefinition)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (instance == null)
        {
            return Results.NotFound(new { error = $"Instance #{id} not found." });
        }

        if (!await authService.CanViewWorkflowAsync(principal, instance, cancellationToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not authorized to view this workflow timeline.");
        }

        var items = await timelineService.GetTimelineAsync(id, cancellationToken);
        return Results.Ok(new { workflowInstanceId = id, items });
    }

    private static async Task<IResult> GetMyTasks(
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);

        var tasks = await dbContext.WorkflowTasks
            .Include(t => t.WorkflowStep)
            .Include(t => t.WorkflowInstance)
            .ThenInclude(i => i.WorkflowVersion)
            .ThenInclude(v => v.WorkflowDefinition)
            .Where(t => t.AssignedUserId == userId && t.Status == WorkflowTaskStatus.Pending)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new WorkflowTaskDto(
                t.Id,
                t.WorkflowInstanceId,
                t.WorkflowStep.Code,
                t.WorkflowStep.Name,
                t.Status,
                t.AssignedUserId,
                t.AssignedRoleId,
                t.AssignedDepartmentId,
                t.CreatedAt,
                t.DueAt,
                t.CompletedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(tasks);
    }

    private static async Task<IResult> GetAllTasks(
        [FromQuery] string? status,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.WorkflowTasks
            .Include(t => t.WorkflowStep)
            .Include(t => t.WorkflowInstance)
            .AsQueryable();

        if (Enum.TryParse<WorkflowTaskStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(t => t.Status == parsedStatus);
        }

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .Select(t => new WorkflowTaskDto(
                t.Id,
                t.WorkflowInstanceId,
                t.WorkflowStep.Code,
                t.WorkflowStep.Name,
                t.Status,
                t.AssignedUserId,
                t.AssignedRoleId,
                t.AssignedDepartmentId,
                t.CreatedAt,
                t.DueAt,
                t.CompletedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(tasks);
    }

    private static async Task<IResult> GetTaskById(
        long id,
        ClaimsPrincipal principal,
        IWorkflowAuthorizationService authService,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.WorkflowTasks
            .Include(t => t.WorkflowStep)
            .Include(t => t.WorkflowInstance)
            .ThenInclude(i => i.WorkflowVersion)
            .ThenInclude(v => v.WorkflowDefinition)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (task == null) return Results.NotFound(new { error = "Workflow task not found." });

        if (!await authService.CanViewTaskAsync(principal, task, cancellationToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not authorized to view this task.");
        }

        return Results.Ok(new WorkflowTaskDto(
            task.Id,
            task.WorkflowInstanceId,
            task.WorkflowStep.Code,
            task.WorkflowStep.Name,
            task.Status,
            task.AssignedUserId,
            task.AssignedRoleId,
            task.AssignedDepartmentId,
            task.CreatedAt,
            task.DueAt,
            task.CompletedAt));
    }

    private static async Task<IResult> ReassignTask(
        long id,
        [FromBody] ReassignTaskRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        IWorkflowTimelineService timelineService,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.WorkflowTasks
            .Include(t => t.WorkflowInstance)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (task == null) return Results.NotFound(new { error = "Workflow task not found." });

        var previousUserId = task.AssignedUserId;
        task.ReassignedFromUserId = previousUserId;
        task.AssignedUserId = request.TargetUserId;
        task.AssignedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var currentUserId = GetUserId(principal);
        await timelineService.AppendEventAsync(
            workflowInstanceId: task.WorkflowInstanceId,
            workflowVersionId: task.WorkflowInstance.WorkflowVersionId,
            stepId: task.WorkflowStepId,
            eventType: "REASSIGNED",
            actionCode: "REASSIGN",
            fromStepId: null,
            toStepId: task.WorkflowStepId,
            userId: currentUserId,
            displayName: currentUserId,
            comment: $"Reassigned task #{task.Id} from {previousUserId} to {request.TargetUserId}. Reason: {request.Reason}",
            cancellationToken: cancellationToken);

        return Results.Ok(new { message = $"Task #{id} successfully reassigned to user '{request.TargetUserId}'." });
    }

    private static async Task<IResult> DelegateTask(
        long id,
        [FromBody] DelegateTaskRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        IWorkflowTimelineService timelineService,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetUserId(principal);

        var delegation = new WorkflowDelegation
        {
            DelegatorUserId = currentUserId,
            DelegateeUserId = request.DelegateeUserId,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddDays(request.DurationDays > 0 ? request.DurationDays : 7),
            IsActive = true,
            Reason = request.Reason,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.WorkflowDelegations.Add(delegation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = $"Successfully delegated approvals to '{request.DelegateeUserId}' for {request.DurationDays} days." });
    }

    private static async Task<IResult> GetDefinitions(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var definitions = await dbContext.WorkflowDefinitions
            .Include(d => d.Versions)
            .OrderBy(d => d.Code)
            .Select(d => new
            {
                d.Id,
                d.Code,
                d.Name,
                d.Description,
                d.IsEnabled,
                Versions = d.Versions.Select(v => new
                {
                    v.Id,
                    v.VersionNumber,
                    v.Status,
                    v.PublishedAt
                })
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(definitions);
    }

    private static async Task<IResult> CreateDefinition(
        [FromBody] CreateDefinitionRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await dbContext.WorkflowDefinitions.AnyAsync(d => d.Code == request.Code, cancellationToken))
        {
            return Results.BadRequest(new { error = $"Workflow definition code '{request.Code}' already exists." });
        }

        var userId = GetUserId(principal);
        var def = new WorkflowDefinition
        {
            Code = request.Code.ToUpperInvariant(),
            Name = request.Name,
            Description = request.Description,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };

        dbContext.WorkflowDefinitions.Add(def);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/workflow-definitions", def);
    }

    private static async Task<IResult> CreateVersion(
        string code,
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var def = await dbContext.WorkflowDefinitions
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Code == code, cancellationToken);

        if (def == null) return Results.NotFound(new { error = $"Workflow definition '{code}' not found." });

        var nextVersionNumber = def.Versions.Count == 0 ? 1 : def.Versions.Max(v => v.VersionNumber) + 1;
        var userId = GetUserId(principal);

        var version = new WorkflowVersion
        {
            WorkflowDefinitionId = def.Id,
            VersionNumber = nextVersionNumber,
            Status = WorkflowVersionStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };

        dbContext.WorkflowVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(version);
    }

    private static async Task<IResult> PublishVersion(
        long versionId,
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.WorkflowVersions
            .Include(v => v.WorkflowDefinition)
            .ThenInclude(d => d.Versions)
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);

        if (version == null) return Results.NotFound(new { error = "Workflow version not found." });

        // Retire previous published versions for this definition
        foreach (var v in version.WorkflowDefinition.Versions.Where(v => v.Status == WorkflowVersionStatus.Published))
        {
            v.Status = WorkflowVersionStatus.Retired;
        }

        var userId = GetUserId(principal);
        version.Status = WorkflowVersionStatus.Published;
        version.PublishedAt = DateTimeOffset.UtcNow;
        version.PublishedBy = userId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = $"Version #{version.VersionNumber} of workflow '{version.WorkflowDefinition.Code}' published successfully." });
    }

    private static string GetUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? principal.FindFirstValue(ClaimTypes.Name)
               ?? principal.Identity?.Name
               ?? "systemadmin";
    }
}
