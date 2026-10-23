using API.HR.Node.Contracts;

namespace API.HR.Node.Services.Import;

public sealed record ExcelParseResult<T>(
    IReadOnlyList<T> Rows,
    IReadOnlyList<string> MatchedHeaders,
    IReadOnlyList<string> UnmappedHeaders,
    IReadOnlyList<EmployeeImportError> ParseErrors,
    int TotalRowsInSheet)
{
    public bool HasErrors => ParseErrors.Count > 0;
    public IReadOnlyList<string> DetectedHeaders => MatchedHeaders.Concat(UnmappedHeaders).ToList();
}
