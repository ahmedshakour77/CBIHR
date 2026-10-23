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

public static class CourseEndpoints
{
    private const int MaximumRowsPerRequest = 100_000;

    public static IEndpointRouteBuilder MapCourseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/courses")
            .WithTags("Courses")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // Mobile: Get current employee's training courses
        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var empId = await GetCurrentEmployeeIdAsync(principal, userManager);
            if (string.IsNullOrWhiteSpace(empId))
            {
                return Results.Json(new { message = "هذا الحساب غير مرتبط بسجل موظف." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var courses = await db.EmployeeCourses
                .AsNoTracking()
                .Include(c => c.Employee).ThenInclude(e => e.Formation)
                .Where(c => c.Employee.EmployeeID == empId)
                .OrderByDescending(c => c.StartDate ?? DateOnly.MinValue)
                .Select(c => new CourseDto(
                    c.Id,
                    c.EmployeeId,
                    c.Employee.EmployeeID,
                    c.Employee.Name,
                    c.Employee.Formation != null ? c.Employee.Formation.Name : null,
                    c.CourseName,
                    c.StartDate,
                    c.EndDate,
                    c.DaysCount,
                    c.Institution,
                    c.Notes,
                    c.Status,
                    c.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(courses);
        })
        .WithName("GetMyCourses")
        .WithSummary("استرجاع الدورات التدريبية الخاصة بالموظف الحالي لتطبيق الهاتف");

        // 1. Get all courses with filtering & paging (Protected against unprivileged global enumeration)
        group.MapGet("/", async (
            [FromQuery] int? employeeId,
            [FromQuery] string? employeeNumber,
            [FromQuery] string? courseName,
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

            var query = db.EmployeeCourses
                .AsNoTracking()
                .Include(c => c.Employee).ThenInclude(e => e.Formation)
                .AsQueryable();

            if (!IsStaff(principal))
            {
                var currentEmpId = await GetCurrentEmployeeIdAsync(principal, userManager);
                if (string.IsNullOrWhiteSpace(currentEmpId))
                {
                    return Results.Json(new { message = "غير مصرح لك باستعراض سجلات الموظفين." }, statusCode: StatusCodes.Status403Forbidden);
                }
                query = query.Where(c => c.Employee.EmployeeID == currentEmpId);
            }
            else
            {
                if (employeeId.HasValue)
                {
                    query = query.Where(c => c.EmployeeId == employeeId.Value);
                }

                if (!string.IsNullOrWhiteSpace(employeeNumber))
                {
                    var num = employeeNumber.Trim();
                    query = query.Where(c => c.Employee.EmployeeID == num);
                }
            }

            if (!string.IsNullOrWhiteSpace(courseName))
            {
                var name = courseName.Trim();
                query = query.Where(c => c.CourseName.Contains(name));
            }

            if (DateOnly.TryParse(from, out var fromDate))
            {
                query = query.Where(c => c.StartDate >= fromDate);
            }

            if (DateOnly.TryParse(to, out var toDate))
            {
                query = query.Where(c => c.EndDate <= toDate);
            }

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(c => c.StartDate ?? DateOnly.MinValue)
                .ThenByDescending(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CourseDto(
                    c.Id,
                    c.EmployeeId,
                    c.Employee.EmployeeID,
                    c.Employee.Name,
                    c.Employee.Formation != null ? c.Employee.Formation.Name : null,
                    c.CourseName,
                    c.StartDate,
                    c.EndDate,
                    c.DaysCount,
                    c.Institution,
                    c.Notes,
                    c.Status,
                    c.CreatedAtUtc))
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
        .WithName("GetCourses")
        .WithSummary("استعلام سجلات الدورات التدريبية مع الفرز والتصفية والبحث");

        // 2. Get distinct course names for dropdown / auto-complete
        group.MapGet("/names", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            var names = await db.EmployeeCourses
                .AsNoTracking()
                .Select(c => c.CourseName.Trim())
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync(ct);

            return Results.Ok(names);
        })
        .WithName("GetDistinctCourseNames")
        .WithSummary("استرجاع قائمة بأسماء الدورات التدريبية المعتمدة للاقتراح والفرز");

        // 3. Get single course by ID (With IDOR check)
        group.MapGet("/{id:int}", async (
            int id,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var course = await db.EmployeeCourses
                .AsNoTracking()
                .Include(c => c.Employee).ThenInclude(e => e.Formation)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (course is null) return Results.NotFound(new { message = "سجل الدورة التدريبية غير موجود." });

            if (!IsStaff(principal))
            {
                var currentEmpId = await GetCurrentEmployeeIdAsync(principal, userManager);
                if (string.IsNullOrWhiteSpace(currentEmpId) || !string.Equals(course.Employee.EmployeeID, currentEmpId, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Json(new { message = "غير مصرح لك باستعراض بيانات هذه الدورة." }, statusCode: StatusCodes.Status403Forbidden);
                }
            }

            return Results.Ok(new CourseDto(
                course.Id,
                course.EmployeeId,
                course.Employee.EmployeeID,
                course.Employee.Name,
                course.Employee.Formation?.Name,
                course.CourseName,
                course.StartDate,
                course.EndDate,
                course.DaysCount,
                course.Institution,
                course.Notes,
                course.Status,
                course.CreatedAtUtc));
        })
        .WithName("GetCourseById")
        .WithSummary("استعلام تفاصيل دورة تدريبية محددة");

        // 4. Get courses for an employee (With IDOR check)
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
                    return Results.Json(new { message = "غير مصرح لك باستعراض دورات هذا الموظف." }, statusCode: StatusCodes.Status403Forbidden);
                }
            }

            var courses = await db.EmployeeCourses
                .AsNoTracking()
                .Include(c => c.Employee).ThenInclude(e => e.Formation)
                .Where(c => c.EmployeeId == employeeId)
                .OrderByDescending(c => c.StartDate ?? DateOnly.MinValue)
                .Select(c => new CourseDto(
                    c.Id,
                    c.EmployeeId,
                    c.Employee.EmployeeID,
                    c.Employee.Name,
                    c.Employee.Formation != null ? c.Employee.Formation.Name : null,
                    c.CourseName,
                    c.StartDate,
                    c.EndDate,
                    c.DaysCount,
                    c.Institution,
                    c.Notes,
                    c.Status,
                    c.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(courses);
        })
        .WithName("GetCoursesByEmployeeId")
        .WithSummary("استعراض الدورات التدريبية لموظف محدد");

        // 5. Create new course
        group.MapPost("/", async (CreateCourseRequest req, ApplicationDbContext db, CancellationToken ct) =>
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

            if (string.IsNullOrWhiteSpace(req.CourseName))
            {
                return Results.BadRequest(new { message = "اسم الدورة التدريبية مطلوب." });
            }

            DateOnly? start = null;
            if (!string.IsNullOrWhiteSpace(req.StartDate) && TryParseDate(req.StartDate, out var s))
            {
                start = s;
            }

            DateOnly? end = null;
            if (!string.IsNullOrWhiteSpace(req.EndDate) && TryParseDate(req.EndDate, out var e))
            {
                end = e;
            }

            if (start.HasValue && end.HasValue && end.Value < start.Value)
            {
                return Results.BadRequest(new { message = "تاريخ نهاية الدورة لا يمكن أن يكون قبل تاريخ بدئها." });
            }

            int? days = req.DaysCount;
            if ((!days.HasValue || days.Value <= 0) && start.HasValue && end.HasValue)
            {
                days = (end.Value.DayNumber - start.Value.DayNumber) + 1;
            }

            var course = new EmployeeCourse
            {
                EmployeeId = employee.Id,
                CourseName = req.CourseName.Trim(),
                StartDate = start,
                EndDate = end,
                DaysCount = days,
                Institution = NormalizeOptional(req.Institution),
                Notes = NormalizeOptional(req.Notes),
                Status = !string.IsNullOrWhiteSpace(req.Status) ? req.Status.Trim() : "معتمدة",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            db.EmployeeCourses.Add(course);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/courses/{course.Id}", new CourseDto(
                course.Id,
                course.EmployeeId,
                employee.EmployeeID,
                employee.Name,
                null,
                course.CourseName,
                course.StartDate,
                course.EndDate,
                course.DaysCount,
                course.Institution,
                course.Notes,
                course.Status,
                course.CreatedAtUtc));
        })
        .WithName("CreateCourse")
        .WithSummary("تسجيل دورة تدريبية جديدة لموظف");

        // 6. Update course
        group.MapPut("/{id:int}", async (int id, UpdateCourseRequest req, ApplicationDbContext db, CancellationToken ct) =>
        {
            var course = await db.EmployeeCourses.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (course is null) return Results.NotFound(new { message = "سجل الدورة التدريبية غير موجود." });

            if (string.IsNullOrWhiteSpace(req.CourseName))
            {
                return Results.BadRequest(new { message = "اسم الدورة التدريبية مطلوب." });
            }

            DateOnly? start = null;
            if (!string.IsNullOrWhiteSpace(req.StartDate) && TryParseDate(req.StartDate, out var s))
            {
                start = s;
            }

            DateOnly? end = null;
            if (!string.IsNullOrWhiteSpace(req.EndDate) && TryParseDate(req.EndDate, out var e))
            {
                end = e;
            }

            if (start.HasValue && end.HasValue && end.Value < start.Value)
            {
                return Results.BadRequest(new { message = "تاريخ نهاية الدورة لا يمكن أن يكون قبل تاريخ بدئها." });
            }

            int? days = req.DaysCount;
            if ((!days.HasValue || days.Value <= 0) && start.HasValue && end.HasValue)
            {
                days = (end.Value.DayNumber - start.Value.DayNumber) + 1;
            }

            course.CourseName = req.CourseName.Trim();
            course.StartDate = start;
            course.EndDate = end;
            course.DaysCount = days;
            course.Institution = NormalizeOptional(req.Institution);
            course.Notes = NormalizeOptional(req.Notes);
            if (!string.IsNullOrWhiteSpace(req.Status)) course.Status = req.Status.Trim();

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "تم تحديث بيانات الدورة التدريبية بنجاح." });
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("UpdateCourse")
        .WithSummary("تعديل بيانات دورة تدريبية");

        // 7. Delete course
        group.MapDelete("/{id:int}", async (int id, ApplicationDbContext db, CancellationToken ct) =>
        {
            var course = await db.EmployeeCourses.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (course is null) return Results.NotFound(new { message = "سجل الدورة التدريبية غير موجود." });

            db.EmployeeCourses.Remove(course);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "تم حذف سجل الدورة التدريبية بنجاح." });
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("DeleteCourse")
        .WithSummary("حذف سجل دورة تدريبية");

        // 8. Import courses (JSON Payload)
        group.MapPost("/import", async (
            CourseImportRequest req,
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
        .WithName("ImportCourses")
        .WithSummary("استيراد دورات الموظفين دفعة واحدة مع التحقق من صحة البيانات");

        // 9. Import from Excel File
        group.MapPost("/import/excel", async (
            IFormFile file,
            [FromForm] string? mode,
            IExcelImportService importService,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "يرجى اختيار ملف Excel صالح (.xlsx)." });
            }

            using var stream = file.OpenReadStream();
            var parseResult = await importService.ParseCoursesExcelAsync(stream, ct);

            if (parseResult.HasErrors)
            {
                return Results.BadRequest(new
                {
                    message = "يحتوي الملف على أخطاء تمنع الاستيراد.",
                    errors = parseResult.ParseErrors
                });
            }

            try
            {
                var request = new CourseImportRequest(
                    Mode: mode ?? "upsert",
                    Courses: parseResult.Rows);

                var response = await importService.ImportCoursesAsync(request, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .DisableAntiforgery()
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("ImportCoursesExcel")
        .WithSummary("استيراد وتحديث دورات الموظفين عبر ملف Excel (.xlsx)");

        // 10. Excel Template Download
        endpoints.MapGet("/api/import/template/courses", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateCoursesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "نموذج_استيراد_الدورات_التدريبية.xlsx");
        })
        .WithTags("Excel Import")
        .RequireAuthorization();

        return endpoints;
    }

    public static async Task<(bool Success, CourseImportResponse? Response, IReadOnlyList<EmployeeImportError>? Errors, string? ErrorMessage)> ExecuteImportAsync(
        CourseImportRequest request,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mode = request.Mode?.Trim().ToLowerInvariant();

        if (mode is not ("insert" or "upsert"))
        {
            return (false, null, null, "Mode must be either 'insert' or 'upsert'.");
        }

        if (request.Courses is null || request.Courses.Count == 0)
        {
            return (false, null, null, "At least one course record is required.");
        }

        if (request.Courses.Count > MaximumRowsPerRequest)
        {
            return (false, null, null, $"A request cannot contain more than {MaximumRowsPerRequest} courses.");
        }

        var isUpsert = mode == "upsert";
        var errors = new List<EmployeeImportError>();
        var requestedEmployeeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < request.Courses.Count; index++)
        {
            var row = request.Courses[index];
            var rowNumber = index + 1;

            if (row is null)
            {
                errors.Add(new EmployeeImportError(rowNumber, "course", "Course data is required."));
                continue;
            }

            var employeeId = row.EmployeeID?.Trim();
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                errors.Add(new EmployeeImportError(rowNumber, "employeeID", "الرقم الوظيفي مطلوب."));
            }
            else
            {
                requestedEmployeeIds.Add(employeeId);
            }

            if (string.IsNullOrWhiteSpace(row.CourseName))
            {
                errors.Add(new EmployeeImportError(rowNumber, "courseName", "اسم الدورة مطلوب."));
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

        var seenKeys = new HashSet<(string EmployeeId, string CourseName, string StartDate)>();

        for (var index = 0; index < request.Courses.Count; index++)
        {
            var row = request.Courses[index];
            var rowNumber = index + 1;
            var employeeId = row.EmployeeID.Trim();

            if (!employees.ContainsKey(employeeId))
            {
                continue; // skipped row (unregistered employee)
            }

            DateOnly? startDate = null;
            if (!string.IsNullOrWhiteSpace(row.StartDate))
            {
                if (!TryParseDate(row.StartDate, out startDate) || startDate is null)
                {
                    errors.Add(new EmployeeImportError(rowNumber, "startDate", "صيغة تاريخ بدء الدورة غير صحيحة (dd/MM/yyyy أو yyyy-MM-dd)."));
                }
            }

            DateOnly? endDate = null;
            if (!string.IsNullOrWhiteSpace(row.EndDate))
            {
                if (!TryParseDate(row.EndDate, out endDate) || endDate is null)
                {
                    errors.Add(new EmployeeImportError(rowNumber, "endDate", "صيغة تاريخ نهاية الدورة غير صحيحة (dd/MM/yyyy أو yyyy-MM-dd)."));
                }
                else if (startDate.HasValue && endDate.Value < startDate.Value)
                {
                    errors.Add(new EmployeeImportError(rowNumber, "endDate", "تاريخ نهاية الدورة لا يمكن أن يكون قبل تاريخ بدئها."));
                }
            }

            if (!isUpsert)
            {
                var normCourse = row.CourseName.Trim().ToLowerInvariant();
                var sDateKey = startDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                if (!seenKeys.Add((employeeId, normCourse, sDateKey)))
                {
                    errors.Add(new EmployeeImportError(rowNumber, "courseName", "الدورة مكررة لنفس الموظف وتاريخ البدء داخل الملف."));
                }
            }
        }

        if (errors.Count > 0)
        {
            return (false, null, errors, null);
        }

        var courseIndex = new Dictionary<(int EmployeeId, string CourseName, string StartDate), EmployeeCourse>();

        if (isUpsert)
        {
            var empDbIds = employees.Values.Select(e => e.Id).Distinct().ToList();
            foreach (var batch in empDbIds.Chunk(1000))
            {
                var batchList = batch.ToList();
                var existingCourses = await dbContext.EmployeeCourses
                    .Where(c => batchList.Contains(c.EmployeeId))
                    .ToListAsync(cancellationToken);

                foreach (var c in existingCourses)
                {
                    var cNameKey = c.CourseName.Trim().ToLowerInvariant();
                    var sDateKey = c.StartDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                    courseIndex[(c.EmployeeId, cNameKey, sDateKey)] = c;
                }
            }
        }

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var missingEmployeeIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            for (var index = 0; index < request.Courses.Count; index++)
            {
                var row = request.Courses[index];
                var employeeId = row.EmployeeID.Trim();

                if (!employees.TryGetValue(employeeId, out var employee))
                {
                    skipped++;
                    missingEmployeeIds.Add(employeeId);
                    continue;
                }

                var startDate = ParseValidatedDate(row.StartDate);
                var endDate = ParseValidatedDate(row.EndDate);

                int? days = null;
                var normDaysStr = ExcelImportService.NormalizeDaysCount(row.DaysCount);
                if (!string.IsNullOrWhiteSpace(normDaysStr) && int.TryParse(normDaysStr, out var pd) && pd > 0)
                {
                    days = pd;
                }
                else if (startDate.HasValue && endDate.HasValue)
                {
                    days = (endDate.Value.DayNumber - startDate.Value.DayNumber) + 1;
                    if (days < 1) days = 1;
                }

                var courseNameTrimmed = row.CourseName.Trim();
                var cNameKey = courseNameTrimmed.ToLowerInvariant();
                var sDateKey = startDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                var matchKey = (employee.Id, cNameKey, sDateKey);

                EmployeeCourse? course = null;

                if (isUpsert && courseIndex.TryGetValue(matchKey, out var existing))
                {
                    course = existing;
                    updated++;
                }
                else
                {
                    course = new EmployeeCourse
                    {
                        EmployeeId = employee.Id,
                        CourseName = courseNameTrimmed,
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    };

                    dbContext.EmployeeCourses.Add(course);

                    if (isUpsert)
                    {
                        courseIndex[matchKey] = course;
                    }

                    inserted++;
                }

                course.CourseName = courseNameTrimmed;
                course.StartDate = startDate;
                course.EndDate = endDate;
                course.DaysCount = days;
                course.Notes = NormalizeOptional(row.Notes);
                course.Status = !string.IsNullOrWhiteSpace(row.Status) ? row.Status.Trim() : "معتمدة";
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

        return (true, new CourseImportResponse(
            request.Courses.Count,
            inserted,
            updated,
            skipped,
            missingEmployeeIds.ToList()), null, null);
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
