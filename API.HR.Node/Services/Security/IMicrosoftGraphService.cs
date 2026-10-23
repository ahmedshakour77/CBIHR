namespace API.HR.Node.Services.Security;

public record MicrosoftUserProfile(
    string? Id,
    string? DisplayName,
    string? Mail,
    string? UserPrincipalName,
    string? GivenName,
    string? Surname,
    string? JobTitle)
{
    public string? PrimaryEmail => !string.IsNullOrWhiteSpace(Mail) ? Mail : UserPrincipalName;
}

public interface IMicrosoftGraphService
{
    Task<MicrosoftUserProfile?> GetUserProfileAsync(string accessToken, CancellationToken cancellationToken = default);
}
