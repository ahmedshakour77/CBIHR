using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

public class EmployeeEmail
{
    public int Id { get; set; }

    [Required]
    public string EmployeeID { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    /// <summary>What this address is used for. Existing rows default to work email.</summary>
    public EmailLabel Label { get; set; } = EmailLabel.Work;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Employee? Employee { get; set; }
}
