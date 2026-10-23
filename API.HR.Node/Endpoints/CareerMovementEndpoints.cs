using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Import;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace API.HR.Node.Endpoints;

public static class CareerMovementEndpoints
{
    private const int MaximumRowsPerRequest = 100_000;

    public static IEndpointRouteBuilder MapCareerMovementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/movements")
            .WithTags("Career Movements")
            .RequireAuthorization();

        // Mobile: Get current employee's career movements
        group.MapGet("/me", async (
            ApplicationDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var user = httpContext.User;
            var claimEmpId = user.FindFirst("employee_id")?.Value;
            if (string.IsNullOrWhiteSpace(claimEmpId))
            {
                var userManager = httpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
                var appUser = await userManager.GetUserAsync(user);
                claimEmpId = appUser?.EmployeeID;
            }

            if (string.IsNullOrWhiteSpace(claimEmpId))
            {
                return Results.Json(new { message = "هذا الحساب غير مرتبط بسجل موظف." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var emp = await db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeID == claimEmpId, ct);

            if (emp is null)
                return Results.NotFound(new { message = "سجل الموظف غير موجود." });

            var movements = await db.EmployeeCareerMovements.AsNoTracking()
                .Where(m => m.EmployeeId == emp.Id)
                .OrderBy(m => m.MovementDate)
                .Select(m => new CareerMovementDto(
                    m.Id,
                    m.EmployeeId,
                    emp.EmployeeID,
                    emp.Name,
                    m.MovementDate,
                    m.FormationName,
                    m.CountForAllowance,
                    m.CountForPromotion,
                    m.CountForPension,
                    m.Notes,
                    m.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(movements);
        })
        .WithName("GetMyCareerMovements")
        .WithSummary("استرجاع سجل التنقلات والمسار الوظيفي للموظف الحالي لتطبيق الهاتف.");

        // 1. Get movements for an employee
        group.MapGet("/employee/{employeeNumber}", async (
            string employeeNumber,
            ApplicationDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var user = httpContext.User;
            var isStaff = user.IsInRole("Admin") || user.IsInRole("HRManager") || user.IsInRole("Supervisor")
                || user.HasClaim(c => (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role") && (c.Value == "Admin" || c.Value == "HRManager"));

            var claimEmpId = user.FindFirst("employee_id")?.Value;
            var isOwner = !string.IsNullOrWhiteSpace(claimEmpId) && claimEmpId.Equals(employeeNumber, StringComparison.OrdinalIgnoreCase);

            if (!isStaff && !isOwner)
            {
                var userManager = httpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
                var appUser = await userManager.GetUserAsync(user);
                if (appUser != null && !string.IsNullOrWhiteSpace(appUser.EmployeeID) && appUser.EmployeeID.Equals(employeeNumber, StringComparison.OrdinalIgnoreCase))
                {
                    isOwner = true;
                }
            }

            if (!isStaff && !isOwner)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "ليس لديك صلاحية للوصول إلى الحركات الوظيفية لهذا الموظف.");
            }

            var emp = await db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeID == employeeNumber, ct);

            if (emp is null)
                return Results.NotFound(new { message = $"الموظف بالرقم {employeeNumber} غير موجود." });

            var movements = await db.EmployeeCareerMovements.AsNoTracking()
                .Where(m => m.EmployeeId == emp.Id)
                .OrderBy(m => m.MovementDate)
                .Select(m => new CareerMovementDto(
                    m.Id,
                    m.EmployeeId,
                    emp.EmployeeID,
                    emp.Name,
                    m.MovementDate,
                    m.FormationName,
                    m.CountForAllowance,
                    m.CountForPromotion,
                    m.CountForPension,
                    m.Notes,
                    m.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(movements);
        });

        // 2. Create a movement
        group.MapPost("/", async (
            [FromBody] CreateCareerMovementRequest req,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.FormationName))
                return Results.BadRequest(new { message = "بيان الحركة / التشكيل مطلوب." });

            if (!DateOnly.TryParse(req.MovementDate, CultureInfo.InvariantCulture, out var movDate) &&
                !DateOnly.TryParse(req.MovementDate, out movDate))
            {
                return Results.BadRequest(new { message = "تاريخ التنقل غير صالح." });
            }

            Employee? emp = null;
            if (req.EmployeeId.HasValue)
            {
                emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == req.EmployeeId.Value, ct);
            }
            else if (!string.IsNullOrWhiteSpace(req.EmployeeNumber))
            {
                emp = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeID == req.EmployeeNumber.Trim(), ct);
            }

            if (emp is null)
                return Results.BadRequest(new { message = "تعذر العثور على الموظف المحدد." });

            var isOut = req.FormationName.Contains("خارج الخدمة", StringComparison.OrdinalIgnoreCase) ||
                        req.FormationName.Contains("انقطاع", StringComparison.OrdinalIgnoreCase) ||
                        req.FormationName.Contains("كف يد", StringComparison.OrdinalIgnoreCase);

            var movement = new EmployeeCareerMovement
            {
                EmployeeId = emp.Id,
                MovementDate = movDate,
                FormationName = req.FormationName.Trim(),
                CountForAllowance = req.CountForAllowance ?? !isOut,
                CountForPromotion = req.CountForPromotion ?? !isOut,
                CountForPension = req.CountForPension ?? !isOut,
                Notes = req.Notes?.Trim()
            };

            db.EmployeeCareerMovements.Add(movement);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/movements/{movement.Id}", new CareerMovementDto(
                movement.Id,
                movement.EmployeeId,
                emp.EmployeeID,
                emp.Name,
                movement.MovementDate,
                movement.FormationName,
                movement.CountForAllowance,
                movement.CountForPromotion,
                movement.CountForPension,
                movement.Notes,
                movement.CreatedAtUtc));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        // 3. Update a movement
        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] UpdateCareerMovementRequest req,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var movement = await db.EmployeeCareerMovements
                .Include(m => m.Employee)
                .FirstOrDefaultAsync(m => m.Id == id, ct);

