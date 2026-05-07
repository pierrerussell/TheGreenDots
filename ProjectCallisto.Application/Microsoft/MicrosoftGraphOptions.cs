namespace ProjectCallisto.Application.Microsoft;

public class MicrosoftGraphOptions
{
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string TenantId { get; set; }
    public required string RedirectUri { get; set; }
    public required string[] Scopes { get; set; }
}