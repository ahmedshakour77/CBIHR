namespace API.HR.Node.Services.Canva;

public class CanvaOptions
{
    public const string SectionName = "Canva";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = "brandtemplate:meta:read brandtemplate:content:read design:meta:read design:content:read design:content:write asset:read asset:write profile:read";
}
