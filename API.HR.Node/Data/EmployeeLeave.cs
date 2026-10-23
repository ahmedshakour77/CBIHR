using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

/// <summary>
/// إجازة موظف (Leave / Vacation record)
/// </summary>
public sealed class EmployeeLeave
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int? LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public int DaysCount { get; set; }

    [MaxLength(100)]
    public string? OrderNumber { get; set; }

    public DateOnly? OrderDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "معتمدة";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// نوع الإجازة (Leave Type Lookup Entity)
/// </summary>
public sealed class LeaveType : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(200)] public required string Name { get; set; }
}