            if (movement is null)
                return Results.NotFound(new { message = "الحركة المطلوبة غير موجودة." });

            if (!DateOnly.TryParse(req.MovementDate, CultureInfo.InvariantCulture, out var movDate) &&
                !DateOnly.TryParse(req.MovementDate, out movDate))
            {
                return Results.BadRequest(new { message = "تاريخ التنقل غير صالح." });
            }

            movement.MovementDate = movDate;
            movement.FormationName = req.FormationName.Trim();
            movement.CountForAllowance = req.CountForAllowance;
            movement.CountForPromotion = req.CountForPromotion;
            movement.CountForPension = req.CountForPension;
            movement.Notes = req.Notes?.Trim();

            await db.SaveChangesAsync(ct);

            return Results.Ok(new CareerMovementDto(
                movement.Id,
                movement.EmployeeId,
                movement.Employee.EmployeeID,
                movement.Employee.Name,
                movement.MovementDate,
                movement.FormationName,
                movement.CountForAllowance,
                movement.CountForPromotion,
                movement.CountForPension,
                movement.Notes,
                movement.CreatedAtUtc));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        // 4. Delete a movement
        group.MapDelete("/{id:int}", async (
            int id,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var movement = await db.EmployeeCareerMovements.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (movement is null)
                return Results.NotFound();

            db.EmployeeCareerMovements.Remove(movement);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        // 5. Excel Import Endpoint
        endpoints.MapPost("/api/import/career-movements/excel", async (
            IFormFile file,
            IExcelImportService importService,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { message = "الرجاء اختيار ملف Excel صالح." });

            using var stream = file.OpenReadStream();
            var parseResult = await importService.ParseCareerMovementsExcelAsync(stream, ct);

            if (parseResult.HasErrors)
            {
                return Results.BadRequest(new
                {
                    message = "فشل تحليل أعمدة أو بيانات ملف حركات الخدمة.",
                    errors = parseResult.ParseErrors
                });
            }

            if (parseResult.Rows.Count == 0)
            {
                return Results.BadRequest(new { message = "لم يتم العثور على أي صفوف بيانات صالحة للاستيراد." });
            }

            try
            {
                var request = new CareerMovementImportRequest(
                    Mode: "upsert",
                    Movements: parseResult.Rows);

                var response = await importService.ImportCareerMovementsAsync(request, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithTags("Excel Import")
        .DisableAntiforgery()
        .WithName("ImportCareerMovementsFromExcel")
        .WithSummary("استيراد حركات الخدمة والتنقلات عبر رفع ملف Excel (.xlsx)")
        .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        // 6. Excel Template Download
        endpoints.MapGet("/api/import/template/career-movements", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateCareerMovementsTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "نموذج_استيراد_حركات_الخدمة_والتنقلات.xlsx");
        })
        .WithTags("Excel Import")
        .RequireAuthorization();

        return endpoints;
    }

    public static async Task<(bool Success, CareerMovementImportResponse? Response, IReadOnlyList<EmployeeImportError>? Errors, string? Message)> ExecuteImportAsync(
        CareerMovementImportRequest request,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        if (request.Movements == null || request.Movements.Count == 0)
            return (false, null, null, "لا توجد صفوف للمعالجة في الطلب.");

        if (request.Movements.Count > MaximumRowsPerRequest)
            return (false, null, null, $"الحد الأقصى المسموح به هو {MaximumRowsPerRequest:N0} صف.");

        var distinctEmpIds = request.Movements
            .Where(r => !string.IsNullOrWhiteSpace(r.EmployeeID))
            .Select(r => r.EmployeeID.Trim())
            .Distinct()
            .ToList();

        // Batch lookup employees
        var employeeDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        const int batchSize = 1000;
        for (var i = 0; i < distinctEmpIds.Count; i += batchSize)
        {
            var chunk = distinctEmpIds.Skip(i).Take(batchSize).ToList();
            var chunkEmployees = await db.Employees.AsNoTracking()
                .Where(e => chunk.Contains(e.EmployeeID))
                .Select(e => new { e.Id, e.EmployeeID })
                .ToListAsync(ct);

            foreach (var emp in chunkEmployees)
            {
                employeeDict[emp.EmployeeID] = emp.Id;
            }
        }

        var missingEmpIds = distinctEmpIds
            .Where(id => !employeeDict.ContainsKey(id))
            .ToList();

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<EmployeeImportError>();

        // Load existing movements for matched employees
        var matchedEmployeePks = employeeDict.Values.ToList();
        var existingMovements = new List<EmployeeCareerMovement>();
        for (var i = 0; i < matchedEmployeePks.Count; i += batchSize)
        {
            var chunk = matchedEmployeePks.Skip(i).Take(batchSize).ToList();
            var chunkMovements = await db.EmployeeCareerMovements
                .Where(m => chunk.Contains(m.EmployeeId))
                .ToListAsync(ct);
            existingMovements.AddRange(chunkMovements);
        }

        var existingMap = existingMovements
            .GroupBy(m => (m.EmployeeId, m.MovementDate))
            .ToDictionary(g => g.Key, g => g.First());

        var rowNum = 1;
        var newMovementsToAdd = new List<EmployeeCareerMovement>();

        foreach (var row in request.Movements)
        {
            rowNum++;
            var empId = row.EmployeeID?.Trim();
            if (string.IsNullOrWhiteSpace(empId) || !employeeDict.TryGetValue(empId, out var employeePk))
            {
                skipped++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.MovementDate) ||
                (!DateOnly.TryParse(row.MovementDate, CultureInfo.InvariantCulture, out var movDate) &&
                 !DateOnly.TryParse(row.MovementDate, out movDate)))
            {
                errors.Add(new EmployeeImportError(rowNum, "MovementDate", $"الصف {rowNum}: تاريخ التنقل غير صالح."));
                skipped++;
                continue;
            }

            var formName = string.IsNullOrWhiteSpace(row.FormationName) ? "غير محدد" : row.FormationName.Trim();

            // Automatic rule for 3 flags:
            var isOut = formName.Contains("خارج الخدمة", StringComparison.OrdinalIgnoreCase) ||
                        formName.Contains("انقطاع", StringComparison.OrdinalIgnoreCase) ||
                        formName.Contains("كف يد", StringComparison.OrdinalIgnoreCase);

            var allowance = row.CountForAllowance ?? !isOut;
            var promotion = row.CountForPromotion ?? !isOut;
            var pension = row.CountForPension ?? !isOut;

            var key = (EmployeeId: employeePk, MovementDate: movDate);

            if (existingMap.TryGetValue(key, out var existing))
            {
                if (request.Mode == "insert")
                {
                    skipped++;
                }
                else
                {
                    existing.FormationName = formName;
                    existing.CountForAllowance = allowance;
                    existing.CountForPromotion = promotion;
                    existing.CountForPension = pension;
                    if (!string.IsNullOrWhiteSpace(row.Notes))
                    {
                        existing.Notes = row.Notes.Trim();
                    }
                    updated++;
                }
            }
            else
            {
                var newMov = new EmployeeCareerMovement
                {
                    EmployeeId = employeePk,
                    MovementDate = movDate,
                    FormationName = formName,
                    CountForAllowance = allowance,
                    CountForPromotion = promotion,
                    CountForPension = pension,
                    Notes = row.Notes?.Trim()
                };

                newMovementsToAdd.Add(newMov);
                existingMap[key] = newMov;
                inserted++;
            }
        }

        if (newMovementsToAdd.Count > 0)
        {
            await db.EmployeeCareerMovements.AddRangeAsync(newMovementsToAdd, ct);
        }

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        var response = new CareerMovementImportResponse(
            TotalRows: request.Movements.Count,
            Inserted: inserted,
            Updated: updated,
            Skipped: skipped,
            MissingEmployeeIDs: missingEmpIds);

        return (true, response, errors, null);
    }
}
