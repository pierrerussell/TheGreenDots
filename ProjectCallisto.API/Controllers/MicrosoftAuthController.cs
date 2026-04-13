using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectCallisto.API.Configuration;
using ProjectCallisto.API.Services;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.API.Controllers;

[Authorize]
[ApiController]
[Route("/api")]
public class MicrosoftAuthController : ControllerBase
{
    private readonly MicrosoftGraphOptions _options;
    private readonly AppDbContext _dbContext;
    private readonly IOrganisationOnboardingService _onboardingService;

    public MicrosoftAuthController(
        IOptions<MicrosoftGraphOptions> options,
        AppDbContext dbContext,
        IOrganisationOnboardingService onboardingService)
    {
        _options = options.Value;
        _dbContext = dbContext;
        _onboardingService = onboardingService;
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
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = string.Join(" ", _options.Scopes),
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
        // Validate state
        var savedState = Request.Cookies["ms_auth_state"];
        if (state != savedState)
        {
            return BadRequest("Invalid state");
        }
        Response.Cookies.Delete("ms_auth_state");

        // Get current user
        var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.SubjectId == subjectId);
        if (user == null)
        {
            return BadRequest("User not found");
        }

        try
        {
            var organisation = await _onboardingService.ConnectOrganisationAsync(user, code);
            return Redirect($"/onboarding/add-organization?success=true&orgId={organisation.Id}");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}