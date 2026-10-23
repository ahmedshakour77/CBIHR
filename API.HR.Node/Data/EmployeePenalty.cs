using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

/// <summary>
/// عقوبة انضباطية مفروضة على الموظف (Disciplinary Penalty / Sanction)
/// </summary>
public sealed class EmployeePenalty
{
    public int Id { get; set; }

    // رقم الموظف — linked to Employees.EmployeeID during import, stored as the surrogate FK.
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // العقوبة — جدول منفصل لقائمة العقوبات (Penalty Type lookup)
    public int? PenaltyTypeId { get; set; }
    public PenaltyType? PenaltyType { get; set; }

    // تاريخ نيل العقوبة / تاريخ العقوبة
    public DateOnly? PenaltyDate { get; set; }

    // رقم الكتاب / رقم الأمر
    [MaxLength(100)]
    public string? OrderNumber { get; set; }

    // الملاحظات — تأخذ "نوع العقوبة" أو الملاحظات الواردة بالملف
    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// جدول قائمة أنواع العقوبات الانضباطية (Lookup table for penalty types)
/// </summary>
public sealed class PenaltyType : ILookupEntity
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    /// <summary>تأثير تأخير العلاوة أو الترفيع بالأشهر</summary>
    public int DelayMonths { get; set; } = 0;

    [MaxLength(500)]
    public string? Description { get; set; }
}
