using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace API.HR.Node.Services.Security;

public sealed class PasskeyChallengeStore(
    IMemoryCache memoryCache,
    ILogger<PasskeyChallengeStore> logger) : IPasskeyChallengeStore
{
    private const string CacheKeyPrefix = "passkey_challenge_";
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(2);

    public Task<string> CreateChallengeAsync(
        string challengeType,
        string optionsJson,
        string? userId = null,
        string? userIdentifier = null,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var flowId = GenerateSecureFlowId();
        var effectiveExpiry = expiry ?? DefaultExpiry;
        var now = DateTimeOffset.UtcNow;

        var stored = new StoredPasskeyChallenge(
            FlowId: flowId,
            ChallengeType: challengeType,
            OptionsJson: optionsJson,
            UserId: userId,
            UserIdentifier: userIdentifier,
            CreatedAt: now,
            ExpiresAt: now.Add(effectiveExpiry));

        var cacheKey = CacheKeyPrefix + flowId;
        memoryCache.Set(cacheKey, stored, effectiveExpiry);

        logger.LogInformation("Generated passkey challenge flow '{FlowId}' of type '{Type}' valid for {DurationSeconds}s",
            flowId, challengeType, effectiveExpiry.TotalSeconds);

        return Task.FromResult(flowId);
    }

    public Task<string> ConsumeChallengeAsync(
        string flowId,
        string expectedChallengeType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(flowId))
        {
            throw new InvalidOperationException("PASSKEY_FLOW_REQUIRED: Challenge FlowId is required.");
        }

        var cacheKey = CacheKeyPrefix + flowId.Trim();
        if (!memoryCache.TryGetValue<StoredPasskeyChallenge>(cacheKey, out var stored) || stored == null)
        {
            logger.LogWarning("Passkey challenge flow '{FlowId}' not found or already expired.", flowId);
            throw new InvalidOperationException("PASSKEY_CHALLENGE_EXPIRED: The WebAuthn challenge has expired or does not exist. Please restart authentication.");
        }

        if (stored.IsConsumed)
        {
            memoryCache.Remove(cacheKey);
            logger.LogWarning("Passkey challenge flow '{FlowId}' was already consumed (potential replay attack).", flowId);
            throw new InvalidOperationException("PASSKEY_CHALLENGE_ALREADY_USED: This authentication challenge has already been consumed. Replay is forbidden.");
        }

        if (DateTimeOffset.UtcNow > stored.ExpiresAt)
        {
            memoryCache.Remove(cacheKey);
            logger.LogWarning("Passkey challenge flow '{FlowId}' has expired at {Expiry}.", flowId, stored.ExpiresAt);
            throw new InvalidOperationException("PASSKEY_CHALLENGE_EXPIRED: The WebAuthn challenge has expired. Please restart authentication.");
        }

        if (!string.Equals(stored.ChallengeType, expectedChallengeType, StringComparison.OrdinalIgnoreCase))
        {
            memoryCache.Remove(cacheKey);
            logger.LogWarning("Passkey challenge flow '{FlowId}' expected type '{Expected}' but got '{Actual}'.",
                flowId, expectedChallengeType, stored.ChallengeType);
            throw new InvalidOperationException("PASSKEY_CHALLENGE_TYPE_MISMATCH: The challenge type does not match the attempted operation.");
        }

        // Consume immediately for single-use protection
        stored.IsConsumed = true;
        memoryCache.Remove(cacheKey);

        logger.LogInformation("Successfully consumed passkey challenge flow '{FlowId}' for operation '{Type}'.", flowId, expectedChallengeType);
        return Task.FromResult(stored.OptionsJson);
    }

    private static string GenerateSecureFlowId()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
