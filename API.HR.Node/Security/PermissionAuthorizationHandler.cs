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
        if (PermissionResolver.HasPermission(context.User, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    public static bool IsLegacyPermissionMatch(string userClaim, string requestedPermission)
        => PermissionResolver.IsLegacyPermissionMatch(userClaim, requestedPermission);
}
