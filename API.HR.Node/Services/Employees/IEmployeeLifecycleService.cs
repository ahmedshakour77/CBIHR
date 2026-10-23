namespace API.HR.Node.Services.Employees;

public interface IEmployeeLifecycleService
{
    /// <summary>
    /// استبعاد / أرشفة موظف مؤقتاً بحيث لا يظهر في أي تقرير أو لوحة تحكم أو إحصائية أو دليل للموظفين.
    /// يتم الاحتفاظ بكامل بياناته وتاريخه الوظيفي مع إمكانية استرجاعه في أي وقت.
    /// </summary>
    Task<EmployeeLifecycleResult> ArchiveEmployeeAsync(string employeeId, string reason, string performedBy, CancellationToken ct = default);

    /// <summary>
    /// إلغاء الاستبعاد المؤقت واسترجاع الموظف إلى النظام والتقارير بنشاط كامل.
    /// </summary>
    Task<EmployeeLifecycleResult> RestoreEmployeeAsync(string employeeId, string performedBy, CancellationToken ct = default);

    /// <summary>
    /// إزالة موظف من النظام بشكل نهائي (Hard Delete) مع تنظيف كافة سجلاته والارتباطات التابعة له بأمان كامل.
    /// </summary>
    Task<EmployeeLifecycleResult> DeletePermanentlyAsync(string employeeId, string performedBy, CancellationToken ct = default);

    /// <summary>
    /// جلب قائمة الموظفين المستبعدين مؤقتاً (الأرشيف) لإدارتهم واسترجاعهم.
    /// </summary>
    Task<List<ArchivedEmployeeDto>> GetArchivedEmployeesAsync(string? search = null, CancellationToken ct = default);

    /// <summary>
    /// جلب عدد الموظفين المستبعدين مؤقتاً
    /// </summary>
    Task<int> GetArchivedCountAsync(CancellationToken ct = default);
}

public sealed record EmployeeLifecycleResult(bool Succeeded, string Message, string? EmployeeName = null);

public sealed record ArchivedEmployeeDto(
    int Id,
    string EmployeeID,
    string Name,
    string? Formation,
    string? JobTitle,
    DateTime? ArchivedAt,
    string? ArchiveReason,
    string? ArchivedBy);
