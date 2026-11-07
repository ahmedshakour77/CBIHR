using System.Security.Claims;
using API.HR.Node.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Security;

/// <summary>
/// Synchronizes the authenticated user's ClaimsPrincipal with the live database state
/// (Roles, UserClaims, RoleClaims, EmployeeID, FormationId) so permission changes made
/// in the Users management screen take effect immediately without requiring re-login.
/// </summary>
public sealed class DatabaseClaimsTransformation(IServiceScopeFactory scopeFactory) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true })
        {
            return principal;
        }

        await DatabaseClaimsSynchronizer.EnrichPrincipalFromDatabaseAsync(principal, scopeFactory);
        return principal;
    }
}

public static class DatabaseClaimsSynchronizer
{
    public const string CustomizedPermissionsClaimType = "permissions_customized";

    public static async Task EnrichPrincipalFromDatabaseAsync(
        ClaimsPrincipal principal,
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken = default)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? principal.FindFirstValue("sub");
            var userEmail = principal.FindFirstValue(ClaimTypes.Email)
                         ?? principal.Identity?.Name;

            ApplicationUser? user = null;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                user = await db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            }

            if (user is null && !string.IsNullOrWhiteSpace(userEmail))
            {
                var norm = userEmail.Trim().ToUpperInvariant();
                user = await db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.NormalizedEmail == norm || u.NormalizedUserName == norm, cancellationToken);
            }

            if (user is null)
            {
                return;
            }

            // 1. Load live roles from DB
            var dbRoles = await (from ur in db.UserRoles.AsNoTracking()
                                 join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                 where ur.UserId == user.Id && r.Name != null
                                 select r.Name!).ToListAsync(cancellationToken);

            if (dbRoles.Count == 0)
            {
                dbRoles.Add("Employee");
            }

            // 2. Load live direct user claims from DB
            var userClaims = await db.UserClaims.AsNoTracking()
                .Where(c => c.UserId == user.Id && c.ClaimType != null && c.ClaimValue != null)
                .ToListAsync(cancellationToken);

            var directPermissions = userClaims
                .Where(c => c.ClaimType!.Equals("permission", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.ClaimValue!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var isCustomized = userClaims.Any(c =>
                c.ClaimType!.Equals(CustomizedPermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
                c.ClaimValue == "true") || directPermissions.Count > 0;

            // 3. Calculate effective permissions
            var effectivePermissions = FeaturePermissionCatalog.CalculateEffectivePermissions(
                    dbRoles,
                    directPermissions,
                    ignoreRoleDefaults: isCustomized)
                .Where(p => p.IsEffective)
                .Select(p => p.PermissionCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Also keep any direct non-catalog legacy permission claims the user holds
            foreach (var dp in directPermissions)
            {
                effectivePermissions.Add(dp);
            }

            // 4. Sync Role claims on ClaimsIdentity
            var roleClaimType = identity.RoleClaimType;
            var existingRoleClaims = identity.FindAll(c => c.Type == roleClaimType || c.Type == ClaimTypes.Role || c.Type == "role").ToList();
            foreach (var rc in existingRoleClaims)
            {
                identity.TryRemoveClaim(rc);
            }
            foreach (var roleName in dbRoles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                identity.AddClaim(new Claim(roleClaimType, roleName));
                if (roleClaimType != ClaimTypes.Role)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                }
            }

            // 5. Sync Permission claims on ClaimsIdentity
            var existingPermClaims = identity.FindAll(c => c.Type.Equals("permission", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var pc in existingPermClaims)
            {
                identity.TryRemoveClaim(pc);
            }
            foreach (var perm in effectivePermissions)
            {
                identity.AddClaim(new Claim("permission", perm));
            }

            // 6. Ensure employee_id claim is present and up-to-date
            if (!string.IsNullOrWhiteSpace(user.EmployeeID))
            {
                var existingEmpClaims = identity.FindAll("employee_id").ToList();
                foreach (var ec in existingEmpClaims)
                {
                    identity.TryRemoveClaim(ec);
                }
                identity.AddClaim(new Claim("employee_id", user.EmployeeID));
            }

            // 7. Ensure formation_id & formation_name claims are up-to-date
            int? effectiveFormationId = user.FormationId;
            string? effectiveFormationName = null;

            if (effectiveFormationId.HasValue)
            {
                effectiveFormationName = await db.Formations.AsNoTracking()
                    .Where(f => f.Id == effectiveFormationId.Value)
                    .Select(f => f.Name)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(user.EmployeeID))
            {
                var empFormation = await db.Employees.AsNoTracking()
                    .Where(e => e.EmployeeID == user.EmployeeID)
                    .Select(e => new { e.FormationId, FormationName = e.Formation != null ? e.Formation.Name : null })
                    .FirstOrDefaultAsync(cancellationToken);

                if (empFormation?.FormationId != null)
                {
                    effectiveFormationId = empFormation.FormationId;
                    effectiveFormationName = empFormation.FormationName;
                }
            }

            var existingFormClaims = identity.FindAll(c => c.Type is "formation_id" or "formation_name").ToList();
            foreach (var fc in existingFormClaims)
            {
                identity.TryRemoveClaim(fc);
            }

            if (effectiveFormationId.HasValue)
            {
                identity.AddClaim(new Claim("formation_id", effectiveFormationId.Value.ToString()));
                if (!string.IsNullOrWhiteSpace(effectiveFormationName))
                {
                    identity.AddClaim(new Claim("formation_name", effectiveFormationName));
                }
            }
        }
        catch
        {
            // Never fail authentication state resolution if a transient DB error occurs
        }
    }
}

