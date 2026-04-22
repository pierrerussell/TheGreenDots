namespace ProjectCallisto.Application.Microsoft;

public class MicrosoftGraphOptions
{
    public string ClientId { get; set; }
    public string  ClientSecret { get; set; }
    public string TenantId  { get; set; }
    public string RedirectUri { get; set; }
    public string[]  Scopes { get; set; }
}