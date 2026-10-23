using System.Security.Claims;
using API.HR.Node.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class ProfilePhotoOwnershipTests
{
    [Fact]
    public void ClaimBasedOwnership_WhenEmployeeIdMatches_ShouldGrantAccess()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("employee_id", "EMP-1001"));
        var principal = new ClaimsPrincipal(identity);

        var claimEmpId = principal.FindFirst("employee_id")?.Value;
        var isOwnPhoto = !string.IsNullOrWhiteSpace(claimEmpId) && claimEmpId.Equals("EMP-1001", StringComparison.OrdinalIgnoreCase);

        isOwnPhoto.Should().BeTrue();
    }

    [Fact]
    public void ClaimBasedOwnership_WhenEmployeeIdMismatches_ShouldNotGrantAccess()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("employee_id", "EMP-1001"));
        var principal = new ClaimsPrincipal(identity);

        var claimEmpId = principal.FindFirst("employee_id")?.Value;
        var isOwnPhoto = !string.IsNullOrWhiteSpace(claimEmpId) && claimEmpId.Equals("EMP-2002", StringComparison.OrdinalIgnoreCase);

        isOwnPhoto.Should().BeFalse();
    }
}
