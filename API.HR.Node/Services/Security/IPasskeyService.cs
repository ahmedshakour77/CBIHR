using Fido2NetLib;
using API.HR.Node.Data;
using API.HR.Node.Contracts;

namespace API.HR.Node.Services.Security;

public interface IPasskeyService
{
    Task<CredentialCreateOptions> MakeRegistrationOptionsAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default);

    Task<CredentialCreateOptions> MakeRegistrationOptionsAsync(
        ApplicationUser user,
        string? host,
        CancellationToken cancellationToken = default);

    Task<EmployeePasskey> RegisterCompleteAsync(
        ApplicationUser user,
        CredentialCreateOptions options,
        AuthenticatorAttestationRawResponse attestationResponse,
        string? deviceName,
        CancellationToken cancellationToken = default);

    Task<EmployeePasskey> RegisterCompleteAsync(
        ApplicationUser user,
        CredentialCreateOptions options,
        AuthenticatorAttestationRawResponse attestationResponse,
        string? deviceName,
        string? host,
        string? origin,
        CancellationToken cancellationToken = default);

    Task<AssertionOptions> MakeLoginOptionsAsync(
        string? emailOrUsername,
        CancellationToken cancellationToken = default);

    Task<AssertionOptions> MakeLoginOptionsAsync(
        string? emailOrUsername,
        string? host,
        CancellationToken cancellationToken = default);

    Task<(EmployeePasskey Passkey, ApplicationUser User)> LoginCompleteAsync(
        AssertionOptions options,
        AuthenticatorAssertionRawResponse assertionResponse,
        CancellationToken cancellationToken = default);

    Task<(EmployeePasskey Passkey, ApplicationUser User)> LoginCompleteAsync(
        AssertionOptions options,
        AuthenticatorAssertionRawResponse assertionResponse,
        string? host,
        string? origin,
        CancellationToken cancellationToken = default);

    Task<List<UserPasskeyDto>> GetUserPasskeysAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> RevokePasskeyAsync(
        long passkeyId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> UnfreezePasskeyAsync(
        long passkeyId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeletePasskeyPermanentlyAsync(
        long passkeyId,
        string userId,
        CancellationToken cancellationToken = default);
}
