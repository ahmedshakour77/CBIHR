using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace API.HR.Node.Endpoints;

public static class EmployeeImportEndpoints
{
    // The whole request is saved in one transaction (all-or-nothing), so this
    // caps memory and transaction size. Raise it if you regularly import more.
    private const int MaximumRowsPerRequest = 100_000;

    public static IEndpointRouteBuilder MapEmployeeImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/employees/import/json", ImportJson)
            .WithTags("Employees")
            .WithName("ImportEmployeesFromJson")
            .WithSummary("Imports or updates employees from a JSON request.")
            .Accepts<EmployeeImportRequest>("application/json")
            .RequireAuthorization(AuthorizationPolicies.ImportEmployees);

        return endpoints;
    }

    private static async Task<IResult> ImportJson(
        EmployeeImportRequest request,
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

    public static async Task<(bool Success, EmployeeImportResponse? Response, IReadOnlyList<EmployeeImportError>? Errors, string? ErrorMessage)> ExecuteImportAsync(
        EmployeeImportRequest request,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mode = request.Mode?.Trim().ToLowerInvariant();
        var createMissingLookups = request.CreateMissingLookups ?? true;

        if (mode is not ("insert" or "upsert"))
        {
            return (false, null, null, "Mode must be either 'insert' or 'upsert'.");
        }

        if (request.Employees is null || request.Employees.Count == 0)
        {
            return (false, null, null, "At least one employee is required.");
        }

        if (request.Employees.Count > MaximumRowsPerRequest)
        {
            return (false, null, null, $"A request cannot contain more than {MaximumRowsPerRequest} employees.");
        }

        var errors = new List<EmployeeImportError>();
        var employeeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < request.Employees.Count; index++)
        {
            var row = request.Employees[index];
            var rowNumber = index + 1;

            if (row is null)
            {
                errors.Add(new EmployeeImportError(rowNumber, "employee", "Employee data is required."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.EmployeeID))
            {
                errors.Add(new EmployeeImportError(rowNumber, "employeeID", "Employee ID is required."));
            }
            else if (!employeeIds.Add(row.EmployeeID.Trim()))
            {
                errors.Add(new EmployeeImportError(rowNumber, "employeeID", "Employee ID is duplicated in the request."));
            }

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                errors.Add(new EmployeeImportError(rowNumber, "name", "Employee name is required."));
            }

            ValidateDate(row.LastJobTitleDate, rowNumber, "lastJobTitleDate", errors);
            ValidateDate(row.LastAllowanceDate, rowNumber, "lastAllowanceDate", errors);
            ValidateDate(row.HireDate, rowNumber, "hireDate", errors);
            ValidateDate(row.BirthDate, rowNumber, "birthDate", errors);
        }

        if (errors.Count > 0)
        {
            return (false, null, errors, null);
        }

        var ids = employeeIds.ToList();
        var existingEmployees = (await dbContext.Employees
                .Where(employee => ids.Contains(employee.EmployeeID))
                .ToListAsync(cancellationToken))
            .ToDictionary(employee => employee.EmployeeID, StringComparer.OrdinalIgnoreCase);

        var formations = await LoadLookups<Formation>(dbContext, cancellationToken);
        var jobTitles = await LoadLookups<JobTitle>(dbContext, cancellationToken);
        var positionTitles = await LoadLookups<PositionTitle>(dbContext, cancellationToken);
        var stages = await LoadLookups<Stage>(dbContext, cancellationToken);
        var grades = await LoadLookups<Grade>(dbContext, cancellationToken);
        var educationLevels = await LoadLookups<EducationLevel>(dbContext, cancellationToken);
        var specializations = await LoadLookups<Specialization>(dbContext, cancellationToken);
        var genders = await LoadLookups<Gender>(dbContext, cancellationToken);
        var religions = await LoadLookups<Religion>(dbContext, cancellationToken);
        var nationalities = await LoadLookups<Nationality>(dbContext, cancellationToken);
        var employeeStatuses = await LoadLookups<EmployeeStatus>(dbContext, cancellationToken);

        var inserted = 0;
        var updated = 0;

        for (var index = 0; index < request.Employees.Count; index++)
        {
            var row = request.Employees[index];
            var rowNumber = index + 1;
            var employeeId = row.EmployeeID.Trim();

            if (existingEmployees.TryGetValue(employeeId, out var employee))
            {
                if (mode == "insert")
                {
                    errors.Add(new EmployeeImportError(rowNumber, "employeeID", "Employee already exists."));
                    continue;
                }

                updated++;
            }
            else
            {
                employee = new Employee
                {
                    EmployeeID = employeeId,
                    Name = row.Name.Trim()
                };

                dbContext.Employees.Add(employee);
                existingEmployees.Add(employeeId, employee);
                inserted++;
            }

            employee.Name = row.Name.Trim();
            employee.Formation = ResolveLookup(row.Formation, formations, dbContext.Formations, createMissingLookups, value => new Formation { Name = value }, errors, rowNumber, "formation");
            employee.JobTitle = ResolveLookup(row.JobTitle, jobTitles, dbContext.JobTitles, createMissingLookups, value => new JobTitle { Name = value }, errors, rowNumber, "jobTitle");
            employee.PositionTitle = ResolveLookup(row.PositionTitle, positionTitles, dbContext.PositionTitles, createMissingLookups, value => new PositionTitle { Name = value }, errors, rowNumber, "positionTitle");
            employee.Stage = ResolveLookup(row.Stage, stages, dbContext.Stages, createMissingLookups, value => new Stage { Name = value }, errors, rowNumber, "stage");
            employee.Grade = ResolveLookup(row.Grade, grades, dbContext.Grades, createMissingLookups, value => new Grade { Name = value }, errors, rowNumber, "grade");
            employee.EducationLevel = ResolveLookup(row.EducationLevel, educationLevels, dbContext.EducationLevels, createMissingLookups, value => new EducationLevel { Name = value }, errors, rowNumber, "educationLevel");
            employee.Specialization = ResolveLookup(row.Specialization, specializations, dbContext.Specializations, createMissingLookups, value => new Specialization { Name = value }, errors, rowNumber, "specialization");
            employee.Gender = ResolveLookup(row.Gender, genders, dbContext.Genders, createMissingLookups, value => new Gender { Name = value }, errors, rowNumber, "gender");
            employee.Religion = ResolveLookup(row.Religion, religions, dbContext.Religions, createMissingLookups, value => new Religion { Name = value }, errors, rowNumber, "religion");
            employee.Nationality = ResolveLookup(row.Nationality, nationalities, dbContext.Nationalities, createMissingLookups, value => new Nationality { Name = value }, errors, rowNumber, "nationality");
            employee.EmployeeStatus = ResolveLookup(row.EmployeeStatus, employeeStatuses, dbContext.EmployeeStatuses, createMissingLookups, value => new EmployeeStatus { Name = value }, errors, rowNumber, "employeeStatus");
            employee.LastJobTitleDate = ParseValidatedDate(row.LastJobTitleDate);
            employee.LastAllowanceDate = ParseValidatedDate(row.LastAllowanceDate);
            employee.HireDate = ParseValidatedDate(row.HireDate);
            employee.FirstDepartment = NormalizeOptional(row.FirstDepartment);
            employee.BirthDate = ParseValidatedDate(row.BirthDate);
        }

        if (errors.Count > 0)
        {
            return (false, null, errors, null);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, new EmployeeImportResponse(
            request.Employees.Count,
            inserted,
            updated), null, null);
    }

    private static async Task<Dictionary<string, TLookup>> LoadLookups<TLookup>(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
        where TLookup : class, ILookupEntity
    {
        var values = await dbContext.Set<TLookup>()
            .ToListAsync(cancellationToken);

        // Key by the normalized name so in-memory matching agrees with the
        // database's unique index (which ignores tatweel, etc.). Last-wins is
        // safe: the index already prevents two collation-equal rows from existing.
        var lookups = new Dictionary<string, TLookup>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in values)
        {
            var key = LookupNormalization.Normalize(entity.Name);
            if (key is not null)
            {
                lookups[key] = entity;
            }
        }

        return lookups;
    }

    private static TLookup? ResolveLookup<TLookup>(
        string? rawValue,
        IDictionary<string, TLookup> values,
        DbSet<TLookup> dbSet,
        bool createMissing,
        Func<string, TLookup> factory,
        ICollection<EmployeeImportError> errors,
        int rowNumber,
        string field)
        where TLookup : class, ILookupEntity
    {
        var value = LookupNormalization.Normalize(rawValue);

        if (value is null)
        {
            return null;
        }

        if (values.TryGetValue(value, out var lookup))
        {
            return lookup;
        }

        if (!createMissing)
        {
            errors.Add(new EmployeeImportError(rowNumber, field, $"Lookup value '{value}' was not found."));
            return null;
        }

        lookup = factory(value);
        dbSet.Add(lookup);
        values.Add(value, lookup);
        return lookup;
    }

    private static void ValidateDate(
        string? rawValue,
        int rowNumber,
        string field,
        ICollection<EmployeeImportError> errors)
    {
        if (!TryParseImportDate(rawValue, out _))
        {
            errors.Add(new EmployeeImportError(
                rowNumber,
                field,
                "Date must use dd/MM/yyyy or yyyy-MM-dd format."));
        }
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

        // Employee sheets use dd/MM/yyyy. ISO is also accepted: the two formats
        // cannot be confused, so allowing both keeps one stray value from
        // rolling back the whole (all-or-nothing) import.
        var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd" };

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
}



