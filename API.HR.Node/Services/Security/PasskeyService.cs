using System.Text;
using API.HR.Node.Contracts;
using API.HR.Node.Data;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Security;

public sealed class PasskeyService : IPasskeyService
{
    private readonly Fido2Configuration _baseConfig;
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<PasskeyService> _logger;

    public PasskeyService(
        Fido2Configuration baseConfig,
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<PasskeyService> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _baseConfig = baseConfig;
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    private IFido2 GetFido2(string? customHost = null, string? customOrigin = null)
    {
        var http = _httpContextAccessor?.HttpContext;
        var host = customHost ?? http?.Request.Host.Host;

        string domain = _baseConfig.ServerDomain ?? "portal.aml.lan";
        if (!string.IsNullOrWhiteSpace(host))
        {
            var cleanHost = host.Trim();
            if (cleanHost.Contains(':'))
            {
                cleanHost = cleanHost.Split(':')[0];
            }

            if (string.Equals(cleanHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanHost, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                domain = "localhost";
            }
            else
            {
                domain = cleanHost;
            }
        }

        var origins = _baseConfig.Origins != null
            ? new HashSet<string>(_baseConfig.Origins, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(customOrigin))
        {
            origins.Add(customOrigin.TrimEnd('/'));
        }
        if (http != null)
        {
            origins.Add($"{http.Request.Scheme}://{http.Request.Host.Value}");
        }

        // Always allow portal.aml.lan, aml.lan, localhost and 127.0.0.1 origins
        origins.Add("https://portal.aml.lan");
        origins.Add("http://portal.aml.lan");
        origins.Add("https://portal.aml.lan:7266");
        origins.Add("https://aml.lan");
        origins.Add("http://aml.lan");
        origins.Add("https://apihr.iraqservice.uk");
        origins.Add("https://localhost:7266");
        origins.Add("http://localhost:5058");
        origins.Add("https://localhost:5058");
        origins.Add("http://localhost");
        origins.Add("https://localhost");
        origins.Add("https://127.0.0.1:7266");
        origins.Add("http://127.0.0.1:5058");
        origins.Add("https://127.0.0.1:5058");
        origins.Add("http://127.0.0.1");
        origins.Add("https://127.0.0.1");

        var cfg = new Fido2Configuration
        {
            ServerDomain = domain,
            ServerName = _baseConfig.ServerName ?? "API.HR.Node Passkeys",
            Origins = origins,
            TimestampDriftTolerance = _baseConfig.TimestampDriftTolerance > 0 ? _baseConfig.TimestampDriftTolerance : 300000
        };

        return new Fido2(cfg);
    }

    public Task<CredentialCreateOptions> MakeRegistrationOptionsAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
        => MakeRegistrationOptionsAsync(user, host: null, cancellationToken);

    public async Task<CredentialCreateOptions> MakeRegistrationOptionsAsync(
        ApplicationUser user,
        string? host,
        CancellationToken cancellationToken = default)
    {
        var fidoUser = new Fido2User
        {
            Id = Encoding.UTF8.GetBytes(user.Id),
            Name = user.Email ?? user.UserName ?? user.Id,
            DisplayName = user.UserName ?? user.Email ?? user.Id
        };

        var existingKeys = await _dbContext.EmployeePasskeys
            .Where(p => p.UserId == user.Id && p.IsActive)
            .Select(p => new PublicKeyCredentialDescriptor(p.CredentialId))
            .ToListAsync(cancellationToken);

        var authenticatorSelection = new AuthenticatorSelection
        {
            ResidentKey = ResidentKeyRequirement.Preferred,
            UserVerification = UserVerificationRequirement.Preferred
        };

        var fido = GetFido2(host);
        var options = fido.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fidoUser,
            ExcludeCredentials = existingKeys,
            AuthenticatorSelection = authenticatorSelection,
            AttestationPreference = AttestationConveyancePreference.None
        });

        return options;
    }

    public Task<EmployeePasskey> RegisterCompleteAsync(
        ApplicationUser user,
        CredentialCreateOptions options,
        AuthenticatorAttestationRawResponse attestationResponse,
        string? deviceName,
        CancellationToken cancellationToken = default)
        => RegisterCompleteAsync(user, options, attestationResponse, deviceName, host: null, origin: null, cancellationToken);

