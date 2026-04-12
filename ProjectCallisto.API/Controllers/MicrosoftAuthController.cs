using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectCallisto.API.Configuration;

namespace ProjectCallisto.API.Controllers;

[ApiController]
[Route("/api")]
public class MicrosoftAuthController : ControllerBase
{
    private readonly IOptions<MicrosoftGraphOptions> _options;
    private readonly HttpClient _httpClient;
    
    public MicrosoftAuthController( IOptions<MicrosoftGraphOptions> options,  IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpGet("auth/microsoft/connect")]
    public IActionResult Connect()
    {
        // create random state string for CSRF protection
        var state = Guid.NewGuid().ToString();
        
        Response.Cookies.Append("ms_auth_state", state, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = true,
            Expires = DateTimeOffset.UtcNow.AddMinutes(5)
        });

        var queryParams = new Dictionary<string, string?>
        {
            ["client_id"] = _options.Value.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _options.Value.RedirectUri,
            ["scope"] = string.Join(" ", _options.Value.Scopes),
            ["response_mode"] = "query",
            ["state"] = state
        };

        var queryString = QueryString.Create(queryParams);
        var authUrl = $"https://login.microsoftonline.com/organizations/oauth2/v2.0/authorize{queryString}";      
        
        return Redirect(authUrl);
    }

    [HttpGet("auth/microsoft/callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        var savedState = Request.Cookies["ms_auth_state"];
        if (state != savedState)
        {
            return BadRequest("Invalid state");
        }
        
        // exchange code for token
        var formBody = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["client_id"] = _options.Value.ClientId,
            ["client_secret"] = _options.Value.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = _options.Value.RedirectUri,
            ["grant_type"] = "authorization_code"
        });
        var response = await _httpClient.PostAsync("https://login.microsoftonline.com/organizations/oauth2/v2.0/token", formBody);
        var tokenString = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return BadRequest($"Token exchange failed: {tokenString}");
        }
        Response.Cookies.Delete("ms_auth_state");
        var token = JsonSerializer.Deserialize<MicrosoftTokenResponse>(tokenString);
        return Ok(token);


    }
}

public class MicrosoftTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }
    [JsonPropertyName("expires_in")]
    public int  ExpiresIn { get; set; }
}