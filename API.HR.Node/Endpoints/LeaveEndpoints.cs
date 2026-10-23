using System.Security.Claims;
using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Import;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace API.HR.Node.Endpoints;

public record MobileCreateLeaveDto(int? LeaveTypeId, string StartDate, string EndDate, int? DaysCount, string? Notes);

public static class LeaveEndpoints
{
    private const int MaximumRowsPerRequest = 100_000;

    public static IEndpointRouteBuilder MapLeaveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/leaves")
            .WithTags("Leaves")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // Mobile: Get my leaves
        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var empId = await GetCurrentEmployeeIdAsync(principal, userManager);
            if (string.IsNullOrWhiteSpace(empId))
            {
                return Results.Json(new { message = "هذا الحساب غير مرتبط بسجل موظف في النظام." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var leaves = await db.EmployeeLeaves
                .AsNoTracking()
                .Include(l => l.Employee).ThenInclude(e => e.Formation)
                .Include(l => l.LeaveType)
                .Where(l => l.Employee.EmployeeID == empId)
                .OrderByDescending(l => l.StartDate)
                .Select(l => new LeaveDto(
                    l.Id,
                    l.EmployeeId,
                    l.Employee.EmployeeID,
                    l.Employee.Name,
                    l.Employee.Formation != null ? l.Employee.Formation.Name : null,
                    l.LeaveTypeId,
                    l.LeaveType != null ? l.LeaveType.Name : null,
                    l.StartDate,
                    l.EndDate,
                    l.DaysCount,
                    l.OrderNumber,
                    l.OrderDate,
                    l.Notes,
                    l.Status,
                    l.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(leaves);
        })
        .WithName("GetMyLeaves")
        .WithSummary("استرجاع قائمة إجازات الموظف المسجل حالياً لتطبيق الهاتف");

        // Mobile: Get my leave balance & KPI statistics
        group.MapGet("/me/balance", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var empId = await GetCurrentEmployeeIdAsync(principal, userManager);
            if (string.IsNullOrWhiteSpace(empId))
            {
                return Results.Json(new { message = "هذا الحساب غير مرتبط بسجل موظف في النظام." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var employee = await db.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeID == empId, ct);

            if (employee is null)
            {
                return Results.NotFound(new { message = "سجل الموظف غير موجود." });
            }

            var currentYear = DateTime.UtcNow.Year;
            var yearStart = new DateOnly(currentYear, 1, 1);
            var yearEnd = new DateOnly(currentYear, 12, 31);

            var leavesThisYear = await db.EmployeeLeaves
                .AsNoTracking()
                .Where(l => l.EmployeeId == employee.Id && l.StartDate >= yearStart && l.StartDate <= yearEnd)
                .ToListAsync(ct);

            var totalLeavesCount = await db.EmployeeLeaves
                .CountAsync(l => l.EmployeeId == employee.Id, ct);

            var usedDaysThisYear = leavesThisYear.Sum(l => l.DaysCount);

            return Results.Ok(new
            {
                employeeId = employee.EmployeeID,
                employeeName = employee.Name,
                leaveBalance = employee.LeaveBalance ?? 0m,
                usedDaysThisYear,
                leavesCountThisYear = leavesThisYear.Count,
                totalLeavesCount
            });
        })
        .WithName("GetMyLeaveBalance")
        .WithSummary("استرجاع رصيد الإجازات المتبقي وإحصائيات الإجازات للموظف الحالي");

        // Mobile: Submit leave request from phone
        group.MapPost("/me", async (
            MobileCreateLeaveDto req,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var empId = await GetCurrentEmployeeIdAsync(principal, userManager);
            if (string.IsNullOrWhiteSpace(empId))
            {
                return Results.Json(new { message = "هذا الحساب غير مرتبط بسجل موظف في النظام." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var employee = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeID == empId, ct);
            if (employee is null) return Results.NotFound(new { message = "سجل الموظف غير موجود." });

            if (!TryParseDate(req.StartDate, out var start) || start is null)
                return Results.BadRequest(new { message = "تاريخ بدء الإجازة غير صالح." });

            if (!TryParseDate(req.EndDate, out var end) || end is null)
                return Results.BadRequest(new { message = "تاريخ نهاية الإجازة غير صالح." });

            if (end < start)
                return Results.BadRequest(new { message = "تاريخ نهاية الإجازة لا يمكن أن يسبق تاريخ البدء." });

            int days = req.DaysCount.HasValue && req.DaysCount.Value > 0
                ? req.DaysCount.Value
                : (end.Value.DayNumber - start.Value.DayNumber) + 1;

            var leave = new EmployeeLeave
            {
                EmployeeId = employee.Id,
                LeaveTypeId = req.LeaveTypeId,
                StartDate = start.Value,
                EndDate = end.Value,
                DaysCount = days,
                Notes = req.Notes?.Trim(),
                Status = "قيد المراجعة",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            db.EmployeeLeaves.Add(leave);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/leaves/{leave.Id}", new
            {
                message = "تم تقديم طلب الإجازة بنجاح وهو قيد المراجعة الإدارية.",
                leaveId = leave.Id,
                startDate = leave.StartDate,
                endDate = leave.EndDate,
                daysCount = leave.DaysCount,
                status = leave.Status
            });
        })
        .WithName("SubmitMyLeaveRequest")
        .WithSummary("تقديم طلب إجازة جديد مباشرة عبر تطبيق الهاتف");

        // 1. Get all leaves with filtering & paging (Protected against unprivileged global enumeration)
        group.MapGet("/", async (
            [FromQuery] int? employeeId,
            [FromQuery] string? employeeNumber,
            [FromQuery] int? leaveTypeId,
            [FromQuery] string? status,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            ClaimsPrincipal principal = null!,
            UserManager<ApplicationUser> userManager = null!,
            ApplicationDbContext db = null!,
            CancellationToken ct = default) =>
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var query = db.EmployeeLeaves
                .AsNoTracking()
                .Include(l => l.Employee).ThenInclude(e => e.Formation)
                .Include(l => l.LeaveType)
                .AsQueryable();

            if (!IsStaff(principal))
            {
                var currentEmpId = await GetCurrentEmployeeIdAsync(principal, userManager);
                if (string.IsNullOrWhiteSpace(currentEmpId))
                {
                    return Results.Json(new { message = "غير مصرح لك باستعراض سجلات الموظفين." }, statusCode: StatusCodes.Status403Forbidden);
                }
                query = query.Where(l => l.Employee.EmployeeID == currentEmpId);
            }
            else
            {
                if (employeeId.HasValue)
                {
                    query = query.Where(l => l.EmployeeId == employeeId.Value);
                }

                if (!string.IsNullOrWhiteSpace(employeeNumber))
                {
                    var num = employeeNumber.Trim();
                    query = query.Where(l => l.Employee.EmployeeID == num);
                }
            }

            if (leaveTypeId.HasValue)
            {
                query = query.Where(l => l.LeaveTypeId == leaveTypeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var st = status.Trim();
                query = query.Where(l => l.Status == st);
            }

            if (DateOnly.TryParse(from, out var fromDate))
            {
                query = query.Where(l => l.StartDate >= fromDate);
            }

            if (DateOnly.TryParse(to, out var toDate))
            {
                query = query.Where(l => l.EndDate <= toDate);
            }

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(l => l.StartDate)
                .ThenByDescending(l => l.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new LeaveDto(
                    l.Id,
                    l.EmployeeId,
                    l.Employee.EmployeeID,
                    l.Employee.Name,
                    l.Employee.Formation != null ? l.Employee.Formation.Name : null,
                    l.LeaveTypeId,
                    l.LeaveType != null ? l.LeaveType.Name : null,
                    l.StartDate,
                    l.EndDate,
                    l.DaysCount,
                    l.OrderNumber,
                    l.OrderDate,
                    l.Notes,
                    l.Status,
                    l.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                items
            });
        })
        .WithName("GetLeaves")
        .WithSummary("استعلام سجلات إجازات الموظفين مع الفرز والبحث والتصفية");

        // 2. Get leave types lookup
        group.MapGet("/types", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            var types = await db.LeaveTypes
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync(ct);

            return Results.Ok(types);
        })
        .WithName("GetLeaveTypes")
        .WithSummary("استرجاع أنواع الإجازات المعتمدة");

        // 3. Get single leave by ID (With IDOR check)
        group.MapGet("/{id:int}", async (
            int id,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var leave = await db.EmployeeLeaves
                .AsNoTracking()
                .Include(l => l.Employee).ThenInclude(e => e.Formation)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(l => l.Id == id, ct);

            if (leave is null) return Results.NotFound(new { message = "سجل الإجازة غير موجود." });

            if (!IsStaff(principal))
            {
                var currentEmpId = await GetCurrentEmployeeIdAsync(principal, userManager);
                if (string.IsNullOrWhiteSpace(currentEmpId) || !string.Equals(leave.Employee.EmployeeID, currentEmpId, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Json(new { message = "غير مصرح لك بالوصول إلى هذا السجل." }, statusCode: StatusCodes.Status403Forbidden);
                }
            }

            return Results.Ok(new LeaveDto(
                leave.Id,
                leave.EmployeeId,
                leave.Employee.EmployeeID,
                leave.Employee.Name,
                leave.Employee.Formation?.Name,
                leave.LeaveTypeId,
                leave.LeaveType?.Name,
                leave.StartDate,
                leave.EndDate,
                leave.DaysCount,
                leave.OrderNumber,
                leave.OrderDate,
                leave.Notes,
                leave.Status,
                leave.CreatedAtUtc));
        })
        .WithName("GetLeaveById")
        .WithSummary("استعلام تفاصيل إجازة محددة عبر المعرف");

        // 4. Get leaves for an employee (With IDOR check)
        group.MapGet("/employee/{employeeId:int}", async (
            int employeeId,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            if (!IsStaff(principal))
            {
                var currentEmpId = await GetCurrentEmployeeIdAsync(principal, userManager);
                var targetEmp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
                if (targetEmp == null || !string.Equals(targetEmp.EmployeeID, currentEmpId, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Json(new { message = "غير مصرح لك بالوصول إلى هذا السجل." }, statusCode: StatusCodes.Status403Forbidden);
                }
            }

            var leaves = await db.EmployeeLeaves
                .AsNoTracking()
                .Include(l => l.Employee).ThenInclude(e => e.Formation)
                .Include(l => l.LeaveType)
                .Where(l => l.EmployeeId == employeeId)
                .OrderByDescending(l => l.StartDate)
                .Select(l => new LeaveDto(
                    l.Id,
                    l.EmployeeId,
                    l.Employee.EmployeeID,
                    l.Employee.Name,
                    l.Employee.Formation != null ? l.Employee.Formation.Name : null,
                    l.LeaveTypeId,
                    l.LeaveType != null ? l.LeaveType.Name : null,
                    l.StartDate,
                    l.EndDate,
                    l.DaysCount,
                    l.OrderNumber,
                    l.OrderDate,
                    l.Notes,
                    l.Status,
                    l.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(leaves);
        })
        .WithName("GetLeavesByEmployeeId")
        .WithSummary("استعراض كافة إجازات موظف محدد");

        // 5. Create new leave (Staff or self)
        group.MapPost("/", async (
            CreateLeaveRequest req,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            Employee? employee = null;

            if (req.EmployeeId.HasValue)
            {
                employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == req.EmployeeId.Value, ct);
            }
            else if (!string.IsNullOrWhiteSpace(req.EmployeeID))
            {
                var num = req.EmployeeID.Trim();
                employee = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeID == num, ct);
            }

            if (employee is null)
            {
                return Results.BadRequest(new { message = "الموظف المحدد غير موجود في قاعدة البيانات." });
            }

            if (!IsStaff(principal))
            {
                var currentEmpId = await GetCurrentEmployeeIdAsync(principal, userManager);
                if (!string.Equals(employee.EmployeeID, currentEmpId, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Json(new { message = "لا يمكنك تسجيل إجازة لموظف آخر." }, statusCode: StatusCodes.Status403Forbidden);
                }
            }

            if (employee is null)
            {
                return Results.BadRequest(new { message = "الموظف المحدد غير موجود في قاعدة البيانات." });
            }

            if (!TryParseDate(req.StartDate, out var start) || start is null)
            {
                return Results.BadRequest(new { message = "تاريخ بدء الإجازة غير صحيح (الصيغة المطلوبة: yyyy-MM-dd أو dd/MM/yyyy)." });
            }

            if (!TryParseDate(req.EndDate, out var end) || end is null)
            {
                return Results.BadRequest(new { message = "تاريخ نهاية الإجازة غير صحيح." });
            }

            if (end < start)
            {
                return Results.BadRequest(new { message = "تاريخ نهاية الإجازة لا يمكن أن يكون قبل تاريخ بدئها." });
            }

            int days = req.DaysCount.HasValue && req.DaysCount.Value > 0
                ? req.DaysCount.Value
                : (end.Value.DayNumber - start.Value.DayNumber) + 1;

            int? typeId = req.LeaveTypeId;
            if (!typeId.HasValue && !string.IsNullOrWhiteSpace(req.LeaveTypeName))
            {
                var normName = LookupNormalization.Normalize(req.LeaveTypeName);
                var existingType = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Name == req.LeaveTypeName.Trim(), ct);
                if (existingType is null)
                {
                    existingType = new LeaveType { Name = req.LeaveTypeName.Trim() };
                    db.LeaveTypes.Add(existingType);
                    await db.SaveChangesAsync(ct);
                }
                typeId = existingType.Id;
            }

            DateOnly? orderDate = null;
            if (!string.IsNullOrWhiteSpace(req.OrderDate) && TryParseDate(req.OrderDate, out var od))
            {
                orderDate = od;
            }

            var leave = new EmployeeLeave
            {
                EmployeeId = employee.Id,
                LeaveTypeId = typeId,
                StartDate = start.Value,
                EndDate = end.Value,
                DaysCount = days,
                OrderNumber = req.OrderNumber?.Trim(),
                OrderDate = orderDate,
                Notes = req.Notes?.Trim(),
                Status = !string.IsNullOrWhiteSpace(req.Status) ? req.Status.Trim() : "معتمدة",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            db.EmployeeLeaves.Add(leave);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/leaves/{leave.Id}", new { message = "تم تسجيل الإجازة بنجاح.", id = leave.Id });
        })
        .WithName("CreateLeave")
        .WithSummary("تسجيل إجازة جديدة لموظف");

        // 6. Update leave
        group.MapPut("/{id:int}", async (int id, UpdateLeaveRequest req, ApplicationDbContext db, CancellationToken ct) =>
        {
            var leave = await db.EmployeeLeaves.FirstOrDefaultAsync(l => l.Id == id, ct);
            if (leave is null) return Results.NotFound(new { message = "سجل الإجازة غير موجود." });

            if (!TryParseDate(req.StartDate, out var start) || start is null)
            {
                return Results.BadRequest(new { message = "تاريخ بدء الإجازة غير صحيح." });
            }

            if (!TryParseDate(req.EndDate, out var end) || end is null)
            {
                return Results.BadRequest(new { message = "تاريخ نهاية الإجازة غير صحيح." });
            }

            if (end < start)
            {
                return Results.BadRequest(new { message = "تاريخ نهاية الإجازة لا يمكن أن يكون قبل تاريخ بدئها." });
            }

            int days = req.DaysCount.HasValue && req.DaysCount.Value > 0
                ? req.DaysCount.Value
                : (end.Value.DayNumber - start.Value.DayNumber) + 1;

            if (req.LeaveTypeId.HasValue)
            {
                leave.LeaveTypeId = req.LeaveTypeId;
            }
            else if (!string.IsNullOrWhiteSpace(req.LeaveTypeName))
            {
                var existingType = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Name == req.LeaveTypeName.Trim(), ct);
                if (existingType is null)
                {
                    existingType = new LeaveType { Name = req.LeaveTypeName.Trim() };
                    db.LeaveTypes.Add(existingType);
                    await db.SaveChangesAsync(ct);
                }
                leave.LeaveTypeId = existingType.Id;
            }

            DateOnly? orderDate = null;
            if (!string.IsNullOrWhiteSpace(req.OrderDate) && TryParseDate(req.OrderDate, out var od))
            {
                orderDate = od;
            }

            leave.StartDate = start.Value;
            leave.EndDate = end.Value;
            leave.DaysCount = days;
            leave.OrderNumber = req.OrderNumber?.Trim();
            leave.OrderDate = orderDate;
            leave.Notes = req.Notes?.Trim();
            if (!string.IsNullOrWhiteSpace(req.Status))
            {
                leave.Status = req.Status.Trim();
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "تم تحديث سجل الإجازة بنجاح." });
        })
        .WithName("UpdateLeave")
        .WithSummary("تعديل بيانات إجازة مسجلة");

        // 7. Delete leave
        group.MapDelete("/{id:int}", async (int id, ApplicationDbContext db, CancellationToken ct) =>
        {
            var leave = await db.EmployeeLeaves.FirstOrDefaultAsync(l => l.Id == id, ct);
            if (leave is null) return Results.NotFound(new { message = "سجل الإجازة غير موجود." });

            db.EmployeeLeaves.Remove(leave);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "تم حذف سجل الإجازة بنجاح." });
        })
        .WithName("DeleteLeave")
        .WithSummary("حذف سجل إجازة");

        // 8. Bulk import JSON
        group.MapPost("/import/json", async (LeaveImportRequest req, ApplicationDbContext db, CancellationToken ct) =>
        {
            var (success, response, errors, message) = await ExecuteImportAsync(req, db, ct);
            if (!success)
            {
                if (errors is { Count: > 0 })
                {
                    return Results.BadRequest(new { errors });
                }
                return Results.BadRequest(new { message });
            }
            return Results.Ok(response);
        })
        .WithName("ImportLeavesFromJson")
        .WithSummary("استيراد إجازات الموظفين دفعة واحدة عبر JSON");

        // 9. Bulk import Excel
        group.MapPost("/import/excel", async (
            IFormFile file,
            [FromQuery] string? mode,
            [FromQuery] bool? createMissingLookups,
            IExcelImportService importService,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "يرجى تحديد ملف إكسل صالح." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".xlsx" or ".xlsm"))
            {
                return Results.BadRequest(new { message = "الملف يجب أن يكون بصيغة Excel (.xlsx)." });
            }

            using var stream = file.OpenReadStream();
            var parseResult = await importService.ParseLeavesExcelAsync(stream, ct);

            if (parseResult.HasErrors)
            {
                return Results.BadRequest(new
                {
                    message = "فشل تحليل أعمدة أو بيانات ملف الإجازات.",
                    errors = parseResult.ParseErrors
                });
            }

            if (parseResult.Rows.Count == 0)
            {
                return Results.BadRequest(new { message = "لم يتم العثور على أي صفوف بيانات صالحة للاستيراد." });
            }

            try
            {
                var request = new LeaveImportRequest(
                    Mode: mode ?? "upsert",
                    Leaves: parseResult.Rows,
                    CreateMissingLookups: createMissingLookups ?? true);

                var response = await importService.ImportLeavesAsync(request, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithName("ImportLeavesFromExcel")
        .WithSummary("استيراد إجازات الموظفين عبر رفع ملف Excel (.xlsx)");

        return endpoints;
    }

    public static async Task<(bool Success, LeaveImportResponse? Response, IReadOnlyList<EmployeeImportError>? Errors, string? ErrorMessage)> ExecuteImportAsync(
        LeaveImportRequest request,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mode = request.Mode?.Trim().ToLowerInvariant();
        var createMissingLookups = request.CreateMissingLookups ?? true;

        if (mode is not ("insert" or "upsert"))
        {
            return (false, null, null, "Mode must be either 'insert' or 'upsert'.");
        }

        if (request.Leaves is null || request.Leaves.Count == 0)
        {
            return (false, null, null, "At least one leave record is required.");
        }

        if (request.Leaves.Count > MaximumRowsPerRequest)
        {
            return (false, null, null, $"A request cannot contain more than {MaximumRowsPerRequest} leaves.");
        }

        var isUpsert = mode == "upsert";
        var errors = new List<EmployeeImportError>();
        var requestedEmployeeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < request.Leaves.Count; index++)
        {
            var row = request.Leaves[index];
            var rowNumber = index + 1;

            if (row is null)
            {
                errors.Add(new EmployeeImportError(rowNumber, "leave", "Leave data is required."));
                continue;
            }

            var employeeId = row.EmployeeID?.Trim();
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                errors.Add(new EmployeeImportError(rowNumber, "employeeID", "Employee ID is required."));
            }
            else
            {
                requestedEmployeeIds.Add(employeeId);
            }
        }

        if (errors.Count > 0)
        {
            return (false, null, errors, null);
        }

        var employees = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in requestedEmployeeIds.Chunk(1000))
        {
            var batchList = batch.ToList();
            var found = await dbContext.Employees
                .Where(e => batchList.Contains(e.EmployeeID))
                .ToListAsync(cancellationToken);

            foreach (var emp in found)
            {
                employees[emp.EmployeeID] = emp;
            }
        }

        var seenKeys = new HashSet<(string EmployeeId, DateOnly StartDate, DateOnly EndDate, string Type)>();

        for (var index = 0; index < request.Leaves.Count; index++)
        {
            var row = request.Leaves[index];
            var rowNumber = index + 1;
            var employeeId = row.EmployeeID.Trim();

            if (!employees.ContainsKey(employeeId))
            {
                continue; // skipped row
            }

            DateOnly? startDate = null;
            if (string.IsNullOrWhiteSpace(row.StartDate))
            {
                if (!string.IsNullOrWhiteSpace(row.DaysCount))
                {
                    startDate = new DateOnly(DateTime.Today.Year, 1, 1);
                }
                else
                {
                    errors.Add(new EmployeeImportError(rowNumber, "startDate", "تاريخ البدء أو مجموع الإجازات مطلوب."));
                }
            }
            else if (!TryParseDate(row.StartDate, out startDate) || startDate is null)
            {
                errors.Add(new EmployeeImportError(rowNumber, "startDate", "Start date must use dd/MM/yyyy or yyyy-MM-dd format."));
            }

            DateOnly? endDate = null;
            if (!string.IsNullOrWhiteSpace(row.EndDate))
            {
                if (!TryParseDate(row.EndDate, out endDate) || endDate is null)
                {
                    errors.Add(new EmployeeImportError(rowNumber, "endDate", "End date must use dd/MM/yyyy or yyyy-MM-dd format."));
                }
                else if (startDate.HasValue && endDate.Value < startDate.Value)
                {
                    errors.Add(new EmployeeImportError(rowNumber, "endDate", "End date cannot be before start date."));
                }
            }
            else if (startDate.HasValue)
            {
                var normDaysStr = ExcelImportService.NormalizeDaysCount(row.DaysCount);
                int daysVal = 1;
                if (!string.IsNullOrWhiteSpace(normDaysStr) && int.TryParse(normDaysStr, out var pd) && pd > 0)
                {
                    daysVal = pd;
                }
                endDate = startDate.Value.AddDays(Math.Max(1, daysVal) - 1);
            }

            if (startDate.HasValue && !isUpsert)
            {
                var effectiveEnd = endDate ?? startDate.Value;
                var normType = LookupNormalization.Normalize(row.LeaveType) ?? string.Empty;
                if (!seenKeys.Add((employeeId, startDate.Value, effectiveEnd, normType)))
                {
                    errors.Add(new EmployeeImportError(rowNumber, "startDate", "Employee ID, start date, end date, and leave type are duplicated in the request."));
                }
            }
        }

        if (errors.Count > 0)
        {
            return (false, null, errors, null);
        }

        var types = await LoadLeaveTypes(dbContext, cancellationToken);
        var leaveIndex = new Dictionary<(int EmployeeId, DateOnly StartDate, DateOnly EndDate, string Type), EmployeeLeave>();
        var leaveTypeDateIndex = new Dictionary<(int EmployeeId, DateOnly StartDate, string Type), EmployeeLeave>();

        if (isUpsert)
        {
            var empDbIds = employees.Values.Select(e => e.Id).Distinct().ToList();
            foreach (var batch in empDbIds.Chunk(1000))
            {
                var batchList = batch.ToList();
                var existingLeaves = await dbContext.EmployeeLeaves
                    .Include(l => l.LeaveType)
                    .Where(l => batchList.Contains(l.EmployeeId))
                    .ToListAsync(cancellationToken);

                foreach (var leave in existingLeaves)
                {
                    var typeKey = leave.LeaveType != null ? (LookupNormalization.Normalize(leave.LeaveType.Name) ?? string.Empty) : string.Empty;
                    leaveIndex[(leave.EmployeeId, leave.StartDate, leave.EndDate, typeKey)] = leave;
                    leaveTypeDateIndex[(leave.EmployeeId, leave.StartDate, typeKey)] = leave;
                }
            }
        }

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var missingEmployeeIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        // Turn off AutoDetectChanges for high performance with 14,000+ records
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            for (var index = 0; index < request.Leaves.Count; index++)
            {
                var row = request.Leaves[index];
                var employeeId = row.EmployeeID.Trim();

                if (!employees.TryGetValue(employeeId, out var employee))
                {
                    skipped++;
                    missingEmployeeIds.Add(employeeId);
                    continue;
                }

                var startDate = ParseValidatedDate(row.StartDate) ?? new DateOnly(DateTime.Today.Year, 1, 1);
                var normDaysStr = ExcelImportService.NormalizeDaysCount(row.DaysCount);
                int days = 1;
                if (!string.IsNullOrWhiteSpace(normDaysStr) && int.TryParse(normDaysStr, out var parsedDays) && parsedDays > 0)
                {
                    days = parsedDays;
                }
                else if (ParseValidatedDate(row.EndDate) is { } endParsed)
                {
                    days = (endParsed.DayNumber - startDate.DayNumber) + 1;
                    if (days < 1) days = 1;
                }

                var endDate = ParseValidatedDate(row.EndDate) ?? startDate.AddDays(Math.Max(1, days) - 1);

                var normType = LookupNormalization.Normalize(row.LeaveType) ?? string.Empty;
                var matchKey = (employee.Id, startDate, endDate, normType);
                var typeDateKey = (employee.Id, startDate, normType);

                EmployeeLeave? leave = null;

                if (isUpsert && (leaveIndex.TryGetValue(matchKey, out var existing) ||
                                 leaveTypeDateIndex.TryGetValue(typeDateKey, out existing)))
                {
                    // Exact match or match on (Employee + StartDate + LeaveType) -> Update existing record
                    leave = existing;
                    updated++;
                }
                else
                {
                    // Different dates or type -> New record
                    leave = new EmployeeLeave
                    {
                        EmployeeId = employee.Id,
                        StartDate = startDate,
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    };

                    dbContext.EmployeeLeaves.Add(leave);

                    if (isUpsert)
                    {
                        leaveIndex[matchKey] = leave;
                        leaveTypeDateIndex[typeDateKey] = leave;
                    }

                    inserted++;
                }

                leave.EndDate = endDate;
                leave.DaysCount = days;
                leave.LeaveType = ResolveLeaveType(row.LeaveType, types, dbContext, createMissingLookups, errors, index + 1);
                leave.OrderNumber = NormalizeOptional(row.OrderNumber);
                leave.OrderDate = ParseValidatedDate(row.OrderDate);
                leave.Notes = NormalizeOptional(row.Notes);
                leave.Status = !string.IsNullOrWhiteSpace(row.Status) ? row.Status.Trim() : "معتمدة";
            }
        }
        finally
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        if (errors.Count > 0)
        {
            return (false, null, errors, null);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, new LeaveImportResponse(
            request.Leaves.Count,
            inserted,
            updated,
            skipped,
            missingEmployeeIds.ToList()), null, null);
    }

    private static async Task<Dictionary<string, LeaveType>> LoadLeaveTypes(ApplicationDbContext db, CancellationToken ct)
    {
        var values = await db.LeaveTypes.ToListAsync(ct);
        var map = new Dictionary<string, LeaveType>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in values)
        {
            var key = LookupNormalization.Normalize(t.Name);
            if (key is not null) map[key] = t;
        }
        return map;
    }

    private static LeaveType? ResolveLeaveType(
        string? rawValue,
        IDictionary<string, LeaveType> types,
        ApplicationDbContext db,
        bool createMissing,
        ICollection<EmployeeImportError> errors,
        int rowNumber)
    {
        var value = LookupNormalization.Normalize(rawValue);
        if (value is null) return null;

        if (types.TryGetValue(value, out var type)) return type;

        if (!createMissing)
        {
            errors.Add(new EmployeeImportError(rowNumber, "leaveType", $"نوع الإجازة '{value}' غير موجود مسبقاً."));
            return null;
        }

        type = new LeaveType { Name = value };
        db.LeaveTypes.Add(type);
        types[value] = type;
        return type;
    }

    private static bool TryParseDate(string? rawValue, out DateOnly? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(rawValue)) return true;

        var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "d/M/yyyy" };
        if (!DateOnly.TryParseExact(rawValue.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static DateOnly? ParseValidatedDate(string? rawValue)
    {
        TryParseDate(rawValue, out var value);
        return value;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
}
