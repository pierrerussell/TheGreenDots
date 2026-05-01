using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using ProjectCallisto.API.Controllers;
using ProjectCallisto.Application.Microsoft;
using Xunit;

namespace ProjectCallisto.Tests.Integration;

public class OrganisationsControllerTimezoneTests : IntegrationTestBase
{
    private readonly OrganisationsController _controller;
    private readonly Mock<IMicrosoftGraphService> _mockGraphService;

    public OrganisationsControllerTimezoneTests()
    {
        _mockGraphService = new Mock<IMicrosoftGraphService>();
        _controller = new OrganisationsController(DbContext, _mockGraphService.Object);
    }

    [Fact]
    public async Task GetTimezone_OrganisationExists_ReturnsTimezoneInfo()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync(
            country: "SG",
            timezone: "Asia/Singapore"
        );
        organisation.CountryDetectedFrom = "Microsoft";
        organisation.TimezoneDetectedFrom = "Country";
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetTimezone(organisation.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;

        response.Should().NotBeNull();
        var country = response!.GetType().GetProperty("country")!.GetValue(response);
        var countryDetectedFrom = response.GetType().GetProperty("countryDetectedFrom")!.GetValue(response);
        var timezone = response.GetType().GetProperty("timezone")!.GetValue(response);
        var timezoneDetectedFrom = response.GetType().GetProperty("timezoneDetectedFrom")!.GetValue(response);

        country.Should().Be("SG");
        countryDetectedFrom.Should().Be("Microsoft");
        timezone.Should().Be("Asia/Singapore");
        timezoneDetectedFrom.Should().Be("Country");
    }

    [Fact]
    public async Task GetTimezone_OrganisationNotFound_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _controller.GetTimezone(nonExistentId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetTimezone_OrganisationWithoutTimezone_ReturnsNullValues()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();

        // Act
        var result = await _controller.GetTimezone(organisation.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;

        var country = response!.GetType().GetProperty("country")!.GetValue(response);
        var timezone = response.GetType().GetProperty("timezone")!.GetValue(response);

        country.Should().BeNull();
        timezone.Should().BeNull();
    }

    [Theory]
    [InlineData("Asia/Singapore")]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Australia/Sydney")]
    [InlineData("UTC")]
    public async Task UpdateTimezone_ValidIANATimezone_UpdatesSuccessfully(string timezone)
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateTimezoneRequest(timezone);

        // Act
        var result = await _controller.UpdateTimezone(organisation.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;

        var updatedTimezone = response!.GetType().GetProperty("timezone")!.GetValue(response);
        var timezoneDetectedFrom = response.GetType().GetProperty("timezoneDetectedFrom")!.GetValue(response);

        updatedTimezone.Should().Be(timezone);
        timezoneDetectedFrom.Should().Be("Manual");

        // Verify in database
        var savedOrg = await DbContext.Organisations.FirstAsync(o => o.Id == organisation.Id);
        savedOrg.Timezone.Should().Be(timezone);
        savedOrg.TimezoneDetectedFrom.Should().Be("Manual");
    }

    [Fact]
    public async Task UpdateTimezone_InvalidIANATimezone_ReturnsBadRequest()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateTimezoneRequest("Invalid/Timezone");

        // Act
        var result = await _controller.UpdateTimezone(organisation.Id, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Invalid timezone: Invalid/Timezone");
    }

    [Fact]
    public async Task UpdateTimezone_EmptyTimezone_ReturnsBadRequest()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateTimezoneRequest("");

        // Act
        var result = await _controller.UpdateTimezone(organisation.Id, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateTimezone_OrganisationNotFound_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new UpdateTimezoneRequest("Asia/Singapore");

        // Act
        var result = await _controller.UpdateTimezone(nonExistentId, request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateTimezone_OverridesAutoDetectedTimezone_SetsManualDetection()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync(
            country: "SG",
            timezone: "Asia/Singapore"
        );
        organisation.CountryDetectedFrom = "Microsoft";
        organisation.TimezoneDetectedFrom = "Country";
        await DbContext.SaveChangesAsync();

        var request = new UpdateTimezoneRequest("America/New_York");

        // Act
        var result = await _controller.UpdateTimezone(organisation.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;

        var timezone = response!.GetType().GetProperty("timezone")!.GetValue(response);
        var timezoneDetectedFrom = response.GetType().GetProperty("timezoneDetectedFrom")!.GetValue(response);

        timezone.Should().Be("America/New_York");
        timezoneDetectedFrom.Should().Be("Manual");

        // Verify country is unchanged
        var savedOrg = await DbContext.Organisations.FirstAsync(o => o.Id == organisation.Id);
        savedOrg.Country.Should().Be("SG");
        savedOrg.CountryDetectedFrom.Should().Be("Microsoft");
    }

    [Fact]
    public async Task UpdateTimezone_MultipleUpdates_KeepsManualDetection()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var firstRequest = new UpdateTimezoneRequest("Asia/Singapore");
        await _controller.UpdateTimezone(organisation.Id, firstRequest);

        var secondRequest = new UpdateTimezoneRequest("Europe/London");

        // Act
        var result = await _controller.UpdateTimezone(organisation.Id, secondRequest);

        // Assert
        var savedOrg = await DbContext.Organisations.FirstAsync(o => o.Id == organisation.Id);
        savedOrg.Timezone.Should().Be("Europe/London");
        savedOrg.TimezoneDetectedFrom.Should().Be("Manual");
    }

    [Theory]
    [InlineData("Pacific/Auckland")]
    [InlineData("Africa/Johannesburg")]
    [InlineData("Asia/Dubai")]
    [InlineData("America/Sao_Paulo")]
    [InlineData("Europe/Berlin")]
    public async Task UpdateTimezone_AllMappedTimezones_AreValid(string timezone)
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateTimezoneRequest(timezone);

        // Act
        var result = await _controller.UpdateTimezone(organisation.Id, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateTimezone_PreservesOtherOrganisationProperties()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync(
            name: "Test Organisation",
            country: "SG"
        );
        organisation.StripeCustomerId = "cus_test123";
        await DbContext.SaveChangesAsync();

        var request = new UpdateTimezoneRequest("Asia/Singapore");

        // Act
        await _controller.UpdateTimezone(organisation.Id, request);

        // Assert
        var savedOrg = await DbContext.Organisations.FirstAsync(o => o.Id == organisation.Id);
        savedOrg.Name.Should().Be("Test Organisation");
        savedOrg.Country.Should().Be("SG");
        savedOrg.StripeCustomerId.Should().Be("cus_test123");
    }
}
