using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Endpoints;

public static class EmployeePassportEndpoints
{
    public static IEndpointRouteBuilder MapEmployeePassportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/passport")
            .WithTags("Employee Passport")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // Fetch passport bio/data page image
        group.MapGet("/{id:int}/image", GetPassportImage)
            .WithName("GetPassportImage")
            .WithSummary("استرجاع صورة صفحة البيانات الرئيسية لجواز سفر الموظف.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Fetch biometric face photo (DG2) if available
        group.MapGet("/{id:int}/photo", GetPassportBiometricPhoto)
            .WithName("GetPassportBiometricPhoto")
            .WithSummary("استرجاع الصورة البيومترية من شريحة الجواز إن وجدت.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Get active passport for current logged-in employee
        group.MapGet("/me", GetMyPassport)
            .WithName("GetMyPassport")
            .WithSummary("استرجاع بيانات الجواز الساري للموظف الحالي.");

        return endpoints;
    }

    private static async Task<IResult> GetPassportImage(
        int id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var passport = await db.EmployeePassports
            .AsNoTracking()
            .Include(p => p.Employee)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (passport is null)
        {
            return Results.NotFound(new { message = "سجل جواز السفر غير موجود." });
        }

        var isStaffUser = IsStaff(principal);
        if (!isStaffUser)
        {
            var empId = await GetCurrentEmployeeIdAsync(principal, userManager);
            if (string.IsNullOrWhiteSpace(empId) ||
                (passport.Employee is not null && !string.Equals(passport.Employee.EmployeeID, empId, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Forbid();
            }
        }

        var key = passport.PassportImageKey;
        if (string.IsNullOrWhiteSpace(key) || !await storage.ExistsAsync(key, cancellationToken))
        {
            return Results.NotFound(new { message = "صورة جواز السفر غير متوفرة." });
        }

        var stream = await storage.OpenReadAsync(key, cancellationToken);
        if (stream is null)
        {
            return Results.NotFound(new { message = "تعذر قراءة صورة جواز السفر." });
        }

        var contentType = passport.PassportImageContentType ?? "image/jpeg";
        var fileName = passport.PassportImageFileName ?? $"passport_{passport.PassportNumber}_{id}.jpg";

        return Results.File(stream, contentType, fileName, enableRangeProcessing: true);
    }

    private static async Task<IResult> GetPassportBiometricPhoto(
        int id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var passport = await db.EmployeePassports
            .AsNoTracking()
            .Include(p => p.Employee)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (passport is null)
        {
            return Results.NotFound(new { message = "سجل جواز السفر غير موجود." });
        }

        var isStaffUser = IsStaff(principal);
        if (!isStaffUser)
        {
            var empId = await GetCurrentEmployeeIdAsync(principal, userManager);
            if (string.IsNullOrWhiteSpace(empId) ||
                (passport.Employee is not null && !string.Equals(passport.Employee.EmployeeID, empId, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Forbid();
            }
        }

        var key = passport.BiometricPhotoKey;
        if (string.IsNullOrWhiteSpace(key) || !await storage.ExistsAsync(key, cancellationToken))
        {
            return Results.NotFound(new { message = "الصورة البيومترية غير متوفرة." });
        }

        var stream = await storage.OpenReadAsync(key, cancellationToken);
        if (stream is null)
        {
            return Results.NotFound(new { message = "تعذر قراءة الصورة البيومترية." });
        }

        var contentType = passport.BiometricPhotoContentType ?? "image/jpeg";
        var fileName = passport.BiometricPhotoFileName ?? $"passport_bio_{passport.PassportNumber}_{id}.jpg";

        return Results.File(stream, contentType, fileName, enableRangeProcessing: true);
    }

    private static async Task<IResult> GetMyPassport(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var empId = await GetCurrentEmployeeIdAsync(principal, userManager);
        if (string.IsNullOrWhiteSpace(empId))
        {
            return Results.Json(new { message = "هذا الحساب غير مرتبط بسجل موظف." }, statusCode: StatusCodes.Status403Forbidden);
        }

        var passport = await db.EmployeePassports.AsNoTracking()
            .Where(p => p.Employee != null && p.Employee.EmployeeID == empId && p.IsActive)
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.EmployeeId,
                p.PassportNumber,
                p.PassportType,
                p.CountryCode,
                p.IssuingCountry,
                p.IssuingAuthority,
                p.NationalIdNumber,
                p.FullNameAr,
                p.FullNameEn,
                p.MotherName,
                p.PlaceOfBirth,
                p.DateOfBirth,
                p.Gender,
                p.IssueDate,
                p.ExpiryDate,
                p.RawMrz,
                hasImage = !string.IsNullOrWhiteSpace(p.PassportImageKey),
                imageUrl = !string.IsNullOrWhiteSpace(p.PassportImageKey) ? $"/api/passport/{p.Id}/image" : null,
                hasBioPhoto = !string.IsNullOrWhiteSpace(p.BiometricPhotoKey),
                bioPhotoUrl = !string.IsNullOrWhiteSpace(p.BiometricPhotoKey) ? $"/api/passport/{p.Id}/photo" : null,
                p.IsActive,
                p.CreatedAt,
                p.Notes
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (passport is null)
        {
            return Results.NotFound(new { message = "لم يتم العثور على جواز سفر مسجل لهذا الموظف." });
        }

        return Results.Ok(passport);
    }

    private static async Task<string?> GetCurrentEmployeeIdAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var claimEmpId = principal.FindFirst("employee_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claimEmpId)) return claimEmpId;

        var user = await userManager.GetUserAsync(principal);
        return user?.EmployeeID;
    }

    private static bool IsStaff(ClaimsPrincipal principal) => StaffRoles.IsStaff(principal);
}
