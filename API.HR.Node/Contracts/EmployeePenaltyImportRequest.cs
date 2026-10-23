namespace API.HR.Node.Contracts;

public sealed record EmployeePenaltyImportRequest(
    string Mode,
    IReadOnlyList<EmployeePenaltyImportRow> Penalties,
    bool? CreateMissingLookups = null);

public sealed record EmployeePenaltyImportRow(
    string EmployeeID,         // رقم الموظف (الرقم الوظيفي)
    string? EmployeeName,      // أسم الموظف
    string? Directorate,       // أسم المديرية
    string? JobTitle,          // العنوان الوظيفي
    string? PenaltyName,       // العقوبة (مثل إنذار، لفت نظر، قطع راتب...)
    string? PenaltyDate,       // تاريخ نيل العقوبة
    string? OrderNumber,       // رقم الكتاب
    string? Notes);            // الملاحظات (نوع العقوبة / الملاحظات كما طلب المستخدم)

public sealed record EmployeePenaltyImportResponse(
    int TotalRows,
    int Inserted,
    int Updated,
    int Skipped,
    IReadOnlyList<string> MissingEmployeeIDs);
