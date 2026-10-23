using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace API.HR.Node.Endpoints;

public static class EmployeeStatisticsEndpoints
{
    private const string UnspecifiedLabel = "غير محدد";

    // Age is derived from the date of birth, so it is not a lookup column like the
    // other dimensions and is handled separately.
    private const string AgeDimension = "age";

    // Upper bound is inclusive; the last band is open-ended.
    private static readonly (string Label, int Minimum, int Maximum)[] AgeBands =
    [
        ("أقل من 25", 0, 24),
        ("25 - 29", 25, 29),
        ("30 - 34", 30, 34),
        ("35 - 39", 35, 39),
        ("40 - 44", 40, 44),
        ("45 - 49", 45, 49),
        ("50 - 54", 50, 54),
        ("55 - 59", 55, 59),
        ("60 فأكثر", 60, int.MaxValue),
    ];

    // Category selectors keyed by the dimension name used in the route.
    private static readonly Dictionary<string, Expression<Func<Employee, string?>>> Dimensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["formation"] = employee => employee.Formation!.Name,
            ["jobTitle"] = employee => employee.JobTitle!.Name,
            ["positionTitle"] = employee => employee.PositionTitle!.Name,
            ["stage"] = employee => employee.Stage!.Name,
            ["grade"] = employee => employee.Grade!.Name,
            ["educationLevel"] = employee => employee.EducationLevel!.Name,
            ["specialization"] = employee => employee.Specialization!.Name,
            ["gender"] = employee => employee.Gender!.Name,
            ["religion"] = employee => employee.Religion!.Name,
            ["nationality"] = employee => employee.Nationality!.Name,
            ["status"] = employee => employee.EmployeeStatus!.Name,
            ["department"] = employee => employee.FirstDepartment,
        };

    public static IEndpointRouteBuilder MapEmployeeStatisticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Literal "statistics" outranks the "/{employeeID}" route, so there is no conflict.
        var group = endpoints.MapGroup("/api/employees/statistics")
            .WithTags("Statistics")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        group.MapGet("", GetSummary)
            .WithName("GetEmployeeStatistics")
            .WithSummary("Gets total employees plus counts for every category, for a dashboard.");

        group.MapGet("/{dimension}", GetByDimension)
            .WithName("GetEmployeeStatisticsByDimension")
            .WithSummary("Gets employee counts grouped by a single category (e.g. formation, grade).");

        return endpoints;
    }

    private static async Task<IResult> GetSummary(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var total = await dbContext.Employees.CountAsync(cancellationToken);
        var (ageBands, ageStatistics) = await GetAgeAsync(dbContext, cancellationToken);

        return Results.Ok(new EmployeeStatisticsResponse(
            total,
            await CountByAsync(dbContext, Dimensions["formation"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["jobTitle"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["positionTitle"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["stage"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["grade"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["educationLevel"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["specialization"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["gender"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["religion"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["nationality"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["status"], cancellationToken),
            await CountByAsync(dbContext, Dimensions["department"], cancellationToken),
            ageBands,
            ageStatistics));
    }

    private static async Task<IResult> GetByDimension(
        string dimension,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.Equals(dimension, AgeDimension, StringComparison.OrdinalIgnoreCase))
        {
            var (bands, _) = await GetAgeAsync(dbContext, cancellationToken);
            return Results.Ok(bands);
        }

        if (!Dimensions.TryGetValue(dimension, out var selector))
        {
            return Results.BadRequest(new
            {
                message = $"Unknown dimension '{dimension}'.",
                allowed = Dimensions.Keys.Append(AgeDimension).OrderBy(key => key)
            });
        }

        var counts = await CountByAsync(dbContext, selector, cancellationToken);
        return Results.Ok(counts);
    }

    /// <summary>Buckets employees into age bands and summarises the spread.</summary>
    private static async Task<(List<CategoryCount> Bands, AgeStatistics Statistics)> GetAgeAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Age is derived, so the bucketing runs in memory: the query pulls one small
        // column per employee and this keeps date arithmetic off the database provider.
        var birthDates = await dbContext.Employees
            .AsNoTracking()
            .Select(employee => employee.BirthDate)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var ages = new List<int>(birthDates.Count);
        var unknown = 0;

        foreach (var birthDate in birthDates)
        {
            // A missing or nonsensical date of birth is counted on its own rather
            // than being allowed to skew the bands.
            if (birthDate is null)
            {
                unknown++;
                continue;
            }

            var age = CalculateAge(birthDate.Value, today);
            if (age is < 0 or > 120)
            {
                unknown++;
                continue;
            }

            ages.Add(age);
        }

        var bands = AgeBands
            .Select(band => new CategoryCount(
                band.Label,
                ages.Count(age => age >= band.Minimum && age <= band.Maximum)))
            .ToList();

        // Only surface the unspecified bucket when it actually holds someone.
        if (unknown > 0)
        {
            bands.Add(new CategoryCount(UnspecifiedLabel, unknown));
        }

        return (bands, SummariseAges(ages, unknown));
    }

    private static AgeStatistics SummariseAges(List<int> ages, int unknown)
    {
        if (ages.Count == 0)
        {
            return new AgeStatistics(0, unknown, null, null, null, null);
        }

        ages.Sort();
        var middle = ages.Count / 2;
        var median = ages.Count % 2 == 0
            ? (ages[middle - 1] + ages[middle]) / 2d
            : ages[middle];

        return new AgeStatistics(
            ages.Count,
            unknown,
            ages[0],
            ages[^1],
            Math.Round(ages.Average(), 1),
            Math.Round(median, 1));
    }

    /// <summary>Whole years elapsed, counting a birthday later in the year as not yet reached.</summary>
    private static int CalculateAge(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static async Task<List<CategoryCount>> CountByAsync(
        ApplicationDbContext dbContext,
        Expression<Func<Employee, string?>> categorySelector,
        CancellationToken cancellationToken)
    {
        var groups = await dbContext.Employees
            .AsNoTracking()
            .GroupBy(categorySelector)
            .Select(group => new { Category = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        // Coalesce the null bucket (employees with no value) to a label, then order
        // for a dashboard: biggest first, ties broken alphabetically.
        return groups
            .Select(group => new CategoryCount(group.Category ?? UnspecifiedLabel, group.Count))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Category, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
