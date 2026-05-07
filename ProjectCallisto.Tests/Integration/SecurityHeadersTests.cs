using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProjectCallisto.Tests.Integration;

public class SecurityHeadersTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityHeadersTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AllEndpoints_ShouldInclude_XContentTypeOptions_Header()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");
    }

    [Fact]
    public async Task AllEndpoints_ShouldInclude_XFrameOptions_Header()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").First().Should().Be("DENY");
    }

    [Fact]
    public async Task AllEndpoints_ShouldInclude_XXSSProtection_Header()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.Headers.Should().ContainKey("X-XSS-Protection");
        response.Headers.GetValues("X-XSS-Protection").First().Should().Be("1; mode=block");
    }

    [Fact]
    public async Task AllEndpoints_ShouldInclude_StrictTransportSecurity_Header()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.Headers.Should().ContainKey("Strict-Transport-Security");
        var hstsValue = response.Headers.GetValues("Strict-Transport-Security").First();
        hstsValue.Should().Contain("max-age=31536000");
        hstsValue.Should().Contain("includeSubDomains");
    }

    [Fact]
    public async Task AllEndpoints_ShouldInclude_ContentSecurityPolicy_Header()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.Headers.Should().ContainKey("Content-Security-Policy");
        var cspValue = response.Headers.GetValues("Content-Security-Policy").First();
        cspValue.Should().Contain("default-src 'self'");
        cspValue.Should().Contain("script-src 'self'");
        cspValue.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task APIEndpoints_ShouldInclude_AllSecurityHeaders()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Try an unauthenticated API request (will get 401 but headers should still be present)
        var response = await client.GetAsync("/api/organisations");

        // Assert - All security headers should be present even on error responses
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.Should().ContainKey("X-XSS-Protection");
        response.Headers.Should().ContainKey("Strict-Transport-Security");
        response.Headers.Should().ContainKey("Content-Security-Policy");
    }
}
