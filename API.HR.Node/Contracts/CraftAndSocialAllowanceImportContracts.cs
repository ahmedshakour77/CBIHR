namespace API.HR.Node.Contracts;

public sealed record CraftAndSocialAllowanceImportRow(
    int RowNumber,
    string EmployeeID,
    string? EmployeeName,
    decimal? CraftPercentage,
    decimal? MarriageFixedAmount,
    decimal? ChildrenFixedAmount,
    decimal? HazardFixedAmount,
    bool IsMatched,
    string? MatchedName,
    decimal? BasicSalary,
    decimal? CraftCalculatedAmount,
    string? StatusMessage
);

public sealed record CraftAndSocialAllowanceImportRequest(
    IReadOnlyList<CraftAndSocialAllowanceImportRow> Rows,
    DateOnly? EffectiveStartDate = null,
    string? OrderNumber = null,
    string? ChangeReason = null
);

public sealed record CraftAndSocialAllowanceImportResponse(
    int TotalRows,
    int ImportedCount,
    int UpdatedCount,
    int SkippedCount,
    List<EmployeeImportError> Errors,
    string Message
);
