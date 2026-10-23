namespace API.HR.Node.Contracts;

public sealed record LastAllowanceDateImportRow(
    int RowNumber,
    string EmployeeID,
    string? EmployeeName,
    string? AllowanceDateRaw,
    DateOnly? ParsedDate,
    bool IsMatched,
    string? MatchedName,
    DateOnly? CurrentAllowanceDate,
    bool IsValid,
    string? StatusMessage
);

public sealed record LastAllowanceDateImportRequest(
    IReadOnlyList<LastAllowanceDateImportRow> Rows
);

public sealed record LastAllowanceDateImportResponse(
    int TotalRows,
    int UpdatedCount,
    int SkippedCount,
    List<EmployeeImportError> Errors,
    string Message
);
