using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

/// <summary>
/// سجل حركة وتنقل وظيفي ومحطات الخدمة (Career Movement / Service History Period)
/// </summary>
public sealed class EmployeeCareerMovement
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>تاريخ الحركة أو بداية المحطة الوظيفية</summary>
    public DateOnly MovementDate { get; set; }

    /// <summary>بيان التشكيل أو الحالة (نص حر كما ورد في السجل)</summary>
    [MaxLength(300)]
    public required string FormationName { get; set; }

    /// <summary>احتساب الفترة لأغراض العلاوة السنوية</summary>
    public bool CountForAllowance { get; set; } = true;

    /// <summary>احتساب الفترة لأغراض الترفيع الوظيفي</summary>
    public bool CountForPromotion { get; set; } = true;

    /// <summary>احتساب الفترة لأغراض التقاعد القانوني</summary>
    public bool CountForPension { get; set; } = true;

    /// <summary>ملاحظات إضافية عن الحركة أو الأمر الإداري</summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
