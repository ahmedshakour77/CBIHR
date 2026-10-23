namespace API.HR.Node.Contracts;

/// <summary>A single bucket in a breakdown: a category value and how many employees fall in it.</summary>
public sealed record CategoryCount(
    string Category,
    int Count);

/// <summary>Age spread in whole years, over employees with a usable date of birth.</summary>
/// <param name="Known">Employees whose age could be calculated.</param>
/// <param name="Unknown">Employees with a missing or implausible date of birth.</param>
public sealed record AgeStatistics(
    int Known,
    int Unknown,
    int? Youngest,
    int? Oldest,
    double? Average,
    double? Median);

/// <summary>Employee counts across every category, for a dashboard loaded in one call.</summary>
public sealed record EmployeeStatisticsResponse(
    int TotalEmployees,
    IReadOnlyList<CategoryCount> ByFormation,
    IReadOnlyList<CategoryCount> ByJobTitle,
    IReadOnlyList<CategoryCount> ByPositionTitle,
    IReadOnlyList<CategoryCount> ByStage,
    IReadOnlyList<CategoryCount> ByGrade,
    IReadOnlyList<CategoryCount> ByEducationLevel,
    IReadOnlyList<CategoryCount> BySpecialization,
    IReadOnlyList<CategoryCount> ByGender,
    IReadOnlyList<CategoryCount> ByReligion,
    IReadOnlyList<CategoryCount> ByNationality,
    IReadOnlyList<CategoryCount> ByStatus,
    IReadOnlyList<CategoryCount> ByDepartment,
    // Age bands keep their natural order (youngest first) rather than being sorted
    // by count: the sequence is part of what the band means.
    IReadOnlyList<CategoryCount> ByAgeBand,
    AgeStatistics Age);
