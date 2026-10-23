namespace API.HR.Node.Contracts;

public sealed record AppreciationLetterImportRequest(
    string Mode,
    IReadOnlyList<AppreciationLetterImportRow> Letters,
    bool? CreateMissingLookups = null);

public sealed record AppreciationLetterImportRow(
    string EmployeeID,   // رقم الموظف
    string? Title,       // كتب الشكر
    string? LetterDate,  // تاريخ كتب الشكر
    string? Count,       // عدد كتب الشكر
    string? Type,        // نوع كتب شكر
    string? Notes,       // الملاحظات
    string? LetterNumber); // رقم كتاب

public sealed record AppreciationLetterImportResponse(
    int TotalRows,
    int Inserted,
    int Updated,
    // Rows whose employee is not on file: skipped, not imported.
    int Skipped,
    IReadOnlyList<string> MissingEmployeeIDs);
