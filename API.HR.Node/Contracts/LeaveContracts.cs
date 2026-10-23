using System.Text.Json.Serialization;

namespace API.HR.Node.Contracts;

public sealed record LeaveDto(
    int Id,
    int EmployeeId,
    [property: JsonPropertyName("employeeNumber")] string EmployeeNumber,
    string EmployeeName,
    string? FormationName,
    int? LeaveTypeId,
    string? LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    int DaysCount,
    string? OrderNumber,
    DateOnly? OrderDate,
    string? Notes,
    string Status,
    DateTimeOffset CreatedAtUtc)
{
    [JsonIgnore]
    public string EmployeeID => EmployeeNumber;
}

public sealed record CreateLeaveRequest(
    [property: JsonPropertyName("employeeNumber")] string? EmployeeNumber,
    int? EmployeeId,
    string? LeaveTypeName,
    int? LeaveTypeId,
    string StartDate,
    string EndDate,
    int? DaysCount,
    string? OrderNumber,
    string? OrderDate,
    string? Notes,
    string? Status)
{
    [JsonIgnore]
    public string? EmployeeID => EmployeeNumber;
}

public sealed record UpdateLeaveRequest(
    string? LeaveTypeName,
    int? LeaveTypeId,
    string StartDate,
    string EndDate,
    int? DaysCount,
    string? OrderNumber,
    string? OrderDate,
    string? Notes,
    string? Status);

public sealed record LeaveImportRow(
    string EmployeeID,
    string? LeaveType,
    string StartDate,
    string? EndDate,
    string? DaysCount,
    string? OrderNumber,
    string? OrderDate,
    string? Notes,
    string? Status,
    string? EmployeeName = null);

public sealed record LeaveImportRequest(
    string Mode,
    IReadOnlyList<LeaveImportRow> Leaves,
    bool? CreateMissingLookups = null);

public sealed record LeaveImportResponse(
    int TotalRows,
    int Inserted,
    int Updated,
    int Skipped,
    IReadOnlyList<string> MissingEmployeeIDs);

public sealed record LeaveBalanceImportRow(
    string EmployeeID,
    string? RawBalance,
    decimal? Balance,
    string? EmployeeName = null,
    bool IsMatched = false,
    string? MatchedName = null,
    decimal? PreviousBalance = null,
    string? StatusMessage = null);

public sealed record LeaveBalanceImportRequest(
    IReadOnlyList<LeaveBalanceImportRow> Rows,
    string? Notes = null);

public sealed record LeaveBalanceImportResponse(
    int TotalRows,
    int Updated,
    int Skipped,
    IReadOnlyList<string> MissingEmployeeIDs,
    string Message);
