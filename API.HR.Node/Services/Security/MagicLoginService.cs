using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using API.HR.Node.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using QRCoder;

namespace API.HR.Node.Services.Security;

public enum MagicLoginTokenStatus
{
    Active,
    Consumed,
    Expired,
    Revoked,
    Invalid
}

public sealed record MagicLoginTicket(
    string Token,
    string LoginUrl,
    string QrCodeDataUrl,
    string TargetUserId,
    string TargetUserName,
    string? TargetEmployeeName,
    string? TargetEmployeeId,
    IReadOnlyList<string> TargetRoles,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int DurationSeconds);

public sealed record MagicLoginTokenState(
    MagicLoginTokenStatus Status,
    string? TargetUserId,
    string? TargetUserName,
    string? TargetEmployeeName,
    string? TargetEmployeeId,
    IReadOnlyList<string> TargetRoles,
    string? CreatedByAdminName,
    DateTimeOffset? ExpiresAtUtc,
    int RemainingSeconds,
    DateTimeOffset? ConsumedAtUtc = null,
    string? ConsumedFromIp = null);

public interface IMagicLoginService
{
    MagicLoginTicket CreateMagicLink(
        ApplicationUser targetUser,
        string? employeeName,
        IReadOnlyList<string> roles,
        string baseUri,
        string adminUserId,
        string adminUserName);

    MagicLoginTokenState PeekToken(string? token);

    bool TryConsumeToken(string? token, string? clientIp, out MagicLoginValidationResult result);

    bool RevokeToken(string? token);
}

public sealed record MagicLoginValidationResult(
    bool Succeeded,
    MagicLoginTokenStatus Status,
    string? TargetUserId,
    string? TargetUserName,
    string? ExpectedSecurityStamp,
    string? CreatedByAdminId,
    string? CreatedByAdminName,
    string? ErrorMessage);

