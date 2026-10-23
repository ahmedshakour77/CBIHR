using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

/// <summary>A commendation / appreciation letter (كتاب شكر) granted to an employee.</summary>
public sealed class AppreciationLetter
{
    public int Id { get; set; }

    // رقم الموظف — linked to Employees.EmployeeID during import, stored as the surrogate FK.
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // كتب الشكر — قائمة منسدلة مع بقاء حقل Title النصي للتوافق الشامل
    public int? TitleId { get; set; }
    public AppreciationLetterTitle? TitleLookup { get; set; }

    [MaxLength(500)]
    public string? Title { get; set; }

    // تاريخ كتب الشكر — تاريخ صدور الكتاب
    public DateOnly? LetterDate { get; set; }

    // عدد كتب الشكر — يدعم تعدد الكتب الصادرة باليوم
    public int? Count { get; set; }

    // نوع كتب شكر — قائمة منسدلة
    public int? TypeId { get; set; }
    public AppreciationLetterType? Type { get; set; }

    // الملاحظات — نص حر
    [MaxLength(1000)]
    public string? Notes { get; set; }

    // رقم كتاب — نص حر
    [MaxLength(100)]
    public string? LetterNumber { get; set; }
}

/// <summary>تصنيف / عنوان كتاب الشكر — قائمة منسدلة (Appreciation-letter Title / Category lookup).</summary>
public sealed class AppreciationLetterTitle : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(200)] public required string Name { get; set; }
}

/// <summary>نوع كتاب شكر — قائمة منسدلة (Appreciation-letter Type lookup).</summary>
public sealed class AppreciationLetterType : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(200)] public required string Name { get; set; }

    /// <summary>تأثير القِدم الممنوح — سنة</summary>
    public int SeniorityYears { get; set; } = 0;

    /// <summary>تأثير القِدم الممنوح — شهر</summary>
    public int SeniorityMonths { get; set; } = 0;

    /// <summary>تأثير القِدم الممنوح — يوم</summary>
    public int SeniorityDays { get; set; } = 0;
}
