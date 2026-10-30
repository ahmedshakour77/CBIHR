using System.Security.Claims;
using API.HR.Node.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.HR.Node.Security;

public sealed class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ApplicationUserClaimsPrincipalFactory> _logger;

    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor,
        ApplicationDbContext dbContext,
        ILogger<ApplicationUserClaimsPrincipalFactory> logger)
        : base(userManager, roleManager, optionsAccessor)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // 1. Synchronize / Auto-repair EmployeeID for unlinked human users
        if (!SystemAccounts.IsTechnicalAccount(user.UserName) && !SystemAccounts.IsTechnicalAccount(user.Email))
        {
            var isCurrentLinkedValid = !string.IsNullOrWhiteSpace(user.EmployeeID) &&
                await _dbContext.Employees.AsNoTracking().AnyAsync(e => e.EmployeeID == user.EmployeeID);

            if (!isCurrentLinkedValid && !string.IsNullOrWhiteSpace(user.Email))
            {
                var userEmail = user.Email.Trim().ToLower();
                var matchedEmpEmails = await _dbContext.EmployeeEmails
                    .AsNoTracking()
                    .Where(e => e.Email.Trim().ToLower() == userEmail)
                    .Select(e => e.EmployeeID)
                    .Distinct()
                    .ToListAsync();

                if (matchedEmpEmails.Count == 1)
                {
                    var matchedEmpId = matchedEmpEmails[0];
                    if (await _dbContext.Employees.AsNoTracking().AnyAsync(e => e.EmployeeID == matchedEmpId))
                    {
                        user.EmployeeID = matchedEmpId;
                        try
                        {
                            await UserManager.UpdateAsync(user);
                            _logger.LogInformation("Synced user {UserEmail} to EmployeeID {EmployeeID}", user.Email, user.EmployeeID);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to persist synced EmployeeID for user {UserEmail}", user.Email);
                        }
                    }
                }
            }
        }

        // 2. Check if user has any roles assigned. If not, default to "Employee" role
        var roles = await UserManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            // Ensure Employee role exists in database
            if (!await RoleManager.RoleExistsAsync("Employee"))
            {
                await RoleManager.CreateAsync(new IdentityRole("Employee"));
            }

            try
            {
                await UserManager.AddToRoleAsync(user, "Employee");
                _logger.LogInformation("Assigned default role 'Employee' to user {UserEmail}", user.Email ?? user.UserName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add user {UserEmail} to role 'Employee'", user.Email);
            }

            var roleClaimType = Options.ClaimsIdentity.RoleClaimType;
            if (!identity.HasClaim(c => c.Type == roleClaimType && c.Value == "Employee"))
            {
                identity.AddClaim(new Claim(roleClaimType, "Employee"));
            }
        }

        if (!string.IsNullOrWhiteSpace(user.EmployeeID))
        {
            identity.AddClaim(new Claim("employee_id", user.EmployeeID));
        }

        // Formation Linkage Claim: User's direct formation assignment or fallback to linked employee's formation
        int? effectiveFormationId = user.FormationId;
        string? effectiveFormationName = null;

        if (effectiveFormationId.HasValue)
        {
            var formation = await _dbContext.Formations.AsNoTracking().FirstOrDefaultAsync(f => f.Id == effectiveFormationId.Value);
            effectiveFormationName = formation?.Name;
        }
        else if (!string.IsNullOrWhiteSpace(user.EmployeeID))
        {
            var empFormation = await _dbContext.Employees
                .AsNoTracking()
                .Where(e => e.EmployeeID == user.EmployeeID)
                .Select(e => new { e.FormationId, FormationName = e.Formation != null ? e.Formation.Name : null })
                .FirstOrDefaultAsync();

            if (empFormation?.FormationId != null)
            {
                effectiveFormationId = empFormation.FormationId;
                effectiveFormationName = empFormation.FormationName;
            }
        }

        if (effectiveFormationId.HasValue)
        {
            identity.AddClaim(new Claim("formation_id", effectiveFormationId.Value.ToString()));
            if (!string.IsNullOrWhiteSpace(effectiveFormationName))
            {
                identity.AddClaim(new Claim("formation_name", effectiveFormationName));
            }
        }

        return identity;
    }
}
