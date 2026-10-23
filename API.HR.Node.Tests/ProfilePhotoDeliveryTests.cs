using System.Security.Claims;
using API.HR.Node.Endpoints;
using API.HR.Node.Services.ProfilePhotos;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace API.HR.Node.Tests;

/// <summary>
/// Pages load faces from the photo endpoint by URL instead of embedding base64,
/// so the endpoint's read rule, cache policy and the URL builder are what keep
/// photos visible, fresh and audited.
/// </summary>
public sealed class ProfilePhotoDeliveryTests
{
    private static ClaimsPrincipal UserInRole(string role) =>
        new(new ClaimsIdentity([
            new Claim(ClaimTypes.Name, "staff"),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));

    [Theory]
    [InlineData("Admin")]
    [InlineData("HRManager")]
    [InlineData("FormationManager")]
    [InlineData("Supervisor")]
    public async Task CanAccessPhotoRead_RolesWhosePagesShowPhotos_AreAdmitted(string role)
    {
        var httpContext = new DefaultHttpContext { User = UserInRole(role) };

        // These roles are decided before the user store is consulted.
        var allowed = await EmployeeProfilePhotoEndpoints.CanAccessPhotoReadAsync("EMP100", httpContext, null!);

        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessPhotoRead_Anonymous_IsRefused()
    {
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

        var allowed = await EmployeeProfilePhotoEndpoints.CanAccessPhotoReadAsync("EMP100", httpContext, null!);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void PhotoResponse_RevalidatesEveryUse()
    {
        // Versions restart at 1 after a delete, so a URL can come back pointing
        // at a different face; only revalidation keeps it from going stale.
        var httpContext = new DefaultHttpContext();
        var data = new PhotoServiceResult.PhotoStreamData(
            new MemoryStream([1, 2, 3]), "image/jpeg", "\"abc-v1-thumb\"", DateTimeOffset.UtcNow, 3);

        EmployeeProfilePhotoEndpoints.CreateFileStreamResult(httpContext, data);

        httpContext.Response.Headers.CacheControl.ToString().Should().Be("private, no-cache");
        httpContext.Response.Headers.ETag.ToString().Should().Be("\"abc-v1-thumb\"");
    }

    [Fact]
    public void NotModified_RepeatsTheCachePolicy()
    {
        var httpContext = new DefaultHttpContext();

        EmployeeProfilePhotoEndpoints.NotModified(httpContext);

        httpContext.Response.Headers.CacheControl.ToString().Should().Be("private, no-cache");
    }

    [Fact]
    public void Urls_AreRelative_Versioned_AndEscaped()
    {
        ProfilePhotoUrls.Thumbnail("EMP 7/A", 3)
            .Should().Be("api/employees/EMP%207%2FA/profile-photo/thumbnail?v=3");

        ProfilePhotoUrls.Original("EMP100", null)
            .Should().Be("api/employees/EMP100/profile-photo?v=1");
    }
}
