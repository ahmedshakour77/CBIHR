using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data.Allowances;

/// <summary>
/// فترة سريان مخصصات الموظف مع حفظ السجل التاريخي الكامل للتغييرات
/// عند إضافة أو تعديل مخصص، تُغلق الفترة الحالية وتُفتح فترة جديدة
/// </summary>
public sealed class EmployeeAllowancePeriod
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>
    /// تاريخ بداية سريان هذه الفترة من المخصصات
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// تاريخ نهاية سريان الفترة (null يعني أن الفترة هي الفعالة حالياً ومستمرة)
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// هل هذه هي الفترة الفعالة والمعتمدة حالياً للموظف
    /// </summary>
    public bool IsCurrent { get; set; } = true;

    /// <summary>
    /// الراتب الإسمي الأساسي المعتمد في هذه الفترة (بالدينار العراقي)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BasicSalary { get; set; }

    /// <summary>
    /// مجموع النسب المئوية لكافة المخصصات النسبية في هذه الفترة (مثلاً: 80%)
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal TotalPercentageRate { get; set; }

    /// <summary>
    /// مجموع مبالغ المخصصات النسبية = (الراتب الإسمي × مجموع النسب / 100)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPercentageAmount { get; set; }

    /// <summary>
    /// مجموع مبالغ المخصصات الثابتة / المقطوعة بالدينار العراقي
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalFixedAmount { get; set; }

    /// <summary>
    /// إجمالي الراتب الشهري = الراتب الإسمي + مجموع المخصصات النسبية + مجموع المخصصات الثابتة
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossSalary { get; set; }

    /// <summary>
    /// رقم الأمر الإداري أو السند الصادر بموجبه التعديل
    /// </summary>
    [MaxLength(100)]
    public string? OrderNumber { get; set; }

    /// <summary>
    /// تاريخ الأمر الإداري
    /// </summary>
    public DateOnly? OrderDate { get; set; }

    /// <summary>
    /// سبب التعديل (مثل: ترقية، إضافة مخصصات منصب، زواج، أمر وزاري...)
    /// </summary>
    [MaxLength(300)]
    public string? ChangeReason { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    public ICollection<EmployeeAllowanceItem> Items { get; set; } = new List<EmployeeAllowanceItem>();
}
