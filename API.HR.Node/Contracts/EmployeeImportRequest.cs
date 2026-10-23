namespace API.HR.Node.Contracts;

public sealed record EmployeeImportRequest(
    string Mode,
    IReadOnlyList<EmployeeImportRow> Employees,
    bool? CreateMissingLookups = null);

public sealed record EmployeeImportRow(
    string EmployeeID,
    string Name,
    string? Formation,
    string? JobTitle,
    string? PositionTitle,
    string? Stage,
    string? Grade,
    string? EducationLevel,
    string? Specialization,
    string? LastJobTitleDate,
    string? HireDate,
    string? Gender,
    string? Religion,
    string? Nationality,
    string? FirstDepartment,
    string? BirthDate,
    string? EmployeeStatus,
    string? LastAllowanceDate = null);

public sealed record EmployeeImportError(
    int Row,
    string Field,
    string Message);

public sealed record EmployeeImportResponse(
    int TotalRows,
    int Inserted,
    int Updated);


