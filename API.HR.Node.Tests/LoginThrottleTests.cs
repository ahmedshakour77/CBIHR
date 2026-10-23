using API.HR.Node.Services.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class LoginThrottleTests
{
    private static LoginThrottle Create(int maxFailures = 3) => new(
        new MemoryCache(new MemoryCacheOptions()),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LoginThrottle:MaxFailures"] = maxFailures.ToString(),
            ["LoginThrottle:WindowMinutes"] = "15"
        }).Build());

    [Theory]
    [InlineData("Ali", "ali")]
    [InlineData(" ali@aml.lan ", "ali")]
    [InlineData("AML\\Ali", "ali")]
    [InlineData("ali@hr.node", "ali")]
    public void OneAccountWhicheverWayItIsTyped(string typed, string expected) =>
        Assert.Equal(expected, LoginThrottle.Normalize(typed));

    [Fact]
    public void BlocksOnceTheLimitIsReachedAndCountsAcrossSpellings()
    {
        var throttle = Create(maxFailures: 3);

        Assert.Equal(2, throttle.RecordFailure("ali"));
        Assert.Equal(1, throttle.RecordFailure("ALI@aml.lan"));
        Assert.False(throttle.IsBlocked("ali", out _));

        Assert.Equal(0, throttle.RecordFailure("aml\\ali"));
        Assert.True(throttle.IsBlocked("Ali", out var retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero && retryAfter <= TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void ASuccessfulSignInClearsTheCount()
    {
        var throttle = Create(maxFailures: 2);
        throttle.RecordFailure("sara");
        throttle.RecordFailure("sara");
        Assert.True(throttle.IsBlocked("sara", out _));

        throttle.Reset("sara@aml.lan");

        Assert.False(throttle.IsBlocked("sara", out _));
    }

    [Fact]
    public void AnotherAccountIsNotAffected()
    {
        var throttle = Create(maxFailures: 1);
        throttle.RecordFailure("ali");
        Assert.False(throttle.IsBlocked("sara", out _));
    }
}
