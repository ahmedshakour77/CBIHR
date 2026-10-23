using System.Security.Claims;
using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.ProfilePhotos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.HR.Node.Endpoints;

public static class EmployeeProfilePhotoEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeProfilePhotoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/employees/{employeeID}/profile-photo")
            .WithTags("Employee Profile Photos")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        group.MapPost("", UploadProfilePhoto)
            .WithName("UploadEmployeeProfilePhoto")
            .WithSummary("Uploads or replaces an employee's profile photo.")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ProfilePhotoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/thumbnail", GetProfilePhotoThumbnail)
            .WithName("GetEmployeeProfilePhotoThumbnail")
            .WithSummary("Gets the 150x150 thumbnail of an employee's profile photo.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", GetProfilePhoto)
            .WithName("GetEmployeeProfilePhoto")
            .WithSummary("Gets the full active profile photo of an employee.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/metadata", GetProfilePhotoMetadata)
            .WithName("GetEmployeeProfilePhotoMetadata")
            .WithSummary("Gets the metadata of an employee's active profile photo.")
            .Produces<ProfilePhotoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("", DeleteProfilePhoto)
            .WithName("DeleteEmployeeProfilePhoto")
            .WithSummary("Deactivates/Soft-deletes an employee's profile photo.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> UploadProfilePhoto(
        string employeeID,
        IFormFile? file,
        IEmployeeProfilePhotoService service,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessPhotoUploadAsync(employeeID, httpContext, userManager))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not authorized to upload a profile photo for this employee.");
        }

        if (file == null || file.Length == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "File is required.",
                detail: "Multipart form field 'file' must be provided and contain image data.");
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        var result = await service.UploadOrReplacePhotoAsync(
            employeeID,
            file,
            httpContext.User,
            ipAddress,
            userAgent,
            cancellationToken);

        return result switch
        {
            PhotoServiceResult.Success<ProfilePhotoResponse> success => Results.Ok(success.Data),
            PhotoServiceResult.NotFound nf => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: nf.Message),
            PhotoServiceResult.Forbidden fb => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: fb.Message),
            PhotoServiceResult.ValidationError ve => Results.Problem(statusCode: ve.StatusCode, title: "Validation Error", detail: ve.Message),
            PhotoServiceResult.ConcurrencyConflict cc => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: cc.Message),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "An unexpected error occurred.")
        };
    }

    private static async Task<IResult> GetProfilePhoto(
        string employeeID,
        IEmployeeProfilePhotoService service,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessPhotoReadAsync(employeeID, httpContext, userManager))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not authorized to view the profile photo for this employee.");
        }

        return await ServePhotoInternal(employeeID, thumbnail: false, service, httpContext, cancellationToken);
    }

    private static async Task<IResult> GetProfilePhotoThumbnail(
        string employeeID,
        IEmployeeProfilePhotoService service,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessPhotoReadAsync(employeeID, httpContext, userManager))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not authorized to view the profile photo thumbnail for this employee.");
        }

        return await ServePhotoInternal(employeeID, thumbnail: true, service, httpContext, cancellationToken);
    }

    private static async Task<IResult> ServePhotoInternal(
        string employeeID,
        bool thumbnail,
        IEmployeeProfilePhotoService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var ifNoneMatch = httpContext.Request.Headers.IfNoneMatch.ToString();
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        var result = await service.GetPhotoAsync(
            employeeID,
            thumbnail,
            ifNoneMatch,
            httpContext.User,
            ipAddress,
            userAgent,
            cancellationToken);

        return result switch
        {
            PhotoServiceResult.NotModified => NotModified(httpContext),
            PhotoServiceResult.PhotoStreamData streamData => CreateFileStreamResult(httpContext, streamData),
            PhotoServiceResult.NotFound nf => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: nf.Message),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to retrieve photo.")
        };
    }

    private static async Task<IResult> GetProfilePhotoMetadata(
        string employeeID,
        IEmployeeProfilePhotoService service,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessPhotoReadAsync(employeeID, httpContext, userManager))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not authorized to view the profile photo metadata for this employee.");
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        var result = await service.GetMetadataAsync(employeeID, httpContext.User, ipAddress, userAgent, cancellationToken);

        return result switch
        {
            PhotoServiceResult.Success<ProfilePhotoResponse> success => Results.Ok(success.Data),
            PhotoServiceResult.NotFound nf => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: nf.Message),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to retrieve metadata.")
        };
    }

    private static async Task<IResult> DeleteProfilePhoto(
        string employeeID,
        IEmployeeProfilePhotoService service,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessPhotoDeleteAsync(employeeID, httpContext, userManager))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not authorized to delete the profile photo for this employee.");
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        var result = await service.DeletePhotoAsync(employeeID, httpContext.User, ipAddress, userAgent, cancellationToken);

        return result switch
        {
            PhotoServiceResult.Success<bool> => Results.NoContent(),
            PhotoServiceResult.NotFound nf => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: nf.Message),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to delete photo.")
        };
    }

    internal static async Task<bool> CanAccessPhotoReadAsync(
        string targetEmployeeID,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager)
    {
        var user = httpContext.User;
        if (user.Identity is not { IsAuthenticated: true }) return false;

        // 1. Admin Role
        if (user.IsInRole("Admin") || user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
            return true;

        // 2. Global Read Permission
        if (user.HasClaim(c => (c.Type == "permission" || c.Type == "Permission") && c.Value.Equals(Permissions.ProfilePhotoRead, StringComparison.OrdinalIgnoreCase)))
            return true;

        // 3. Staff roles whose pages (directory, gallery, leaves) show the roster's
        //    photos. Those pages load faces from this endpoint, so it has to admit
        //    the same roles the pages do.
        if (PhotoReaderRoles.Any(user.IsInRole))
            return true;

        // 4. Ownership Check: Accessing own photo
        var claimEmpId = user.FindFirst("employee_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claimEmpId) && claimEmpId.Equals(targetEmployeeID, StringComparison.OrdinalIgnoreCase))
            return true;

        var appUser = await userManager.GetUserAsync(user);
        if (appUser != null && !string.IsNullOrWhiteSpace(appUser.EmployeeID) && appUser.EmployeeID.Equals(targetEmployeeID, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static async Task<bool> CanAccessPhotoUploadAsync(
        string targetEmployeeID,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager)
    {
        var user = httpContext.User;
        if (user.Identity is not { IsAuthenticated: true }) return false;

        if (user.IsInRole("Admin") || user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (user.HasClaim(c => (c.Type == "permission" || c.Type == "Permission") && c.Value.Equals(Permissions.ProfilePhotoUpload, StringComparison.OrdinalIgnoreCase)))
            return true;

        var claimEmpId = user.FindFirst("employee_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claimEmpId) && claimEmpId.Equals(targetEmployeeID, StringComparison.OrdinalIgnoreCase))
            return true;

        var appUser = await userManager.GetUserAsync(user);
        if (appUser != null && !string.IsNullOrWhiteSpace(appUser.EmployeeID) && appUser.EmployeeID.Equals(targetEmployeeID, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static async Task<bool> CanAccessPhotoDeleteAsync(
        string targetEmployeeID,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager)
    {
        var user = httpContext.User;
        if (user.Identity is not { IsAuthenticated: true }) return false;

        if (user.IsInRole("Admin") || user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (user.HasClaim(c => (c.Type == "permission" || c.Type == "Permission") && c.Value.Equals(Permissions.ProfilePhotoDelete, StringComparison.OrdinalIgnoreCase)))
            return true;

        var claimEmpId = user.FindFirst("employee_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claimEmpId) && claimEmpId.Equals(targetEmployeeID, StringComparison.OrdinalIgnoreCase))
            return true;

        var appUser = await userManager.GetUserAsync(user);
        if (appUser != null && !string.IsNullOrWhiteSpace(appUser.EmployeeID) && appUser.EmployeeID.Equals(targetEmployeeID, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private const string PhotoCacheControl = "private, no-cache";

    private static readonly string[] PhotoReaderRoles = ["HRManager", "FormationManager", "Supervisor"];

    // A 304 repeats the caching policy the 200 carried (RFC 9110 §15.4.5);
    // the browser keeps the ETag it already stored.
    internal static IResult NotModified(HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = PhotoCacheControl;
        return Results.StatusCode(StatusCodes.Status304NotModified);
    }

    internal static IResult CreateFileStreamResult(HttpContext httpContext, PhotoServiceResult.PhotoStreamData streamData)
    {
        httpContext.Response.Headers.ETag = streamData.ETag;
        // Revalidate on every use. Versions restart at 1 after a delete, so a
        // fresh-for-an-hour copy could show a replaced face; a 304 costs one
        // small round trip and no bytes.
        httpContext.Response.Headers.CacheControl = PhotoCacheControl;
        httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        httpContext.Response.Headers.ContentDisposition = "inline; filename=\"profile.jpg\"";

        return Results.File(
            streamData.Stream,
            contentType: streamData.ContentType,
            enableRangeProcessing: true);
    }
}
