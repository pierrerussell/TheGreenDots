using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectCallisto.API.Controllers;
using ProjectCallisto.EfCore;
using Xunit;

namespace ProjectCallisto.Tests.Controllers;

/// <summary>
/// Tests for AuthController, focusing on open redirect vulnerability prevention.
/// </summary>
public class AuthControllerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<AuthController>>();

        _controller = new AuthController(_dbContext, _cache, _loggerMock.Object);

        // Mock HttpContext for authentication
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region Open Redirect Prevention Tests

    [Fact]
    public void SignIn_WithValidRelativeUrl_AllowsRedirect()
    {
        // Arrange
        var returnUrl = "/dashboard";

        // Act
        var result = _controller.SignIn(returnUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/dashboard", result.Properties?.RedirectUri);
        VerifyNoWarningLogged();
    }

    [Fact]
    public void SignIn_WithRootPath_AllowsRedirect()
    {
        // Arrange
        var returnUrl = "/";

        // Act
        var result = _controller.SignIn(returnUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyNoWarningLogged();
    }

    [Fact]
    public void SignIn_WithDeepRelativePath_AllowsRedirect()
    {
        // Arrange
        var returnUrl = "/organisation/123/settings";

        // Act
        var result = _controller.SignIn(returnUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/organisation/123/settings", result.Properties?.RedirectUri);
        VerifyNoWarningLogged();
    }

    [Fact]
    public void SignIn_WithQueryParameters_AllowsRedirect()
    {
        // Arrange
        var returnUrl = "/dashboard?tab=reports&date=2026-05-07";

        // Act
        var result = _controller.SignIn(returnUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/dashboard?tab=reports&date=2026-05-07", result.Properties?.RedirectUri);
        VerifyNoWarningLogged();
    }

    [Fact]
    public void SignIn_WithFragment_AllowsRedirect()
    {
        // Arrange
        var returnUrl = "/dashboard#section-1";

        // Act
        var result = _controller.SignIn(returnUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/dashboard#section-1", result.Properties?.RedirectUri);
        VerifyNoWarningLogged();
    }

    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("http://evil.com")]
    [InlineData("https://evil.com/phishing")]
    [InlineData("http://evil.com/steal-credentials")]
    public void SignIn_WithAbsoluteUrl_RejectsAndRedirectsToHome(string maliciousUrl)
    {
        // Act
        var result = _controller.SignIn(maliciousUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyWarningLogged();
    }

    [Theory]
    [InlineData("//evil.com")]
    [InlineData("//evil.com/phishing")]
    [InlineData("///evil.com")]
    public void SignIn_WithProtocolRelativeUrl_RejectsAndRedirectsToHome(string maliciousUrl)
    {
        // Act
        var result = _controller.SignIn(maliciousUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyWarningLogged();
    }

    [Theory]
    [InlineData("/redirect?url=https://evil.com")]
    [InlineData("/path/with://protocol")]
    public void SignIn_WithEmbeddedProtocol_RejectsAndRedirectsToHome(string maliciousUrl)
    {
        // Act
        var result = _controller.SignIn(maliciousUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyWarningLogged();
    }

    [Theory]
    [InlineData("javascript:alert('xss')")]
    [InlineData("data:text/html,<script>alert('xss')</script>")]
    [InlineData("vbscript:msgbox")]
    public void SignIn_WithJavaScriptOrDataUrl_RejectsAndRedirectsToHome(string maliciousUrl)
    {
        // Act
        var result = _controller.SignIn(maliciousUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyWarningLogged();
    }

    [Fact]
    public void SignIn_WithNullReturnUrl_DefaultsToHome()
    {
        // Act
        var result = _controller.SignIn(null) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyNoWarningLogged();
    }

    [Fact]
    public void SignIn_WithEmptyReturnUrl_DefaultsToHome()
    {
        // Act
        var result = _controller.SignIn("") as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyNoWarningLogged();
    }

    [Fact]
    public void SignIn_WithWhitespaceReturnUrl_DefaultsToHome()
    {
        // Act
        var result = _controller.SignIn("   ") as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyNoWarningLogged();
    }

    [Fact]
    public void SignIn_WithUrlWithLeadingWhitespace_TrimsAndAllows()
    {
        // Arrange
        var returnUrl = "  /dashboard";

        // Act
        var result = _controller.SignIn(returnUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/dashboard", result.Properties?.RedirectUri);
        VerifyNoWarningLogged();
    }

    [Theory]
    [InlineData("evil.com")]
    [InlineData("www.evil.com")]
    [InlineData("subdomain.evil.com/path")]
    public void SignIn_WithDomainWithoutProtocol_RejectsAndRedirectsToHome(string maliciousUrl)
    {
        // Act
        var result = _controller.SignIn(maliciousUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyWarningLogged();
    }

    [Fact]
    public void SignIn_WithBackslashes_RejectsAndRedirectsToHome()
    {
        // Arrange - backslashes can be interpreted as forward slashes in some contexts
        var returnUrl = "\\evil.com";

        // Act
        var result = _controller.SignIn(returnUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyWarningLogged();
    }

    #endregion

    #region Real-World Attack Scenarios

    [Fact]
    public void SignIn_PhishingAttackScenario_IsBlocked()
    {
        // Arrange - Attacker sends: signin?returnUrl=https://fake-greendots.com/phishing
        var attackUrl = "https://fake-greendots.com/phishing";

        // Act
        var result = _controller.SignIn(attackUrl) as ChallengeResult;

        // Assert - User is redirected to safe home page instead
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyWarningLogged();
    }

    [Fact]
    public void SignIn_ProtocolRelativePhishing_IsBlocked()
    {
        // Arrange - Attacker uses protocol-relative URL to match current protocol
        var attackUrl = "//fake-greendots.com/steal-session";

        // Act
        var result = _controller.SignIn(attackUrl) as ChallengeResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/", result.Properties?.RedirectUri);
        VerifyWarningLogged();
    }

    #endregion

    #region Helper Methods

    private void VerifyWarningLogged()
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Expected warning to be logged for rejected return URL");
    }

    private void VerifyNoWarningLogged()
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "No warning should be logged for valid return URL");
    }

    #endregion

    public void Dispose()
    {
        _dbContext.Dispose();
        _cache.Dispose();
        GC.SuppressFinalize(this);
    }
}
