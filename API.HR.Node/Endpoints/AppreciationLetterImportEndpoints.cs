using System.Security.Claims;
using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace API.HR.Node.Endpoints;

public static class AppreciationLetterImportEndpoints
{
    private const int MaximumRowsPerRequest = 100_000;

    public static IEndpointRouteBuilder MapAppreciationLetterImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/appreciation-letters/import/json", ImportJson)
            .WithTags("AppreciationLetters")
            .WithName("ImportAppreciationLettersFromJson")
            .WithSummary("Imports or updates employee appreciation letters (كتب الشكر) from a JSON request.")
            .Accepts<AppreciationLetterImportRequest>("application/json")
            .RequireAuthorization(AuthorizationPolicies.ImportAppreciationLetters);

        endpoints.MapGet("/api/appreciation-letters/types", async (ApplicationDbContext dbContext, CancellationToken ct) =>
        {
            var types = await dbContext.AppreciationLetterTypes
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.SeniorityYears,
                    t.SeniorityMonths,
                    t.SeniorityDays
                })
                .ToListAsync(ct);
            return Results.Ok(types);
        })
        .WithTags("AppreciationLetters")
        .WithName("GetAppreciationLetterTypes")
        .WithSummary("Returns all appreciation letter types with their seniority impact (سنة، شهر، يوم).")
        .RequireAuthorization();

        endpoints.MapPost("/api/appreciation-letters/types", async (CreateAppreciationLetterTypeDto dto, ApplicationDbContext dbContext, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return Results.BadRequest(new { message = "اسم نوع كتاب الشكر مطلوب." });

            var trimmed = dto.Name.Trim();
            var exists = await dbContext.AppreciationLetterTypes.AnyAsync(t => t.Name == trimmed, ct);
            if (exists)
                return Results.Conflict(new { message = "نوع كتاب الشكر موجود مسبقاً." });

            var entity = new AppreciationLetterType
            {
                Name = trimmed,
                SeniorityYears = Math.Max(0, dto.SeniorityYears),
                SeniorityMonths = Math.Max(0, dto.SeniorityMonths),
                SeniorityDays = Math.Max(0, dto.SeniorityDays)
            };

            dbContext.AppreciationLetterTypes.Add(entity);
            await dbContext.SaveChangesAsync(ct);

            return Results.Created($"/api/appreciation-letters/types/{entity.Id}", entity);
        })
        .WithTags("AppreciationLetters")
        .WithName("CreateAppreciationLetterType")
        .WithSummary("Creates a new appreciation letter type with seniority impact.")
        .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        endpoints.MapPut("/api/appreciation-letters/types/{id:int}", async (int id, UpdateAppreciationLetterTypeDto dto, ApplicationDbContext dbContext, CancellationToken ct) =>
        {
            var entity = await dbContext.AppreciationLetterTypes.FindAsync(new object[] { id }, ct);
            if (entity is null)
                return Results.NotFound(new { message = "نوع كتاب الشكر غير موجود." });

            if (!string.IsNullOrWhiteSpace(dto.Name))
                entity.Name = dto.Name.Trim();

            entity.SeniorityYears = Math.Max(0, dto.SeniorityYears);
            entity.SeniorityMonths = Math.Max(0, dto.SeniorityMonths);
            entity.SeniorityDays = Math.Max(0, dto.SeniorityDays);

            await dbContext.SaveChangesAsync(ct);
            return Results.Ok(entity);
        })
        .WithTags("AppreciationLetters")
        .WithName("UpdateAppreciationLetterType")
        .WithSummary("Updates an appreciation letter type and its seniority impact (سنة، شهر، يوم).")
        .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        // Mobile: Get current employee's appreciation letters
        endpoints.MapGet("/api/appreciation-letters/me", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var empId = await GetCurrentEmployeeIdAsync(principal, userManager);
            if (string.IsNullOrWhiteSpace(empId))
            {
                return Results.Json(new { message = "هذا الحساب غير مرتبط بسجل موظف." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var letters = await dbContext.AppreciationLetters.AsNoTracking()
                .Include(l => l.TitleLookup)
                .Include(l => l.Type)
                .Where(l => l.Employee.EmployeeID == empId)
                .OrderByDescending(l => l.LetterDate ?? DateOnly.MinValue)
                .Select(l => new
                {
                    l.Id,
                    l.LetterNumber,
                    letterDate = l.LetterDate,
                    count = l.Count ?? 1,
                    title = l.TitleLookup != null ? l.TitleLookup.Name : l.Title,
                    typeName = l.Type != null ? l.Type.Name : null,
                    seniorityYears = l.Type != null ? l.Type.SeniorityYears : 0,
                    seniorityMonths = l.Type != null ? l.Type.SeniorityMonths : 0,
                    seniorityDays = l.Type != null ? l.Type.SeniorityDays : 0,
                    l.Notes
                })
                .ToListAsync(ct);

            return Results.Ok(letters);
        })
        .WithTags("AppreciationLetters")
        .WithName("GetMyAppreciationLetters")
        .WithSummary("استرجاع كتب الشكر والتقدير الخاصة بالموظف الحالي مع أثر القِدم الممنوح")
        .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // Staff/Self: Get letters by employee number
        endpoints.MapGet("/api/appreciation-letters/employee/{employeeNumber}", async (
            string employeeNumber,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!IsStaff(principal))
            {
                var currentEmpId = await GetCurrentEmployeeIdAsync(principal, userManager);
                if (!string.Equals(employeeNumber, currentEmpId, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Json(new { message = "غير مصرح لك باستعراض كتب شكر هذا الموظف." }, statusCode: StatusCodes.Status403Forbidden);
                }
            }

            var letters = await dbContext.AppreciationLetters.AsNoTracking()
                .Include(l => l.TitleLookup)
                .Include(l => l.Type)
                .Where(l => l.Employee.EmployeeID == employeeNumber)
                .OrderByDescending(l => l.LetterDate ?? DateOnly.MinValue)
                .Select(l => new
                {
                    l.Id,
                    l.LetterNumber,
                    letterDate = l.LetterDate,
                    count = l.Count ?? 1,
                    title = l.TitleLookup != null ? l.TitleLookup.Name : l.Title,
                    typeName = l.Type != null ? l.Type.Name : null,
                    seniorityYears = l.Type != null ? l.Type.SeniorityYears : 0,
                    seniorityMonths = l.Type != null ? l.Type.SeniorityMonths : 0,
                    seniorityDays = l.Type != null ? l.Type.SeniorityDays : 0,
                    l.Notes
                })
                .ToListAsync(ct);

            return Results.Ok(letters);
        })
        .WithTags("AppreciationLetters")
        .WithName("GetEmployeeAppreciationLetters")
        .WithSummary("استرجاع كتب الشكر والتقدير لموظف محدد")
        .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        return endpoints;
    }

    private static async Task<IResult> ImportJson(
        AppreciationLetterImportRequest request,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var (success, response, errors, message) = await ExecuteImportAsync(request, dbContext, cancellationToken);
        if (!success)
        {
            if (errors is { Count: > 0 })
            {
                return Results.BadRequest(new { errors });
            }

            return Results.BadRequest(new { message });
        }

        return Results.Ok(response);
    }

    public static async Task<(bool Success, AppreciationLetterImportResponse? Response, IReadOnlyList<EmployeeImportError>? Errors, string? ErrorMessage)> ExecuteImportAsync(
        AppreciationLetterImportRequest request,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mode = request.Mode?.Trim().ToLowerInvariant();
        var createMissingLookups = request.CreateMissingLookups ?? true;

        if (mode is not ("insert" or "upsert"))
        {
            return (false, null, null, "Mode must be either 'insert' or 'upsert'.");
        }

        if (request.Letters is null || request.Letters.Count == 0)
        {
            return (false, null, null, "At least one appreciation letter is required.");
        }

        if (request.Letters.Count > MaximumRowsPerRequest)
        {
            return (false, null, null, $"A request cannot contain more than {MaximumRowsPerRequest} appreciation letters.");
        }

        var isUpsert = mode == "upsert";
        var errors = new List<EmployeeImportError>();
        var requestedEmployeeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < request.Letters.Count; index++)
        {
            var row = request.Letters[index];
            var rowNumber = index + 1;

            if (row is null)
            {
                errors.Add(new EmployeeImportError(rowNumber, "letter", "Appreciation letter data is required."));
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

        var employeeKeys = requestedEmployeeIds.ToList();
        var employees = (await dbContext.Employees
                .Where(employee => employeeKeys.Contains(employee.EmployeeID))
                .ToListAsync(cancellationToken))
            .ToDictionary(employee => employee.EmployeeID, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < request.Letters.Count; index++)
        {
            var row = request.Letters[index];
            var rowNumber = index + 1;
            var employeeId = row.EmployeeID.Trim();

            if (!employees.ContainsKey(employeeId))
            {
                continue;
            }

            if (!TryParseImportDate(row.LetterDate, out var letterDate))
            {
                errors.Add(new EmployeeImportError(rowNumber, "letterDate", "Date must use dd/MM/yyyy or yyyy-MM-dd format."));
            }

            if (!TryParseCount(row.Count, out _))
            {
                errors.Add(new EmployeeImportError(rowNumber, "count", "Count must be a non-negative whole number."));
            }
        }

        if (errors.Count > 0)
        {
            return (false, null, errors, null);
        }

        var titles = await LoadTitles(dbContext, cancellationToken);
        var types = await LoadTypes(dbContext, cancellationToken);

        // Existing letters for the referenced employees for upsert resolution
        List<AppreciationLetter> existingLetters = [];
        if (isUpsert)
        {
            var employeeIds = employees.Values.Select(employee => employee.Id).ToList();

            existingLetters = await dbContext.AppreciationLetters
                .Where(letter => employeeIds.Contains(letter.EmployeeId))
                .ToListAsync(cancellationToken);
        }

        var inserted = 0;
        var updated = 0;
        var skipped = 0;

        // Letters whose employee is not on file are skipped rather than failing the
        // whole import; the ids are reported back so they can be chased up.
        var missingEmployeeIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < request.Letters.Count; index++)
        {
            var row = request.Letters[index];
            var employeeId = row.EmployeeID.Trim();

            if (!employees.TryGetValue(employeeId, out var employee))
            {
                skipped++;
                missingEmployeeIds.Add(employeeId);
                continue;
            }

            var letterDate = ParseValidatedDate(row.LetterDate);
            var letterNum = NormalizeOptional(row.LetterNumber);
            var titleNorm = LookupNormalization.Normalize(row.Title);
            AppreciationLetter? letter = null;

            if (isUpsert)
            {
                // Priority 1: Match by (EmployeeId + LetterNumber) if letter number is present
                if (!string.IsNullOrWhiteSpace(letterNum))
                {
                    letter = existingLetters.FirstOrDefault(l =>
                        l.EmployeeId == employee.Id &&
                        string.Equals(l.LetterNumber?.Trim(), letterNum, StringComparison.OrdinalIgnoreCase));
                }

                // Priority 2: Match by (EmployeeId + LetterDate + Title)
                if (letter is null && letterDate.HasValue && !string.IsNullOrWhiteSpace(titleNorm))
                {
                    letter = existingLetters.FirstOrDefault(l =>
                        l.EmployeeId == employee.Id &&
                        l.LetterDate == letterDate &&
                        string.Equals(LookupNormalization.Normalize(l.Title), titleNorm, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (letter != null)
            {
                updated++;
            }
            else
            {
                letter = new AppreciationLetter
                {
                    EmployeeId = employee.Id,
                };

                dbContext.AppreciationLetters.Add(letter);

                if (isUpsert)
                {
                    existingLetters.Add(letter);
                }

                inserted++;
            }

            letter.LetterDate = letterDate;

            // Resolve Title Lookup (كتب الشكر - قائمة منسدلة)
            var resolvedTitle = ResolveTitle(row.Title, titles, dbContext, createMissingLookups, errors, index + 1);
            letter.TitleLookup = resolvedTitle;
            letter.Title = resolvedTitle?.Name ?? NormalizeOptional(row.Title);

            // Count: defaults to 1 if not specified
            var parsedCount = ParseValidatedCount(row.Count);
            letter.Count = parsedCount is null or <= 0 ? 1 : parsedCount.Value;

            // Resolve Type Lookup (نوع كتب شكر - قائمة منسدلة)
            letter.Type = ResolveType(row.Type, types, dbContext, createMissingLookups, errors, index + 1);
            letter.Notes = NormalizeOptional(row.Notes);
            letter.LetterNumber = letterNum;
        }

        if (errors.Count > 0)
        {
            return (false, null, errors, null);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, new AppreciationLetterImportResponse(
            request.Letters.Count,
            inserted,
            updated,
            skipped,
            missingEmployeeIds.ToList()), null, null);
    }

    private static async Task<Dictionary<string, AppreciationLetterTitle>> LoadTitles(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var values = await dbContext.AppreciationLetterTitles.ToListAsync(cancellationToken);
        var titles = new Dictionary<string, AppreciationLetterTitle>(StringComparer.OrdinalIgnoreCase);

        foreach (var title in values)
        {
            var key = LookupNormalization.Normalize(title.Name);
            if (key is not null)
            {
                titles[key] = title;
            }
        }

        return titles;
    }

    private static AppreciationLetterTitle? ResolveTitle(
        string? rawValue,
        IDictionary<string, AppreciationLetterTitle> titles,
        ApplicationDbContext dbContext,
        bool createMissing,
        ICollection<EmployeeImportError> errors,
        int rowNumber)
    {
        var value = LookupNormalization.Normalize(rawValue);

        if (value is null)
        {
            return null;
        }

        if (titles.TryGetValue(value, out var title))
        {
            return title;
        }

        if (!createMissing)
        {
            errors.Add(new EmployeeImportError(rowNumber, "title", $"Lookup value '{value}' was not found."));
            return null;
        }

        title = new AppreciationLetterTitle { Name = value };
        dbContext.AppreciationLetterTitles.Add(title);
        titles.Add(value, title);
        return title;
    }

    private static async Task<Dictionary<string, AppreciationLetterType>> LoadTypes(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var values = await dbContext.AppreciationLetterTypes.ToListAsync(cancellationToken);
        var types = new Dictionary<string, AppreciationLetterType>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in values)
        {
            var key = LookupNormalization.Normalize(type.Name);
            if (key is not null)
            {
                types[key] = type;
            }
        }

        return types;
    }

    private static AppreciationLetterType? ResolveType(
        string? rawValue,
        IDictionary<string, AppreciationLetterType> types,
        ApplicationDbContext dbContext,
        bool createMissing,
        ICollection<EmployeeImportError> errors,
        int rowNumber)
    {
        var value = LookupNormalization.Normalize(rawValue);

        if (value is null)
        {
            return null;
        }

        if (types.TryGetValue(value, out var type))
        {
            return type;
        }

        if (!createMissing)
        {
            errors.Add(new EmployeeImportError(rowNumber, "type", $"Lookup value '{value}' was not found."));
            return null;
        }

        type = new AppreciationLetterType { Name = value };
        dbContext.AppreciationLetterTypes.Add(type);
        types.Add(value, type);
        return type;
    }

    private static bool TryParseCount(string? rawValue, out int? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        if (!int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < 0)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static int? ParseValidatedCount(string? rawValue)
    {
        TryParseCount(rawValue, out var value);
        return value;
    }

    private static DateOnly? ParseValidatedDate(string? rawValue)
    {
        TryParseImportDate(rawValue, out var value);
        return value;
    }

    private static bool TryParseImportDate(string? rawValue, out DateOnly? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "d/M/yyyy" };

        if (!DateOnly.TryParseExact(
                rawValue.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
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

public sealed record CreateAppreciationLetterTypeDto(string Name, int SeniorityYears = 0, int SeniorityMonths = 0, int SeniorityDays = 0);
public sealed record UpdateAppreciationLetterTypeDto(string? Name, int SeniorityYears = 0, int SeniorityMonths = 0, int SeniorityDays = 0);

