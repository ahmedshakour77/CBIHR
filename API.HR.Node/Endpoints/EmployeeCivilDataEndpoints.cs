using System.Security.Claims;
using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Import;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Endpoints;

public static class EmployeeCivilDataEndpoints
{
    private const int MaximumRowsPerRequest = 100_000;

    public static IEndpointRouteBuilder MapEmployeeCivilDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/employees/civil-data")
            .WithTags("EmployeeCivilData")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // Mobile: Get current employee's civil and identity data
        group.MapGet("/me", GetMyCivilData)
            .WithName("GetMyCivilData")
            .WithSummary("استرجاع بيانات البطاقة الموحدة وبيانات الأحوال المدنية وعائلة الموظف الحالي لتطبيق الهاتف.");

        // Staff/Self: Get civil data by employee number
        group.MapGet("/{employeeNumber}", GetEmployeeCivilData)
            .WithName("GetEmployeeCivilData")
            .WithSummary("استرجاع بيانات الأحوال المدنية والبطاقة الموحدة لموظف محدد.");

        // 1. Download Excel template
        group.MapGet("/template", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateCivilDataTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استكمال_بيانات_البطاقة_الموحدة.xlsx");
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("DownloadCivilDataTemplate")
        .WithSummary("تحميل نموذج إكسل استكمال بيانات البطاقة الموحدة للموظفين");

        // 2. Import Civil Data via JSON
        group.MapPost("/import", async (
            EmployeeCivilDataImportRequest req,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var (success, response, errors, message) = await ExecuteImportAsync(req, db, ct);
            if (!success)
            {
                return Results.BadRequest(new { message, errors });
            }

            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("ImportCivilDataJson")
        .WithSummary("تحديث واستكمال بيانات البطاقة الموحدة للموظفين عبر JSON");

        // 3. Import Civil Data via Excel upload
        group.MapPost("/import/excel", async (
            IFormFile file,
            IExcelImportService importService,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "يرجى اختيار ملف Excel صالح (.xlsx)." });
            }

            using var stream = file.OpenReadStream();
            var parseResult = await importService.ParseCivilDataExcelAsync(stream, ct);

            if (parseResult.HasErrors)
            {
                return Results.BadRequest(new
                {
                    message = "يحتوي الملف على أخطاء تمنع استكمال البيانات.",
                    errors = parseResult.ParseErrors
                });
            }

            try
            {
                var req = new EmployeeCivilDataImportRequest(parseResult.Rows);
                var (success, response, errors, message) = await ExecuteImportAsync(req, db, ct);

                if (!success)
                {
                    return Results.BadRequest(new { message, errors });
                }

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .DisableAntiforgery()
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("ImportCivilDataExcel")
        .WithSummary("استكمال وتحديث بيانات البطاقة الموحدة عبر رفع ملف Excel (.xlsx)");

        return endpoints;
    }

    private static async Task<IResult> GetMyCivilData(
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

        return await FetchCivilDataAsync(empId, db, cancellationToken);
    }

    private static async Task<IResult> GetEmployeeCivilData(
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
                return Results.Json(new { message = "غير مصرح لك باستعراض البيانات المدنية لهذا الموظف." }, statusCode: StatusCodes.Status403Forbidden);
            }
        }

        return await FetchCivilDataAsync(employeeNumber, db, cancellationToken);
    }

