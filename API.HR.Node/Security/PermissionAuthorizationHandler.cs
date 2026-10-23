using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace API.HR.Node.Security;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity is not { IsAuthenticated: true })
        {
            return Task.CompletedTask;
        }

        // Admins bypass specific permission checks
        if (context.User.IsInRole("Admin") || context.User.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Strict Deny-by-Default: Check if user has explicit matching permission claim
        var hasPermission = context.User.HasClaim(c =>
            (c.Type == "permission" || c.Type == "Permission") &&
            c.Value.Equals(requirement.Permission, StringComparison.OrdinalIgnoreCase));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
