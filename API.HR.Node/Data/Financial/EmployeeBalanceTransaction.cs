using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data.Financial;

public enum FinancialTransactionType
{
    Deposit = 1,     // إيداع
    Withdrawal = 2,  // سحب
    InitialBalance = 3, // رصيد افتتاحي
    Adjustment = 4   // تسوية رصيد
}

public sealed class EmployeeBalanceTransaction
{
    public int Id { get; set; }

    public int EmployeeFinancialAccountId { get; set; }
    public EmployeeFinancialAccount FinancialAccount { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public FinancialTransactionType TransactionType { get; set; }

    /// <summary>
    /// مبلغ الحركة
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// الرصيد قبل تنفيذ الحركة
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceBefore { get; set; }

    /// <summary>
    /// الرصيد بعد تنفيذ الحركة
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAfter { get; set; }

    /// <summary>
    /// تاريخ ووقت وقوع الحركة المالية
    /// </summary>
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// البيان أو وصف الحركة
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// رقم الإشعار أو الوصل أو المرجع
    /// </summary>
    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// مفتاح عدم التكرار (Idempotency Key) لمنع تكرار الحركة عند محاولات الإرسال المتعددة
    /// </summary>
    [MaxLength(128)]
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// منفذ الحركة
    /// </summary>
    [MaxLength(150)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
