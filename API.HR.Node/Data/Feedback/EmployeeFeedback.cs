using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data.Feedback;

public enum FeedbackStatus
{
    Pending = 0,    // قيد المراجعة / بانتظار المراجعة
    InProgress = 1, // قيد المعالجة
    Resolved = 2,   // تمت المعالجة / معتمدة
    Rejected = 3    // مرفوض / محفوظ
}

[Table("EmployeeFeedbacks")]
public class EmployeeFeedback
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string ReferenceNumber { get; set; } = string.Empty;

    /// <summary>
    /// فكرة | ملاحظة | استفسار | أخرى
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string FeedbackType { get; set; } = "فكرة";

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public int WordCount { get; set; }

    // Linked employee details (if available)
    public int? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [MaxLength(250)]
    public string? SubmitterName { get; set; }

    [MaxLength(250)]
    public string? SubmitterEmail { get; set; }

    [MaxLength(100)]
    public string? SubmitterJobTitle { get; set; }

    [MaxLength(150)]
    public string? SubmitterDepartment { get; set; }

    // Supporting attachment details (optional, max 2MB)
    [MaxLength(300)]
    public string? AttachmentFileName { get; set; }

    [MaxLength(500)]
    public string? AttachmentStorageKey { get; set; }

    public long? AttachmentSizeBytes { get; set; }

    [MaxLength(100)]
    public string? AttachmentContentType { get; set; }

    // Status & Admin workflow
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Pending;

    [MaxLength(2000)]
    public string? AdminNotes { get; set; }

    [MaxLength(450)]
    public string? RespondedByUserId { get; set; }

    [MaxLength(250)]
    public string? RespondedByUserName { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}
