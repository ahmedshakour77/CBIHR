using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

/// <summary>
/// Entity storing FIDO2 / WebAuthn Passkey credentials bound to an employee account.
/// </summary>
public sealed class EmployeePasskey
{
    public long Id { get; set; }

    [MaxLength(256)]
    public required string UserId { get; set; }

    [MaxLength(100)]
    public required string EmployeeID { get; set; }

    public required byte[] CredentialId { get; set; }

    public required byte[] PublicKey { get; set; }

    public required byte[] UserHandle { get; set; }

    public uint SignatureCounter { get; set; }

    [MaxLength(150)]
    public string? DeviceName { get; set; }

    public Guid AaGuid { get; set; }

    [MaxLength(100)]
    public string? CredentialType { get; set; } = "public-key";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;
}
