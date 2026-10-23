using System.Text.Json.Serialization;

namespace API.HR.Node.Contracts;

public sealed record CourseDto(
    int Id,
    int EmployeeId,
    [property: JsonPropertyName("employeeNumber")] string EmployeeNumber,
    string EmployeeName,
    string? FormationName,
    string CourseName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? DaysCount,
    string? Institution,
    string? Notes,
    string Status,
    DateTimeOffset CreatedAtUtc)
{
    [JsonIgnore]
    public string EmployeeID => EmployeeNumber;
}

public sealed record CreateCourseRequest(
    [property: JsonPropertyName("employeeNumber")] string? EmployeeNumber,
    int? EmployeeId,
    string CourseName,
    string? StartDate,
    string? EndDate,
    int? DaysCount,
    string? Institution,
    string? Notes,
    string? Status)
{
    [JsonIgnore]
    public string? EmployeeID => EmployeeNumber;
}

public sealed record UpdateCourseRequest(
    string CourseName,
    string? StartDate,
    string? EndDate,
    int? DaysCount,
    string? Institution,
    string? Notes,
    string? Status);

public sealed record CourseImportRow(
    string EmployeeID,
    string CourseName,
    string? StartDate,
    string? EndDate,
    string? EmployeeName = null,
    string? DaysCount = null,
    string? Institution = null,
    string? Notes = null,
    string? Status = null);

public sealed record CourseImportRequest(
    string Mode,
    IReadOnlyList<CourseImportRow> Courses,
    bool? CreateMissingLookups = null);

public sealed record CourseImportResponse(
    int TotalRows,
    int Inserted,
    int Updated,
    int Skipped,
    IReadOnlyList<string> MissingEmployeeIDs);
