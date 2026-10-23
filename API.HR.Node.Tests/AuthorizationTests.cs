using System.Security.Claims;
using API.HR.Node.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class AuthorizationTests
{
    private readonly PermissionAuthorizationHandler _handler = new();

    [Fact]
    public async Task HandleAsync_UnauthenticatedUser_ShouldNotSucceed()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // Unauthenticated
        var requirement = new PermissionRequirement(Permissions.ProfilePhotoRead);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_AuthenticatedUserWithExplicitPermission_ShouldSucceed()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.ProfilePhotoUpload));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.ProfilePhotoUpload);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_AuthenticatedUserWithoutPermissionClaims_ShouldDenyAccess()
    {
        var identity = new ClaimsIdentity("TestAuth"); // No permission claims attached
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.ProfilePhotoRead);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_AdminUserRole_ShouldSucceed()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.ManageUsers);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_AuthenticatedUserWithDifferentPermissionClaim_ShouldFail()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.ProfilePhotoRead)); // User has Read, NOT Delete
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.ProfilePhotoDelete);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
