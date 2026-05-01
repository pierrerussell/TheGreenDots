using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectCallisto.API.Controllers;
using ProjectCallisto.Domain.Organisations;
using Xunit;

namespace ProjectCallisto.Tests.Integration;

public class WorkingHoursControllerTests : IntegrationTestBase
{
    private readonly WorkingHoursController _controller;

    public WorkingHoursControllerTests()
    {
        _controller = new WorkingHoursController(DbContext);
    }

    [Fact]
    public async Task GetWorkingHours_WhenNotConfigured_ReturnsDefaults()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();

        // Act
        var result = await _controller.GetWorkingHours(organisation.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<WorkingHoursResponse>().Subject;

        response.Id.Should().BeNull();
        response.StartTime.Should().Be(new TimeOnly(9, 0));
        response.EndTime.Should().Be(new TimeOnly(17, 0));
        response.WorkingDays.Should().BeEquivalentTo(new[] { "monday", "tuesday", "wednesday", "thursday", "friday" });
    }

    [Fact]
    public async Task GetWorkingHours_WhenConfigured_ReturnsExistingWorkingHours()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var workingHours = new WorkingHours(organisation.Id)
        {
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(18, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Wednesday | WorkingDaysFlags.Friday
        };
        await DbContext.WorkingHours.AddAsync(workingHours);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetWorkingHours(organisation.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<WorkingHoursResponse>().Subject;

        response.Id.Should().Be(workingHours.Id);
        response.StartTime.Should().Be(new TimeOnly(8, 0));
        response.EndTime.Should().Be(new TimeOnly(18, 0));
        response.WorkingDays.Should().BeEquivalentTo(new[] { "monday", "wednesday", "friday" });
    }

    [Fact]
    public async Task UpdateWorkingHours_WhenNotConfigured_CreatesNewWorkingHours()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateWorkingHoursRequest(
            StartTime: new TimeOnly(8, 0),
            EndTime: new TimeOnly(18, 0),
            WorkingDays: new[] { "monday", "tuesday", "wednesday", "thursday", "friday" }
        );

        // Act
        var result = await _controller.UpdateWorkingHours(organisation.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<WorkingHoursResponse>().Subject;

        response.Id.Should().NotBeNull();
        response.StartTime.Should().Be(new TimeOnly(8, 0));
        response.EndTime.Should().Be(new TimeOnly(18, 0));

        // Verify in database
        var savedWorkingHours = await DbContext.WorkingHours
            .FirstOrDefaultAsync(wh => wh.OrganisationId == organisation.Id);
        savedWorkingHours.Should().NotBeNull();
        savedWorkingHours!.StartTime.Should().Be(new TimeOnly(8, 0));
        savedWorkingHours.EndTime.Should().Be(new TimeOnly(18, 0));
    }

    [Fact]
    public async Task UpdateWorkingHours_WhenConfigured_UpdatesExistingWorkingHours()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var workingHours = new WorkingHours(organisation.Id)
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0)
        };
        await DbContext.WorkingHours.AddAsync(workingHours);
        await DbContext.SaveChangesAsync();

        var request = new UpdateWorkingHoursRequest(
            StartTime: new TimeOnly(7, 0),
            EndTime: new TimeOnly(19, 0),
            WorkingDays: new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" }
        );

        // Act
        var result = await _controller.UpdateWorkingHours(organisation.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<WorkingHoursResponse>().Subject;

        response.Id.Should().Be(workingHours.Id);
        response.StartTime.Should().Be(new TimeOnly(7, 0));
        response.EndTime.Should().Be(new TimeOnly(19, 0));
        response.WorkingDays.Should().Contain("saturday");

        // Verify only one record exists
        var count = await DbContext.WorkingHours.CountAsync(wh => wh.OrganisationId == organisation.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task UpdateWorkingHours_StartTimeAfterEndTime_ReturnsBadRequest()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateWorkingHoursRequest(
            StartTime: new TimeOnly(18, 0),
            EndTime: new TimeOnly(9, 0),
            WorkingDays: new[] { "monday" }
        );

        // Act
        var result = await _controller.UpdateWorkingHours(organisation.Id, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Start time must be before end time");
    }

    [Fact]
    public async Task UpdateWorkingHours_StartTimeEqualsEndTime_ReturnsBadRequest()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateWorkingHoursRequest(
            StartTime: new TimeOnly(9, 0),
            EndTime: new TimeOnly(9, 0),
            WorkingDays: new[] { "monday" }
        );

        // Act
        var result = await _controller.UpdateWorkingHours(organisation.Id, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Start time must be before end time");
    }

    [Fact]
    public async Task UpdateWorkingHours_EmptyWorkingDaysArray_ReturnsBadRequest()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateWorkingHoursRequest(
            StartTime: new TimeOnly(9, 0),
            EndTime: new TimeOnly(17, 0),
            WorkingDays: Array.Empty<string>()
        );

        // Act
        var result = await _controller.UpdateWorkingHours(organisation.Id, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("At least one working day must be selected");
    }

    [Fact]
    public async Task UpdateWorkingHours_WeekendOnly_WorksCorrectly()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateWorkingHoursRequest(
            StartTime: new TimeOnly(9, 0),
            EndTime: new TimeOnly(17, 0),
            WorkingDays: new[] { "saturday", "sunday" }
        );

        // Act
        var result = await _controller.UpdateWorkingHours(organisation.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<WorkingHoursResponse>().Subject;

        response.WorkingDays.Should().BeEquivalentTo(new[] { "saturday", "sunday" });
    }

    [Fact]
    public async Task UpdateWorkingHours_AllDays_WorksCorrectly()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateWorkingHoursRequest(
            StartTime: new TimeOnly(9, 0),
            EndTime: new TimeOnly(17, 0),
            WorkingDays: new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" }
        );

        // Act
        var result = await _controller.UpdateWorkingHours(organisation.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<WorkingHoursResponse>().Subject;

        response.WorkingDays.Should().HaveCount(7);
        response.WorkingDays.Should().Contain(new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" });
    }

    [Fact]
    public async Task UpdateWorkingHours_WithInvalidDayNames_IgnoresInvalidDays()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var request = new UpdateWorkingHoursRequest(
            StartTime: new TimeOnly(9, 0),
            EndTime: new TimeOnly(17, 0),
            WorkingDays: new[] { "monday", "invalidday", "friday" }
        );

        // Act
        var result = await _controller.UpdateWorkingHours(organisation.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<WorkingHoursResponse>().Subject;

        response.WorkingDays.Should().BeEquivalentTo(new[] { "monday", "friday" });
    }

    [Fact]
    public async Task UpdateWorkingHours_UpdatedAtTimestamp_IsUpdated()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var workingHours = new WorkingHours(organisation.Id);
        await DbContext.WorkingHours.AddAsync(workingHours);
        await DbContext.SaveChangesAsync();

        var originalUpdatedAt = workingHours.UpdatedAt;
        await Task.Delay(10); // Small delay to ensure timestamp difference

        var request = new UpdateWorkingHoursRequest(
            StartTime: new TimeOnly(8, 0),
            EndTime: new TimeOnly(18, 0),
            WorkingDays: new[] { "monday", "friday" }
        );

        // Act
        await _controller.UpdateWorkingHours(organisation.Id, request);

        // Assert
        var updated = await DbContext.WorkingHours.FirstAsync(wh => wh.Id == workingHours.Id);
        updated.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }
}
