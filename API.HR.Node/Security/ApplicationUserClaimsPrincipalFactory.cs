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

        // Fallback default role claim in-memory if user has no assigned roles
        var roleClaimType = Options.ClaimsIdentity.RoleClaimType;
        if (!identity.HasClaim(c => c.Type == roleClaimType))
        {
            identity.AddClaim(new Claim(roleClaimType, "Employee"));
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
