using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

/// <summary>
/// دورة تدريبية للموظف (Employee Training Course)
/// </summary>
public sealed class EmployeeCourse
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>اسم الدورة التدريبية</summary>
    [MaxLength(300)]
    public required string CourseName { get; set; }

    /// <summary>تاريخ بدء الدورة</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>تاريخ نهاية الدورة</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>عدد أيام الدورة (محسوب أو مدخل)</summary>
    public int? DaysCount { get; set; }

    /// <summary>الجهة المنظمة / مكان انعقاد الدورة (اختياري)</summary>
    [MaxLength(200)]
    public string? Institution { get; set; }

    /// <summary>حالة الدورة (معتمدة، مجتازة، مستمرة)</summary>
    [MaxLength(50)]
    public string Status { get; set; } = "معتمدة";

    /// <summary>ملاحظات إضافية</summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
