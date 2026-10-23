namespace API.HR.Node.Contracts;

public sealed record MaritalStatusImportRow(
    int RowNumber,
    string EmployeeID,
    string? EmployeeName,
    string? MaritalStatusRaw,
    bool IsMatched,
    string? MatchedName,
    string? CurrentMaritalStatus,
    string? ResolvedMaritalStatus,
    string? StatusMessage
);

public sealed record MaritalStatusImportRequest(
    IReadOnlyList<MaritalStatusImportRow> Rows,
    bool CreateMissingStatuses = true
);

public sealed record MaritalStatusImportResponse(
    int TotalRows,
    int UpdatedCount,
    int SkippedCount,
    int NewStatusesCreatedCount,
    List<EmployeeImportError> Errors,
    string Message
);
