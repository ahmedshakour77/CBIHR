using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Endpoints;

public record UpdateResidenceLocationDto(double? Latitude, double? Longitude, string? LocationUrl);

public static class EmployeeResidenceEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeResidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/residence")
            .WithTags("Employee Residence")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // Mobile: Get current employee's active and historical residence records
        group.MapGet("/me", GetMyResidence)
            .WithName("GetMyResidence")
            .WithSummary("استرجاع بيانات وبطاقة السكن والموقع الجغرافي للموظف الحالي لتطبيق الهاتف.");

        // Mobile: Update current employee's live GPS coordinates from mobile phone
        group.MapPut("/me/location", UpdateMyLocation)
            .WithName("UpdateMyResidenceLocation")
            .WithSummary("تحديث الإحداثيات الجغرافية (GPS) ورابط الموقع الحيوي للسكن مباشرة من هاتف الموظف.");

        // Mobile/GIS: Get all employee map pins for the interactive mobile map
        group.MapGet("/pins", GetMapPins)
            .WithName("GetResidenceMapPins")
            .WithSummary("استرجاع إحداثيات ومواقع سكن الموظفين المعتمدة مع صورهم الشخصية لعرضها على الخريطة.")
            // Every employee's home address and GPS position: staff only.
            .RequireAuthorization(AuthorizationPolicies.Staff);

        // Staff/Self: Get residences for a specific employee
        group.MapGet("/employee/{employeeNumber}", GetEmployeeResidences)
            .WithName("GetEmployeeResidences")
            .WithSummary("استرجاع سجلات سكن موظف محدد.");

        // Fetch residence card images (front / back)
        group.MapGet("/{id:int}/card/{side}", GetResidenceCardImage)
            .WithName("GetResidenceCardImage")
            .WithSummary("Fetches the front or back image/document of an employee residence card.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetMyResidence(
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

        var residences = await db.EmployeeResidences.AsNoTracking()
            .Include(r => r.Governorate)
            .Where(r => r.EmployeeIdNumber == empId || r.Employee.EmployeeID == empId)
            .OrderByDescending(r => r.FromDate ?? DateOnly.MinValue)
            .ThenByDescending(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.EmployeeId,
                r.EmployeeIdNumber,
                governorateId = r.GovernorateId,
                governorate = r.Governorate != null ? r.Governorate.Name : null,
                r.InformationOffice,
                r.FamilyHeadName,
                r.HousingType,
                r.District,
                r.Mahalla,
                r.Zuqaq,
                r.Dar,
                r.ComplexName,
                r.BuildingNumber,
                r.FloorNumber,
                r.ApartmentNumber,
                r.FormNumber,
                r.FormDate,
                r.LocationUrl,
                r.Latitude,
                r.Longitude,
                r.FromDate,
                r.ToDate,
                isCurrent = r.ToDate == null || r.ToDate >= DateOnly.FromDateTime(DateTime.UtcNow),
                hasCardFront = !string.IsNullOrWhiteSpace(r.CardFrontKey),
                cardFrontUrl = !string.IsNullOrWhiteSpace(r.CardFrontKey) ? $"/api/residence/{r.Id}/card/front" : null,
                hasCardBack = !string.IsNullOrWhiteSpace(r.CardBackKey),
                cardBackUrl = !string.IsNullOrWhiteSpace(r.CardBackKey) ? $"/api/residence/{r.Id}/card/back" : null
            })
            .ToListAsync(cancellationToken);

        var current = residences.FirstOrDefault(r => r.isCurrent) ?? residences.FirstOrDefault();

        return Results.Ok(new
        {
            current,
            history = residences
        });
    }

    private static async Task<IResult> UpdateMyLocation(
        UpdateResidenceLocationDto req,
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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var residence = await db.EmployeeResidences
            .Where(r => (r.EmployeeIdNumber == empId || r.Employee.EmployeeID == empId) && (r.ToDate == null || r.ToDate >= today))
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (residence is null)
        {
            // Fallback to most recent residence
            residence = await db.EmployeeResidences
                .Where(r => r.EmployeeIdNumber == empId || r.Employee.EmployeeID == empId)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (residence is null)
        {
            return Results.NotFound(new { message = "لا يوجد سجل سكن مسجل لهذا الموظف لتحديث إحداثياته." });
        }

        if (req.Latitude.HasValue && req.Longitude.HasValue)
        {
            residence.Latitude = req.Latitude.Value;
            residence.Longitude = req.Longitude.Value;
        }

        if (!string.IsNullOrWhiteSpace(req.LocationUrl))
        {
            residence.LocationUrl = req.LocationUrl.Trim();
        }
        else if (req.Latitude.HasValue && req.Longitude.HasValue)
        {
            residence.LocationUrl = $"https://www.google.com/maps?q={req.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{req.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        residence.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            message = "تم تحديث إحداثيات السكن بنجاح.",
            residenceId = residence.Id,
            latitude = residence.Latitude,
            longitude = residence.Longitude,
            locationUrl = residence.LocationUrl
        });
    }

    private static async Task<IResult> GetMapPins(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var pins = await db.EmployeeResidences.AsNoTracking()
            .Include(r => r.Employee).ThenInclude(e => e.Formation)
            .Include(r => r.Employee).ThenInclude(e => e.JobTitle)
            .Include(r => r.Governorate)
            .Where(r => (r.Latitude != null && r.Longitude != null) || !string.IsNullOrWhiteSpace(r.LocationUrl))
            .Where(r => r.ToDate == null || r.ToDate >= today)
            .Select(r => new
            {
                r.Id,
                employeeId = r.Employee.Id,
                employeeNumber = r.Employee.EmployeeID,
                employeeName = r.Employee.Name,
                formation = r.Employee.Formation != null ? r.Employee.Formation.Name : null,
                jobTitle = r.Employee.JobTitle != null ? r.Employee.JobTitle.Name : null,
                governorate = r.Governorate != null ? r.Governorate.Name : null,
                r.HousingType,
                r.District,
                r.Mahalla,
                r.Zuqaq,
                r.Dar,
                r.ComplexName,
                r.BuildingNumber,
                r.FloorNumber,
                r.ApartmentNumber,
                r.Latitude,
                r.Longitude,
                r.LocationUrl,
                photoUrl = $"/api/employees/{r.Employee.EmployeeID}/profile-photo/thumbnail",
                hasPhoto = db.EmployeeProfilePhotos.Any(p => p.EmployeeID == r.Employee.EmployeeID && p.IsActive)
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(pins);
    }

    private static async Task<IResult> GetEmployeeResidences(
        string employeeNumber,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        if (!IsStaff(principal))
        {
            var currentEmpId = await GetCurrentEmployeeIdAsync(principal, userManager);
            if (!string.Equals(employeeNumber, currentEmpId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new { message = "غير مصرح لك بالوصول إلى سجلات سكن هذا الموظف." }, statusCode: StatusCodes.Status403Forbidden);
            }
        }

        var residences = await db.EmployeeResidences.AsNoTracking()
            .Include(r => r.Governorate)
            .Where(r => r.EmployeeIdNumber == employeeNumber || r.Employee.EmployeeID == employeeNumber)
            .OrderByDescending(r => r.FromDate ?? DateOnly.MinValue)
            .ThenByDescending(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.EmployeeId,
                r.EmployeeIdNumber,
                governorateId = r.GovernorateId,
                governorate = r.Governorate != null ? r.Governorate.Name : null,
                r.InformationOffice,
                r.FamilyHeadName,
                r.HousingType,
                r.District,
                r.Mahalla,
                r.Zuqaq,
                r.Dar,
                r.ComplexName,
                r.BuildingNumber,
                r.FloorNumber,
                r.ApartmentNumber,
                r.FormNumber,
                r.FormDate,
                r.LocationUrl,
                r.Latitude,
                r.Longitude,
                r.FromDate,
                r.ToDate,
                isCurrent = r.ToDate == null || r.ToDate >= DateOnly.FromDateTime(DateTime.UtcNow),
                hasCardFront = !string.IsNullOrWhiteSpace(r.CardFrontKey),
                cardFrontUrl = !string.IsNullOrWhiteSpace(r.CardFrontKey) ? $"/api/residence/{r.Id}/card/front" : null,
                hasCardBack = !string.IsNullOrWhiteSpace(r.CardBackKey),
                cardBackUrl = !string.IsNullOrWhiteSpace(r.CardBackKey) ? $"/api/residence/{r.Id}/card/back" : null
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(residences);
    }

    private static async Task<IResult> GetResidenceCardImage(
        int id,
        string side,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var residence = await db.EmployeeResidences.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.CardFrontKey, r.CardBackKey,
                r.CardFrontFileName, r.CardBackFileName,
                r.CardFrontContentType, r.CardBackContentType,
                r.EmployeeIdNumber,
                OwnerEmployeeId = r.Employee.EmployeeID
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (residence is null)
        {
            return Results.NotFound(new { message = "سجل السكن غير موجود." });
        }

        // Ids are sequential: without this check every scanned ID card is one counter away.
        if (!IsStaff(principal))
        {
            var currentEmpId = await GetCurrentEmployeeIdAsync(principal, userManager);
            var isOwner = !string.IsNullOrWhiteSpace(currentEmpId)
                && (string.Equals(currentEmpId, residence.OwnerEmployeeId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(currentEmpId, residence.EmployeeIdNumber, StringComparison.OrdinalIgnoreCase));
            if (!isOwner)
            {
                // 404, not 403: do not confirm that someone else's record exists.
                return Results.NotFound(new { message = "سجل السكن غير موجود." });
            }
        }

        var isFront = string.Equals(side, "front", StringComparison.OrdinalIgnoreCase);
        var isBack = string.Equals(side, "back", StringComparison.OrdinalIgnoreCase);

        if (!isFront && !isBack)
        {
            return Results.BadRequest(new { message = "الجهة المطلوبة غير صالحة. اختر 'front' أو 'back'." });
        }

        var key = isFront ? residence.CardFrontKey : residence.CardBackKey;
        var fileName = isFront ? residence.CardFrontFileName : residence.CardBackFileName;
        var contentType = isFront ? residence.CardFrontContentType : residence.CardBackContentType;

        if (string.IsNullOrWhiteSpace(key) || !await storage.ExistsAsync(key, cancellationToken))
        {
            return Results.NotFound(new { message = "مستند بطاقة السكن المطلوب غير موجود." });
        }

        var stream = await storage.OpenReadAsync(key, cancellationToken);
        if (stream is null)
        {
            return Results.NotFound(new { message = "تعذر قراءة ملف بطاقة السكن." });
        }

        contentType ??= "image/jpeg";
        fileName ??= $"residence_card_{side}_{id}.jpg";

        return Results.File(stream, contentType, fileName, enableRangeProcessing: true);
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