    public async Task<EmployeePasskey> RegisterCompleteAsync(
        ApplicationUser user,
        CredentialCreateOptions options,
        AuthenticatorAttestationRawResponse attestationResponse,
        string? deviceName,
        string? host,
        string? origin,
        CancellationToken cancellationToken = default)
    {
        IsCredentialIdUniqueToUserAsyncDelegate isUnique = async (paramsArgs, ct) =>
        {
            var passkeys = await _dbContext.EmployeePasskeys.ToListAsync(ct);
            var exists = passkeys.Any(p => p.CredentialId.SequenceEqual(paramsArgs.CredentialId));
            return !exists;
        };

        var fido = GetFido2(host, origin);
        var success = await fido.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestationResponse,
            OriginalOptions = options,
            IsCredentialIdUniqueToUserCallback = isUnique
        }, cancellationToken);

        var passkey = new EmployeePasskey
        {
            UserId = user.Id,
            EmployeeID = user.EmployeeID!,
            CredentialId = success.Id,
            PublicKey = success.PublicKey,
            UserHandle = success.User.Id,
            SignatureCounter = success.SignCount,
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Web Browser (Windows Hello / Touch ID)" : deviceName.Trim(),
            AaGuid = success.AaGuid,
            CredentialType = success.Type.ToString(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow
        };

        _dbContext.EmployeePasskeys.Add(passkey);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Passkey registered for employee {EmployeeID} (User: {UserId}).", user.EmployeeID, user.Id);

        return passkey;
    }

    public Task<AssertionOptions> MakeLoginOptionsAsync(
        string? emailOrUsername,
        CancellationToken cancellationToken = default)
        => MakeLoginOptionsAsync(emailOrUsername, host: null, cancellationToken);

    public async Task<AssertionOptions> MakeLoginOptionsAsync(
        string? emailOrUsername,
        string? host,
        CancellationToken cancellationToken = default)
    {
        List<PublicKeyCredentialDescriptor> allowedCredentials = [];

        if (!string.IsNullOrWhiteSpace(emailOrUsername))
        {
            var input = emailOrUsername.Trim();
            var user = await _userManager.FindByEmailAsync(input)
                ?? await _userManager.FindByNameAsync(input);

            if (user != null)
            {
                allowedCredentials = await _dbContext.EmployeePasskeys
                    .Where(p => p.UserId == user.Id && p.IsActive)
                    .Select(p => new PublicKeyCredentialDescriptor(p.CredentialId))
                    .ToListAsync(cancellationToken);
            }
        }

        var fido = GetFido2(host);
        var options = fido.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedCredentials,
            UserVerification = UserVerificationRequirement.Required
        });

        return options;
    }

    public Task<(EmployeePasskey Passkey, ApplicationUser User)> LoginCompleteAsync(
        AssertionOptions options,
        AuthenticatorAssertionRawResponse assertionResponse,
        CancellationToken cancellationToken = default)
        => LoginCompleteAsync(options, assertionResponse, host: null, origin: null, cancellationToken);

    public async Task<(EmployeePasskey Passkey, ApplicationUser User)> LoginCompleteAsync(
        AssertionOptions options,
        AuthenticatorAssertionRawResponse assertionResponse,
        string? host,
        string? origin,
        CancellationToken cancellationToken = default)
    {
        var passkeys = await _dbContext.EmployeePasskeys
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        var passkey = passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(assertionResponse.RawId));

        if (passkey is null)
        {
            throw new Fido2VerificationException("Registered passkey credential not found or deactivated.");
        }

        var user = await _userManager.FindByIdAsync(passkey.UserId);
        if (user is null || string.IsNullOrWhiteSpace(user.EmployeeID))
        {
            throw new Fido2VerificationException("Employee user account not found or unlinked.");
        }

        IsUserHandleOwnerOfCredentialIdAsync isUserHandleOwner = async (args, ct) =>
        {
            var all = await _dbContext.EmployeePasskeys.ToListAsync(ct);
            var stored = all.FirstOrDefault(p => p.CredentialId.SequenceEqual(args.CredentialId));
            return stored != null && stored.UserHandle.SequenceEqual(args.UserHandle.ToArray());
        };

        var fido = GetFido2(host, origin);
        var res = await fido.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = assertionResponse,
            OriginalOptions = options,
            StoredPublicKey = passkey.PublicKey,
            StoredSignatureCounter = passkey.SignatureCounter,
            IsUserHandleOwnerOfCredentialIdCallback = isUserHandleOwner
        }, cancellationToken);

        passkey.SignatureCounter = res.SignCount;
        passkey.LastUsedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Passkey authentication successful for employee {EmployeeID}.", passkey.EmployeeID);

        return (passkey, user);
    }

    public async Task<List<UserPasskeyDto>> GetUserPasskeysAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeePasskeys
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.LastUsedAt)
            .Select(p => new UserPasskeyDto(
                p.Id,
                p.EmployeeID,
                p.DeviceName,
                p.IsActive,
                p.CreatedAt,
                p.LastUsedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokePasskeyAsync(
        long passkeyId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var passkey = await _dbContext.EmployeePasskeys
            .FirstOrDefaultAsync(p => p.Id == passkeyId && (p.UserId == userId || userId == "admin"), cancellationToken);

        if (passkey is null) return false;

        passkey.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnfreezePasskeyAsync(
        long passkeyId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var passkey = await _dbContext.EmployeePasskeys
            .FirstOrDefaultAsync(p => p.Id == passkeyId && (p.UserId == userId || userId == "admin"), cancellationToken);

        if (passkey is null) return false;

        passkey.IsActive = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeletePasskeyPermanentlyAsync(
        long passkeyId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var passkey = await _dbContext.EmployeePasskeys
            .FirstOrDefaultAsync(p => p.Id == passkeyId && (p.UserId == userId || userId == "admin"), cancellationToken);

        if (passkey is null) return false;

        _dbContext.EmployeePasskeys.Remove(passkey);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
