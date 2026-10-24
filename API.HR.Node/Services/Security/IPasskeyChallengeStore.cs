namespace API.HR.Node.Services.Security;

public sealed record StoredPasskeyChallenge(
    string FlowId,
    string ChallengeType,
    string OptionsJson,
    string? UserId,
    string? UserIdentifier,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    public bool IsConsumed { get; set; }
}

public interface IPasskeyChallengeStore
{
    /// <summary>
    /// Stores a generated WebAuthn options/challenge server-side, returning a unique FlowId.
    /// </summary>
    Task<string> CreateChallengeAsync(
        string challengeType,
        string optionsJson,
        string? userId = null,
        string? userIdentifier = null,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and consumes the server-side challenge for one-time use, returning the stored OptionsJson.
    /// Throws InvalidOperationException if expired, consumed, or not found.
    /// </summary>
    Task<string> ConsumeChallengeAsync(
        string flowId,
        string expectedChallengeType,
        CancellationToken cancellationToken = default);
}
