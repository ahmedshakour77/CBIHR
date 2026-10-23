namespace API.HR.Node.Contracts;

public sealed record EmployeeEmailAccountImportRequest(
    IReadOnlyList<EmployeeEmailAccountImportRow> Rows);

public sealed record EmployeeEmailAccountImportRow(
    string EmployeeID,
    string Name,
    string Email);

public sealed record EmployeeEmailAccountImportResponse(
    int TotalRows,
    int Inserted,
    int Updated,
    int Skipped,
    IReadOnlyList<string> Details);
