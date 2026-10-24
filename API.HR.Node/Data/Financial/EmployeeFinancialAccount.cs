using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data.Financial;

public sealed class EmployeeFinancialAccount
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>
    /// الرصيد المالي الحالي للموظف
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0m;

    /// <summary>
    /// تاريخ ووقت آخر تحديث للرصيد
    /// </summary>
    public DateTime? BalanceLastUpdatedAt { get; set; }

    /// <summary>
    /// متبقي القرض على الموظف
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal LoanRemaining { get; set; } = 0m;

    /// <summary>
    /// تاريخ ووقت آخر تحديث لمتبقي القرض
    /// </summary>
    public DateTime? LoanLastUpdatedAt { get; set; }

    /// <summary>
    /// متبقي السلفة على الموظف
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AdvanceRemaining { get; set; } = 0m;

    /// <summary>
    /// تاريخ ووقت آخر تحديث لمتبقي السلفة
    /// </summary>
    public DateTime? AdvanceLastUpdatedAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(150)]
    public string? LastUpdatedBy { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<EmployeeBalanceTransaction> Transactions { get; set; } = new List<EmployeeBalanceTransaction>();
}
