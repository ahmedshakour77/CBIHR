using Microsoft.Extensions.Caching.Memory;

namespace API.HR.Node.Services.Security;

/// <summary>
/// Counts failed sign-ins per account name on the login form, whichever path failed.
/// ASP.NET Identity's own lockout only sees the local password check, so a wrong domain
/// (aml.lan) password used to cost nothing here: it could be retried without limit, and
/// every retry also counted against the account in the directory, where enough of them
/// lock the person out of Windows as well. Once an account name reaches the limit inside
/// the window, the form stops checking it, without asking the directory, until the
/// window passes.
/// </summary>
/// <remarks>
/// Kept in this server's memory: a restart clears it, and in a multi-server deployment
/// each server counts on its own. Keep <c>LoginThrottle:MaxFailures</c> below the
/// directory's own lockout threshold, so this limit is reached first.
/// </remarks>
public sealed class LoginThrottle(IMemoryCache cache, IConfiguration configuration)
{
    private readonly int maxFailures = Math.Max(1, configuration.GetValue("LoginThrottle:MaxFailures", 5));
    private readonly TimeSpan window = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("LoginThrottle:WindowMinutes", 15)));

    private sealed class Counter
    {
        public int Failures;
        public DateTimeOffset WindowEnds;
    }

    private static string Key(string accountName) => "login-fail:" + Normalize(accountName);

    /// <summary>The account part of what was typed: "Ali", "ali@aml.lan" and "AML\ali" are one account.</summary>
    internal static string Normalize(string accountName)
    {
        var name = accountName.Trim().ToLowerInvariant();
        var slash = name.LastIndexOf('\\');
        if (slash >= 0) name = name[(slash + 1)..];
        var at = name.IndexOf('@');
        return at > 0 ? name[..at] : name;
    }

    /// <summary>True while this account name has used up its attempts; <paramref name="retryAfter"/> says for how long.</summary>
    public bool IsBlocked(string accountName, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!cache.TryGetValue(Key(accountName), out Counter? counter) || counter is null) return false;
        lock (counter)
        {
            if (counter.Failures < maxFailures) return false;
            retryAfter = counter.WindowEnds - DateTimeOffset.UtcNow;
            return retryAfter > TimeSpan.Zero;
        }
    }

    /// <summary>Records a failed attempt and returns how many remain before the account name is blocked.</summary>
    public int RecordFailure(string accountName)
    {
        var key = Key(accountName);
        var counter = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new Counter { WindowEnds = DateTimeOffset.UtcNow + window };
        })!;
        lock (counter)
        {
            counter.Failures++;
            return Math.Max(0, maxFailures - counter.Failures);
        }
    }

    public void Reset(string accountName) => cache.Remove(Key(accountName));
}