    private static async Task<IResult> FetchCivilDataAsync(
        string employeeNumber,
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var emp = await db.Employees.AsNoTracking()
            .Include(e => e.Formation)
            .Include(e => e.JobTitle)
            .Include(e => e.MaritalStatus)
            .Include(e => e.Gender)
            .Include(e => e.Religion)
            .Include(e => e.Nationality)
            .Include(e => e.Martyrs).ThenInclude(m => m.MartyrdomType)
            .Include(e => e.Martyrs).ThenInclude(m => m.Kinship)
            .FirstOrDefaultAsync(e => e.EmployeeID == employeeNumber, cancellationToken);

        if (emp is null)
        {
            return Results.NotFound(new { message = "سجل الموظف غير موجود." });
        }

        return Results.Ok(new
        {
            employeeId = emp.EmployeeID,
            name = emp.Name,
            formation = emp.Formation?.Name,
            jobTitle = emp.JobTitle?.Name,
            nationalId = emp.NationalId,
            unifiedEmployeeNumber = emp.UnifiedEmployeeNumber,
            motherName = emp.MotherName,
            firstName = emp.FirstName,
            secondName = emp.SecondName,
            thirdName = emp.ThirdName,
            fourthName = emp.FourthName,
            maritalStatus = emp.MaritalStatus?.Name,
            gender = emp.Gender?.Name,
            religion = emp.Religion?.Name,
            nationality = emp.Nationality?.Name,
            birthDate = emp.BirthDate,
            hireDate = emp.HireDate,
            leaveBalance = emp.LeaveBalance,
            martyrsCount = emp.Martyrs.Count,
            martyrs = emp.Martyrs.Select(m => new
            {
                m.Id,
                m.MartyrName,
                martyrdomType = m.MartyrdomType?.Name,
                kinship = m.Kinship?.Name,
                m.DecisionNumber,
                m.Notes
            })
        });
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

    private static bool IsStaff(ClaimsPrincipal principal)
    {
        return principal.IsInRole("Admin") ||
               principal.IsInRole("HRManager") ||
               principal.IsInRole("Supervisor") ||
               principal.IsInRole("FormationManager");
    }

    public static async Task<(bool Success, EmployeeCivilDataImportResponse? Response, IReadOnlyList<EmployeeImportError>? Errors, string? ErrorMessage)> ExecuteImportAsync(
        EmployeeCivilDataImportRequest request,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.Rows is null || request.Rows.Count == 0)
        {
            return (false, null, null, "قائمة السجلات فارغة.");
        }

        if (request.Rows.Count > MaximumRowsPerRequest)
        {
            return (false, null, null, $"لا يمكن استيراد أكثر من {MaximumRowsPerRequest} سجل في العملية الواحدة.");
        }

        var errors = new List<EmployeeImportError>();
        var cleanRows = new List<EmployeeCivilDataImportRow>();

        for (var i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var rowNum = i + 1;

            if (row is null) continue;

            if (string.IsNullOrWhiteSpace(row.EmployeeID))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: رقم الموظف مطلوب للربط."));
                continue;
            }

            cleanRows.Add(row);
        }

        if (errors.Count > 0)
        {
            return (false, null, errors, null);
        }

        var requestedEmpIds = cleanRows
            .Select(r => r.EmployeeID.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingEmployees = await dbContext.Employees
            .Where(e => requestedEmpIds.Contains(e.EmployeeID))
            .ToDictionaryAsync(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var updated = 0;
        var skipped = 0;
        var missingIds = new List<string>();

        foreach (var row in cleanRows)
        {
            var empId = row.EmployeeID.Trim();
            if (!existingEmployees.TryGetValue(empId, out var employee))
            {
                skipped++;
                if (!missingIds.Contains(empId, StringComparer.OrdinalIgnoreCase))
                {
                    missingIds.Add(empId);
                }
                continue;
            }

            var modified = false;

            if (!string.IsNullOrWhiteSpace(row.NationalId))
            {
                employee.NationalId = row.NationalId.Trim();
                modified = true;
            }

            if (!string.IsNullOrWhiteSpace(row.UnifiedEmployeeNumber))
            {
                employee.UnifiedEmployeeNumber = row.UnifiedEmployeeNumber.Trim();
                modified = true;
            }

            if (!string.IsNullOrWhiteSpace(row.MotherName))
            {
                employee.MotherName = row.MotherName.Trim();
                modified = true;
            }

            if (!string.IsNullOrWhiteSpace(row.FirstName))
            {
                employee.FirstName = row.FirstName.Trim();
                modified = true;
            }

            if (!string.IsNullOrWhiteSpace(row.SecondName))
            {
                employee.SecondName = row.SecondName.Trim();
                modified = true;
            }

            if (!string.IsNullOrWhiteSpace(row.ThirdName))
            {
                employee.ThirdName = row.ThirdName.Trim();
                modified = true;
            }

            if (!string.IsNullOrWhiteSpace(row.FourthName))
            {
                employee.FourthName = row.FourthName.Trim();
                modified = true;
            }

            if (modified)
            {
                updated++;
            }
            else
            {
                // No fields provided to update for this existing employee
                skipped++;
            }
        }

        if (updated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var response = new EmployeeCivilDataImportResponse(
            TotalRows: cleanRows.Count,
            Updated: updated,
            Skipped: skipped,
            MissingEmployeeIDs: missingIds);

        return (true, response, null, null);
    }
}
