using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

public class EmployeePhone
{
    public int Id { get; set; }

    [Required]
    public string EmployeeID { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    /// <summary>What this number is used for. Existing rows default to mobile.</summary>
    public PhoneLabel Label { get; set; } = PhoneLabel.Mobile;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Employee? Employee { get; set; }
}
