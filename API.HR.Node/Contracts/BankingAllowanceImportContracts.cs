namespace API.HR.Node.Contracts;

public sealed record BankingAllowanceImportRow(
    int RowNumber,
    string EmployeeID,
    string? EmployeeName,
    string? Formation,
    string? JobTitle,
    string? Stage,
    string? Grade,
    string? Certificate,
    string? Specialization,
    string? HireDateStr,
    string? ResponsibilityRaw,
    decimal? ExtractedPercentage,
    bool IsMatched,
    string? MatchedName,
    decimal? BasicSalary,
    decimal? CalculatedAmount,
    string? StatusMessage
);

public sealed record BankingAllowanceImportRequest(
    IReadOnlyList<BankingAllowanceImportRow> Rows,
    DateOnly? EffectiveStartDate = null,
    string? OrderNumber = null,
    string? ChangeReason = null
);

public sealed record BankingAllowanceImportResponse(
    int TotalRows,
    int ImportedCount,
    int UpdatedCount,
    int SkippedCount,
    List<EmployeeImportError> Errors,
    string Message
);
