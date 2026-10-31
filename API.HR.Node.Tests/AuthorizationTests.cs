using System.Security.Claims;
using API.HR.Node.Components.Layout;
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

    [Fact]
    public async Task HandleAsync_UserWithEditPermission_ShouldAutomaticallySatisfyViewRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.EmployeesEdit)); // Edit permission
        var principal = new ClaimsPrincipal(identity);

        // Requirement is View permission
        var requirement = new PermissionRequirement(Permissions.EmployeesView);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("having X.Edit automatically grants X.View");
    }

    [Fact]
    public async Task HandleAsync_UserWithViewPermission_ShouldNOTSatisfyEditRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.EmployeesView)); // View only
        var principal = new ClaimsPrincipal(identity);

        // Requirement is Edit permission
        var requirement = new PermissionRequirement(Permissions.EmployeesEdit);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("having X.View must NOT grant X.Edit");
    }

    [Fact]
    public async Task HandleAsync_UserWithNonAdminRoleAndNoPermissionClaim_ShouldBeDenied()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.Role, "Supervisor")); // Role with no direct claim attached to principal
        var principal = new ClaimsPrincipal(identity);

        // Requirement is Users.Edit
        var requirement = new PermissionRequirement(Permissions.UsersEdit);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("roles without permission claims cannot satisfy permission requirement unless Admin");
    }

    [Fact]
    public void FeaturePermissionCatalog_CanView_ShouldReturnTrue_WhenUserHasEditPermission()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.LeavesEdit));
        var principal = new ClaimsPrincipal(identity);

        principal.CanView(Permissions.LeavesView).Should().BeTrue();
    }

    [Fact]
    public void FeaturePermissionCatalog_CalculateEffectivePermissions_Admin_HasAllPermissionsEffective()
    {
        var effective = FeaturePermissionCatalog.CalculateEffectivePermissions(["Admin"], []);

        effective.Should().NotBeEmpty();
        effective.All(p => p.IsEffective).Should().BeTrue("Admin role must have every permission effective");
    }

    [Fact]
    public void FeaturePermissionCatalog_CalculateEffectivePermissions_RoleAndDirectClaims_MergedProperly()
    {
        // HRManager role gives EmployeesView, EmployeesEdit, LeavesView, etc.
        // Direct claim gives AuditLogsView
        var effective = FeaturePermissionCatalog.CalculateEffectivePermissions(
            ["HRManager"],
            [Permissions.AuditLogsView])
            .ToDictionary(p => p.PermissionCode, StringComparer.OrdinalIgnoreCase);

        effective[Permissions.EmployeesView].IsEffective.Should().BeTrue();
        effective[Permissions.EmployeesEdit].IsEffective.Should().BeTrue();
        effective[Permissions.AuditLogsView].IsEffective.Should().BeTrue();
        effective[Permissions.AuditLogsView].GrantedDirectly.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_UserWithUsersViewOnly_CannotSatisfyUsersEditRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.UsersView));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.UsersEdit);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("Users.View must never satisfy Users.Edit requirement");
    }

    [Fact]
    public async Task HandleAsync_UserWithUsersEdit_SatisfiesUsersEditRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.UsersEdit));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.UsersEdit);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("Users.Edit satisfies Users.Edit requirement");
    }

    [Fact]
    public async Task HandleAsync_UserWithDevicesViewOnly_CannotSatisfyDevicesEditRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.DevicesView));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.DevicesEdit);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("Devices.View only must DENY mutations requiring Devices.Edit");
    }

    [Fact]
    public async Task HandleAsync_UserWithDevicesEdit_SatisfiesDevicesEditRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.DevicesEdit));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.DevicesEdit);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("Devices.Edit must ALLOW mutations requiring Devices.Edit");
    }

    [Fact]
    public async Task HandleAsync_UserWithDevicesEdit_SatisfiesDevicesViewRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.DevicesEdit));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.DevicesView);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("Devices.Edit implies Devices.View");
    }

    [Fact]
    public async Task HandleAsync_UserWithFeedbackViewOnly_CannotSatisfyFeedbackEditRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.FeedbackView));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.FeedbackEdit);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("Feedback.View only must DENY mutations requiring Feedback.Edit");
    }

    [Fact]
    public async Task HandleAsync_UserWithFeedbackEdit_SatisfiesFeedbackEditRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.FeedbackEdit));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.FeedbackEdit);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("Feedback.Edit must ALLOW operations requiring Feedback.Edit");
    }

    [Fact]
    public async Task HandleAsync_UserWithFeedbackEdit_SatisfiesFeedbackViewRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.FeedbackEdit));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.FeedbackView);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("Feedback.Edit implies Feedback.View");
    }

    [Fact]
    public async Task HandleAsync_UserWithUsersEditOnly_CannotSatisfyUsersPermissionsEditRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.UsersEdit));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.UsersPermissionsEdit);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("Users.Edit lifecycle permission must NOT satisfy Users.PermissionsEdit");
    }

    [Fact]
    public async Task HandleAsync_UserWithUsersPermissionsEdit_SatisfiesUsersPermissionsEditRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.UsersPermissionsEdit));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.UsersPermissionsEdit);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("Users.PermissionsEdit satisfies Users.PermissionsEdit requirement");
    }

    [Fact]
    public async Task HandleAsync_UserWithUsersPermissionsEdit_SatisfiesUsersViewRequirement()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.UsersPermissionsEdit));
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement(Permissions.UsersView);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("Users.PermissionsEdit implies Users.View");
    }

    [Fact]
    public void FeaturePermissionCatalog_UsersPermissionsEdit_RegisteredAndExcludedFromStandardHrManagerPackage()
    {
        FeaturePermissionCatalog.AllPermissionCodes.Should().Contain(Permissions.UsersPermissionsEdit);
        FeaturePermissionCatalog.RolePermissionPackages["HRManager"].Should().NotContain(Permissions.UsersPermissionsEdit,
            "Users.PermissionsEdit is sensitive and must not be part of the default HRManager package");
    }

    [Fact]
    public void NavCatalog_AllMenuPermissions_MustExistInFeaturePermissionCatalog()
    {
        var allPerms = FeaturePermissionCatalog.AllPermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var navPermissions = NavCatalog.Entries
            .Where(item => !string.IsNullOrWhiteSpace(item.ViewPermission))
            .Select(item => item.ViewPermission!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        navPermissions.Should().NotBeEmpty();
        foreach (var perm in navPermissions)
        {
            allPerms.Should().Contain(perm, $"Navigation menu permission '{perm}' must be a registered permission in FeaturePermissionCatalog");
        }
    }
}

