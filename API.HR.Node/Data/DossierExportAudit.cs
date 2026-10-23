using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Data;

[Index(nameof(VerificationToken), IsUnique = true)]
[Index(nameof(EmployeeIDNumber))]
[Index(nameof(CreatedAtUtc))]
public sealed class DossierExportAudit
{
    public long Id { get; set; }

    [Required]
    [MaxLength(32)]
    public string VerificationToken { get; set; } = string.Empty;

    public int EmployeeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string EmployeeIDNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string EmployeeName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? FormationName { get; set; }

    [MaxLength(256)]
    public string? JobTitleName { get; set; }

    /// <summary>
    /// SHA-256 digest of key employee properties at the exact moment of document export.
    /// Used upon scanning to detect if the printed paper matches current system records
    /// or if employee data has been modified since the export date.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string SnapshotDataDigest { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ExportedByUserId { get; set; }

    [MaxLength(256)]
    public string? ExportedByUserName { get; set; }

    [MaxLength(64)]
    public string? ExportedFromIp { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public int VerificationCount { get; set; } = 0;

    public DateTime? LastVerifiedAtUtc { get; set; }
}
