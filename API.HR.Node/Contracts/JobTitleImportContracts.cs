namespace API.HR.Node.Contracts;

public sealed record JobTitleImportRow(
    int RowNumber,
    string? Code,
    string? JobFamily,
    string Name,
    string? GradeName,
    bool IsExisting,
    string? CurrentCode = null,
    string? CurrentFamily = null,
    string? CurrentGrade = null,
    string? StatusMessage = null
);

public sealed record JobTitleImportRequest(
    IReadOnlyList<JobTitleImportRow> Rows,
    bool AutoLinkGrades = true
);

public sealed record JobTitleImportResponse(
    int TotalRows,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    List<EmployeeImportError> Errors,
    string Message
);
