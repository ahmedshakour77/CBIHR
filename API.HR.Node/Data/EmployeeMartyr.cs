using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

/// <summary>
/// سجل الشهيد المرتبط بالموظف (ذوي الشهداء)
/// </summary>
public sealed class EmployeeMartyr
{
    public int Id { get; set; }

    // رقم الموظف التسلسلي — المفتاح الأجنبي لجدول الموظفين
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // الرقم الوظيفي كنص للربط والبحث السريع والمطابقة أثناء الاستيراد
    [MaxLength(50)]
    public string EmployeeIdNumber { get; set; } = string.Empty;

    // اسم الشهيد
    [MaxLength(250)]
    public string MartyrName { get; set; } = string.Empty;

    // نوع الشهادة — قائمة منسدلة (ضحايا الإرهاب، الحشد الشعبي، النظام البائد، إلخ)
    public int? MartyrdomTypeId { get; set; }
    public MartyrdomType? MartyrdomType { get; set; }

    // الصلة — قائمة منسدلة (أب، أم، ابن، ابنة، أخ، أخت، زوج، زوجة، إلخ)
    public int? KinshipId { get; set; }
    public MartyrKinship? Kinship { get; set; }

    // رقم القرار — نص حر
    [MaxLength(150)]
    public string? DecisionNumber { get; set; }

    // ملاحظات إضافية
    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// جدول قائمة أنواع الشهادة (Lookup table for Martyrdom Types)
/// </summary>
public sealed class MartyrdomType : ILookupEntity
{
    public int Id { get; set; }

    [MaxLength(250)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

/// <summary>
/// جدول قائمة صلة القرابة بالشهيد (Lookup table for Martyr Kinship / Relationship)
/// </summary>
public sealed class MartyrKinship : ILookupEntity
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string Name { get; set; }

    /// <summary>درجة القرابة (1: درجة أولى، 2: درجة ثانية، إلخ)</summary>
    public int Degree { get; set; } = 1;
}
