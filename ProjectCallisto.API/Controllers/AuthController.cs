using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ProjectCallisto.Domain.Users;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.API.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext dbContext, IMemoryCache cache, ILogger<AuthController> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }
    
    [Authorize]
    [HttpGet("api/auth/me")]
    public async Task<IActionResult> Me()
    {
        var user = HttpContext.User;
        var claims = user.Claims;
        var auth0User = new Auth0User()
        {
            GivenName = claims.FirstOrDefault(c => c.Type == "given_name")?.Value,
            FamilyName = claims.FirstOrDefault(c => c.Type == "family_name")?.Value,
            Nickname = claims.FirstOrDefault(c => c.Type == "nickname")?.Value,
            Name = claims.FirstOrDefault(c => c.Type == "name")?.Value,
            Picture = claims.FirstOrDefault(c => c.Type == "picture")?.Value != null
                ? new Uri(claims.FirstOrDefault(c => c.Type == "picture")!.Value)
                : null,
            UpdatedAt = claims.FirstOrDefault(c => c.Type == "updated_at")?.Value != null
                ? DateTimeOffset.Parse(claims.FirstOrDefault(c => c.Type == "updated_at")!.Value) :
                DateTimeOffset.UtcNow,
            Email = claims.FirstOrDefault(c => c.Type == "email")?.Value,
            EmailVerified = claims.FirstOrDefault(c => c.Type == "email_verified") != null,
            Sub = claims.FirstOrDefault(c => c.Type == "sub")?.Value,
            Sid = claims.FirstOrDefault(c => c.Type == "sid")?.Value
        };
        var subjectId = auth0User.Sub!;
        var cacheKey = $"user:{subjectId}";
        if (_cache.TryGetValue<User>(cacheKey, out var cachedUser) && cachedUser != null)
        {
            return Ok(cachedUser);
        }
        
        // look for user in db. if dont exist, create new user.
        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.SubjectId == subjectId);
        if (existingUser == null)
        {
            existingUser = new User()
            {
                SubjectId = auth0User.Sub!,
                Email = auth0User.Email!,
                Name = auth0User.Name,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await _dbContext.Users.AddAsync(existingUser);
            await _dbContext.SaveChangesAsync();
        }
        _cache.Set(cacheKey, existingUser);
        
        return Ok(existingUser);
    }

    [HttpGet("signin")]
    public IActionResult SignIn([FromQuery] string? returnUrl = "/")
    {
        var validatedUrl = ValidateAndSanitizeReturnUrl(returnUrl);
        return Challenge(new AuthenticationProperties { RedirectUri = validatedUrl });
    }

    /// <summary>
    /// Validates return URL to prevent open redirect attacks.
    /// Only allows relative URLs starting with '/'. Rejects absolute URLs and protocol-relative URLs.
    /// </summary>
    private string ValidateAndSanitizeReturnUrl(string? returnUrl)
    {
        // Default to home if null or empty
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        // Trim whitespace
        returnUrl = returnUrl.Trim();

        // Only allow relative URLs starting with '/'
        if (!returnUrl.StartsWith('/'))
        {
            _logger.LogWarning("Rejected return URL (not relative): {ReturnUrl}", returnUrl);
            return "/";
        }

        // Reject protocol-relative URLs (//evil.com)
        if (returnUrl.StartsWith("//"))
        {
            _logger.LogWarning("Rejected protocol-relative return URL: {ReturnUrl}", returnUrl);
            return "/";
        }

        // Check for suspicious patterns (URLs with protocols embedded)
        if (returnUrl.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected return URL with embedded protocol: {ReturnUrl}", returnUrl);
            return "/";
        }

        // Valid relative URL
        return returnUrl;
    }

    [HttpGet("signout")]
    public new IActionResult SignOut()
    {
        return SignOut(new AuthenticationProperties { RedirectUri = "/" }, "Cookies", "OpenIdConnect");
    }
}

public class Auth0User
{
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? Nickname { get; set; }
    public string? Name { get; set; }
    public Uri? Picture { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? Sub { get; set; }
    public string? Sid  { get; set; }
}