public sealed class MagicLoginService : IMagicLoginService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(2);
    private readonly ITimeLimitedDataProtector _protector;
    private readonly ILogger<MagicLoginService> _logger;
    private readonly ConcurrentDictionary<string, NonceRecord> _nonces = new(StringComparer.Ordinal);

    private sealed class NonceRecord
    {
        public required string Token { get; init; }
        public required string ProtectedPayload { get; init; }
        public required string TargetUserId { get; init; }
        public required string TargetUserName { get; init; }
        public string? TargetEmployeeName { get; init; }
        public string? TargetEmployeeId { get; init; }
        public required IReadOnlyList<string> TargetRoles { get; init; }
        public required string ExpectedSecurityStamp { get; init; }
        public required string CreatedByAdminId { get; init; }
        public required string CreatedByAdminName { get; init; }
        public required DateTimeOffset CreatedAtUtc { get; init; }
        public required DateTimeOffset ExpiresAtUtc { get; init; }

        // 0 = Active, 1 = Consumed, 2 = Revoked
        public int State;
        public DateTimeOffset? ConsumedAtUtc { get; set; }
        public string? ConsumedFromIp { get; set; }
    }

    private sealed record ProtectedPayloadModel(
        string Nonce,
        string UserId,
        string SecurityStamp,
        string AdminId,
        long ExpiresUnixSeconds);

    public MagicLoginService(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<MagicLoginService> logger)
    {
        _protector = dataProtectionProvider
            .CreateProtector("API.HR.Node.Security.MagicLogin.v1")
            .ToTimeLimitedDataProtector();
        _logger = logger;
    }

    public MagicLoginTicket CreateMagicLink(
        ApplicationUser targetUser,
        string? employeeName,
        IReadOnlyList<string> roles,
        string baseUri,
        string adminUserId,
        string adminUserName)
    {
        CleanupExpiredEntries();

        // Revoke any previously active magic link for the same user so only the latest one is active
        foreach (var kvp in _nonces)
        {
            if (string.Equals(kvp.Value.TargetUserId, targetUser.Id, StringComparison.OrdinalIgnoreCase) &&
                Volatile.Read(ref kvp.Value.State) == 0)
            {
                Interlocked.CompareExchange(ref kvp.Value.State, 2, 0);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(TokenLifetime);

        // 18 random bytes -> 24 URL-safe Base64 characters (144 bits of cryptographic entropy)
        var rawBytes = RandomNumberGenerator.GetBytes(18);
        var token = WebEncoders.Base64UrlEncode(rawBytes);

        var payload = new ProtectedPayloadModel(
            Nonce: token,
            UserId: targetUser.Id,
            SecurityStamp: targetUser.SecurityStamp ?? string.Empty,
            AdminId: adminUserId,
            ExpiresUnixSeconds: expiresAt.ToUnixTimeSeconds());

        var payloadJson = JsonSerializer.Serialize(payload);
        var protectedPayload = _protector.Protect(payloadJson, TokenLifetime);

        var record = new NonceRecord
        {
            Token = token,
            ProtectedPayload = protectedPayload,
            TargetUserId = targetUser.Id,
            TargetUserName = targetUser.UserName ?? targetUser.Email ?? targetUser.Id,
            TargetEmployeeName = employeeName,
            TargetEmployeeId = targetUser.EmployeeID,
            TargetRoles = roles.ToList(),
            ExpectedSecurityStamp = targetUser.SecurityStamp ?? string.Empty,
            CreatedByAdminId = adminUserId,
            CreatedByAdminName = adminUserName,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAt,
            State = 0
        };

        _nonces[token] = record;

        var cleanBase = baseUri.TrimEnd('/');
        var loginUrl = $"{cleanBase}/Account/MagicLogin?t={Uri.EscapeDataString(token)}";
        var qrDataUrl = GenerateQrCodeDataUrl(loginUrl);

        _logger.LogInformation(
            "Magic login link created by admin '{AdminName}' ({AdminId}) for user '{TargetUser}' ({TargetUserId}), expires at {ExpiresAtUtc:O}.",
            adminUserName, adminUserId, record.TargetUserName, targetUser.Id, expiresAt);

        return new MagicLoginTicket(
            Token: token,
            LoginUrl: loginUrl,
            QrCodeDataUrl: qrDataUrl,
            TargetUserId: targetUser.Id,
            TargetUserName: record.TargetUserName,
            TargetEmployeeName: employeeName,
            TargetEmployeeId: targetUser.EmployeeID,
            TargetRoles: record.TargetRoles,
            CreatedAtUtc: now,
            ExpiresAtUtc: expiresAt,
            DurationSeconds: (int)TokenLifetime.TotalSeconds);
    }

    public MagicLoginTokenState PeekToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !_nonces.TryGetValue(token.Trim(), out var record))
        {
            return new MagicLoginTokenState(
                MagicLoginTokenStatus.Invalid,
                null, null, null, null, [], null, null, 0);
        }

        var now = DateTimeOffset.UtcNow;
        var state = Volatile.Read(ref record.State);

        if (state == 1)
        {
            return new MagicLoginTokenState(
                MagicLoginTokenStatus.Consumed,
                record.TargetUserId,
                record.TargetUserName,
                record.TargetEmployeeName,
                record.TargetEmployeeId,
                record.TargetRoles,
                record.CreatedByAdminName,
                record.ExpiresAtUtc,
                0,
                record.ConsumedAtUtc,
                record.ConsumedFromIp);
        }

        if (state == 2)
        {
            return new MagicLoginTokenState(
                MagicLoginTokenStatus.Revoked,
                record.TargetUserId,
                record.TargetUserName,
                record.TargetEmployeeName,
                record.TargetEmployeeId,
                record.TargetRoles,
                record.CreatedByAdminName,
                record.ExpiresAtUtc,
                0);
        }

        var remaining = (int)Math.Ceiling((record.ExpiresAtUtc - now).TotalSeconds);
        if (remaining <= 0)
        {
            return new MagicLoginTokenState(
                MagicLoginTokenStatus.Expired,
                record.TargetUserId,
                record.TargetUserName,
                record.TargetEmployeeName,
                record.TargetEmployeeId,
                record.TargetRoles,
                record.CreatedByAdminName,
                record.ExpiresAtUtc,
                0);
        }

        return new MagicLoginTokenState(
            MagicLoginTokenStatus.Active,
            record.TargetUserId,
            record.TargetUserName,
            record.TargetEmployeeName,
            record.TargetEmployeeId,
            record.TargetRoles,
            record.CreatedByAdminName,
            record.ExpiresAtUtc,
            remaining);
    }

    public bool TryConsumeToken(string? token, string? clientIp, out MagicLoginValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(token) || !_nonces.TryGetValue(token.Trim(), out var record))
        {
            result = new MagicLoginValidationResult(
                false, MagicLoginTokenStatus.Invalid, null, null, null, null, null,
                "رابط الدخول غير صالح أو غير موجود.");
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (now >= record.ExpiresAtUtc)
        {
            result = new MagicLoginValidationResult(
                false, MagicLoginTokenStatus.Expired, record.TargetUserId, record.TargetUserName, null, record.CreatedByAdminId, record.CreatedByAdminName,
                "انتهت صلاحية رابط الدخول المؤقت (تجاوز مدة الدقيقتين المحددة).");
            return false;
        }

        // Verify cryptographic time-limited data protection envelope
        try
        {
            var unprotectedJson = _protector.Unprotect(record.ProtectedPayload, out var expiration);
            if (now >= expiration)
            {
                result = new MagicLoginValidationResult(
                    false, MagicLoginTokenStatus.Expired, record.TargetUserId, record.TargetUserName, null, record.CreatedByAdminId, record.CreatedByAdminName,
                    "انتهت صلاحية رابط الدخول المؤقت (تجاوز مدة الدقيقتين المحددة).");
                return false;
            }

            var payload = JsonSerializer.Deserialize<ProtectedPayloadModel>(unprotectedJson);
            if (payload is null ||
                !string.Equals(payload.Nonce, record.Token, StringComparison.Ordinal) ||
                !string.Equals(payload.UserId, record.TargetUserId, StringComparison.Ordinal))
            {
                result = new MagicLoginValidationResult(
                    false, MagicLoginTokenStatus.Invalid, null, null, null, null, null,
                    "فشل التحقق التشفيري من صحة الرابط.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cryptographic verification failed or expired for magic login token.");
            result = new MagicLoginValidationResult(
                false, MagicLoginTokenStatus.Expired, record.TargetUserId, record.TargetUserName, null, record.CreatedByAdminId, record.CreatedByAdminName,
                "انتهت صلاحية رابط الدخول المؤقت (تجاوز مدة الدقيقتين المحددة).");
            return false;
        }

        // Atomic transition from 0 (Active) -> 1 (Consumed) so it can NEVER be used twice
        var previousState = Interlocked.CompareExchange(ref record.State, 1, 0);
        if (previousState == 1)
        {
            result = new MagicLoginValidationResult(
                false, MagicLoginTokenStatus.Consumed, record.TargetUserId, record.TargetUserName, null, record.CreatedByAdminId, record.CreatedByAdminName,
                "تم استخدام رابط الدخول هذا مسبقاً (الرابط صالح للاستخدام لمرة واحدة فقط).");
            return false;
        }

        if (previousState == 2)
        {
            result = new MagicLoginValidationResult(
                false, MagicLoginTokenStatus.Revoked, record.TargetUserId, record.TargetUserName, null, record.CreatedByAdminId, record.CreatedByAdminName,
                "تم إلغاء رابط الدخول هذا من قبل مدير النظام.");
            return false;
        }

        record.ConsumedAtUtc = now;
        record.ConsumedFromIp = clientIp;

        result = new MagicLoginValidationResult(
            true,
            MagicLoginTokenStatus.Consumed,
            record.TargetUserId,
            record.TargetUserName,
            record.ExpectedSecurityStamp,
            record.CreatedByAdminId,
            record.CreatedByAdminName,
            null);
        return true;
    }

    public bool RevokeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !_nonces.TryGetValue(token.Trim(), out var record))
        {
            return false;
        }

        return Interlocked.CompareExchange(ref record.State, 2, 0) == 0;
    }

    private void CleanupExpiredEntries()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        foreach (var kvp in _nonces)
        {
            if (kvp.Value.ExpiresAtUtc < cutoff)
            {
                _nonces.TryRemove(kvp.Key, out _);
            }
        }
    }

    private static string GenerateQrCodeDataUrl(string url)
    {
        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var pngQr = new PngByteQRCode(qrData);
        var pngBytes = pngQr.GetGraphic(10);
        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
    }
}

