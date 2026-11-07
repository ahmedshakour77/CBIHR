using API.HR.Node.Data;
using API.HR.Node.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace API.HR.Node.Components.Account
{
    // This is a server-side AuthenticationStateProvider that revalidates the security stamp for the connected user
    // every 30 minutes an interactive circuit is connected, and synchronizes live permissions from the database.
    internal sealed class IdentityRevalidatingAuthenticationStateProvider(
            ILoggerFactory loggerFactory,
            IServiceScopeFactory scopeFactory,
            IOptions<IdentityOptions> options)
        : RevalidatingServerAuthenticationStateProvider(loggerFactory)
    {
        private DateTimeOffset _lastEnrichedAt = DateTimeOffset.MinValue;

        protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var state = await base.GetAuthenticationStateAsync();
            if (state.User.Identity is { IsAuthenticated: true })
            {
                var now = DateTimeOffset.UtcNow;
                if ((now - _lastEnrichedAt).TotalSeconds >= 1.5)
                {
                    _lastEnrichedAt = now;
                    await DatabaseClaimsSynchronizer.EnrichPrincipalFromDatabaseAsync(state.User, scopeFactory);
                }
            }
            return state;
        }

        protected override async Task<bool> ValidateAuthenticationStateAsync(
            AuthenticationState authenticationState, CancellationToken cancellationToken)
        {
            await DatabaseClaimsSynchronizer.EnrichPrincipalFromDatabaseAsync(authenticationState.User, scopeFactory, cancellationToken);

            // Get the user manager from a new scope to ensure it fetches fresh data
            await using var scope = scopeFactory.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            return await ValidateSecurityStampAsync(userManager, authenticationState.User);
        }

        private async Task<bool> ValidateSecurityStampAsync(UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null)
            {
                return false;
            }
            else if (!userManager.SupportsUserSecurityStamp)
            {
                return true;
            }
            else
            {
                var principalStamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
                var userStamp = await userManager.GetSecurityStampAsync(user);
                return principalStamp == userStamp;
            }
        }
    }
}
