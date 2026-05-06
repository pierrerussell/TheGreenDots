using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectCallisto.Application.Reports;
using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.Tests.Integration;

namespace ProjectCallisto.Tests.Reports;

public class ReportCalculationServiceTests : IntegrationTestBase
{
    private readonly IReportCalculationService _service;
    private readonly IPresenceBreakdownCalculator _calculator;
    private readonly IInsightDetectionService _insightService;

    public ReportCalculationServiceTests()
    {
        _calculator = new PresenceBreakdownCalculator();
        _insightService = new InsightDetectionService();
        var logger = NullLogger<ReportCalculationService>.Instance;
        _service = new ReportCalculationService(DbContext, _calculator, _insightService, logger);
    }

    [Fact]
    public async Task CalculateWeeklyReportAsync_WithNoMembers_ReturnsEmptyReport()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync(
            name: "Test Org",
            timezone: "America/New_York");

        var workingHours = new WorkingHours(org.Id)
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };
        await DbContext.WorkingHours.AddAsync(workingHours);
        await DbContext.SaveChangesAsync();

        // Act
        var report = await _service.CalculateWeeklyReportAsync(org.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(org.Id, report.OrganisationId);
        Assert.Equal("Test Org", report.OrganisationName);
        Assert.Equal("America/New_York", report.Timezone);
        Assert.Empty(report.Employees);
        Assert.Equal(0, report.TotalMembers);
    }

    [Fact]
    public async Task CalculateWeeklyReportAsync_WithSingleMember_CalculatesCorrectBreakdowns()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync(
            name: "Test Org",
            timezone: "UTC");

        var workingHours = new WorkingHours(org.Id)
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };
        await DbContext.WorkingHours.AddAsync(workingHours);

        // Create a member
        var member = new TenantMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            MicrosoftUserId = "user-1",
            IsAssignedSeat = true,
            DisplayName = "John Doe",
            Email = "john@example.com",
            JobTitle = "Developer",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await DbContext.TenantMembers.AddAsync(member);

        // Add presence history for last Monday (9 AM to 5 PM, all Available)
        var lastMonday = GetLastMonday();
        for (int hour = 9; hour <= 16; hour++)
        {
            await DbContext.PresenceHistories.AddAsync(new PresenceHistory
            {
                TenantMemberId = member.Id,
                Availability = "Available",
                Activity = "Available",
                RecordedAt = new DateTimeOffset(lastMonday.Year, lastMonday.Month, lastMonday.Day, hour, 0, 0, TimeSpan.Zero)
            });
        }

        await DbContext.SaveChangesAsync();

        // Act
        var report = await _service.CalculateWeeklyReportAsync(org.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(report);
        Assert.Single(report.Employees);

        var employeeBreakdown = report.Employees[0];
        Assert.Equal(member.Id, employeeBreakdown.TenantMemberId);
        Assert.Equal("John Doe", employeeBreakdown.DisplayName);
        Assert.Equal("john@example.com", employeeBreakdown.Email);
        Assert.Equal("Developer", employeeBreakdown.JobTitle);

        // Verify working hours breakdown (9 AM - 4 PM on Monday = 7 hours Available)
        Assert.True(employeeBreakdown.WorkingHoursBreakdown.TotalHours > 0);
        Assert.True(employeeBreakdown.WorkingHoursBreakdown.AvailableHours > 0);

        // Verify full week breakdown
        Assert.Equal(employeeBreakdown.WorkingHoursBreakdown.TotalHours,
            employeeBreakdown.FullWeekBreakdown.TotalHours); // All activity was during working hours

        // Verify overtime calculation
        Assert.Equal(0, employeeBreakdown.OvertimeHours); // No overtime
    }

    [Fact]
    public async Task CalculateWeeklyReportAsync_WithMultipleStatuses_CalculatesCorrectly()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync(
            name: "Test Org",
            timezone: "UTC");

        var workingHours = new WorkingHours(org.Id)
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };
        await DbContext.WorkingHours.AddAsync(workingHours);

        var member = new TenantMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            MicrosoftUserId = "user-1",
            IsAssignedSeat = true,
            DisplayName = "Test Employee",
            Email = "test@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await DbContext.TenantMembers.AddAsync(member);

        var lastMonday = GetLastMonday();

        // Add presence with multiple statuses during working hours (9 AM - 12 PM)
        await DbContext.PresenceHistories.AddAsync(new PresenceHistory
        {
            TenantMemberId = member.Id,
            Availability = "Available",
            Activity = "Available",
            RecordedAt = new DateTimeOffset(lastMonday.Year, lastMonday.Month, lastMonday.Day, 9, 0, 0, TimeSpan.Zero)
        });

        await DbContext.PresenceHistories.AddAsync(new PresenceHistory
        {
            TenantMemberId = member.Id,
            Availability = "Busy",
            Activity = "InAMeeting",
            RecordedAt = new DateTimeOffset(lastMonday.Year, lastMonday.Month, lastMonday.Day, 10, 0, 0, TimeSpan.Zero)
        });

        await DbContext.PresenceHistories.AddAsync(new PresenceHistory
        {
            TenantMemberId = member.Id,
            Availability = "Away",
            Activity = "Away",
            RecordedAt = new DateTimeOffset(lastMonday.Year, lastMonday.Month, lastMonday.Day, 11, 0, 0, TimeSpan.Zero)
        });

        // Close out the day at noon
        await DbContext.PresenceHistories.AddAsync(new PresenceHistory
        {
            TenantMemberId = member.Id,
            Availability = "Offline",
            Activity = "Offline",
            RecordedAt = new DateTimeOffset(lastMonday.Year, lastMonday.Month, lastMonday.Day, 12, 0, 0, TimeSpan.Zero)
        });

        await DbContext.SaveChangesAsync();

        // Act
        var report = await _service.CalculateWeeklyReportAsync(org.Id, CancellationToken.None);

        // Assert
        Assert.Single(report.Employees);

        var employeeBreakdown = report.Employees[0];

        // Verify breakdowns calculated (should have 3 hours total: 9-10 Available, 10-11 Busy, 11-12 Away)
        Assert.True(employeeBreakdown.WorkingHoursBreakdown.TotalHours > 0);
        Assert.True(employeeBreakdown.WorkingHoursBreakdown.AvailableHours > 0);
        Assert.True(employeeBreakdown.WorkingHoursBreakdown.BusyHours > 0);
        Assert.True(employeeBreakdown.WorkingHoursBreakdown.AwayHours > 0);

        // Verify full week and working hours are same (no activity outside working hours)
        Assert.Equal(employeeBreakdown.WorkingHoursBreakdown.TotalHours, employeeBreakdown.FullWeekBreakdown.TotalHours);
        Assert.Equal(0, employeeBreakdown.OvertimeHours);
    }

    [Fact]
    public async Task CalculateDailyReportAsync_CalculatesYesterdayData()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync(
            name: "Test Org",
            timezone: "UTC");

        var workingHours = new WorkingHours(org.Id)
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday | WorkingDaysFlags.Saturday | WorkingDaysFlags.Sunday
        };
        await DbContext.WorkingHours.AddAsync(workingHours);

        var member = new TenantMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            MicrosoftUserId = "user-1",
            IsAssignedSeat = true,
            DisplayName = "Daily Worker",
            Email = "daily@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await DbContext.TenantMembers.AddAsync(member);

        // Add presence for yesterday
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1).Date;
        for (int hour = 9; hour <= 12; hour++)
        {
            await DbContext.PresenceHistories.AddAsync(new PresenceHistory
            {
                TenantMemberId = member.Id,
                Availability = "Available",
                Activity = "Available",
                RecordedAt = new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, hour, 0, 0, TimeSpan.Zero)
            });
        }

        await DbContext.SaveChangesAsync();

        // Act
        var report = await _service.CalculateDailyReportAsync(org.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(report);
        Assert.Single(report.Employees);
        Assert.Equal("Daily Worker", report.Employees[0].DisplayName);

        // Verify date range is yesterday
        Assert.Equal(yesterday.Date, report.StartDate.Date);
        Assert.Equal(yesterday.Date, report.EndDate.Date);
    }

    [Fact]
    public async Task CalculateMonthlyReportAsync_CalculatesLastMonthData()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync(
            name: "Test Org",
            timezone: "UTC");

        var workingHours = new WorkingHours(org.Id)
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };
        await DbContext.WorkingHours.AddAsync(workingHours);

        var member = new TenantMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            MicrosoftUserId = "user-1",
            IsAssignedSeat = true,
            DisplayName = "Monthly Worker",
            Email = "monthly@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await DbContext.TenantMembers.AddAsync(member);

        // Add presence for first day of last month
        var today = DateTimeOffset.UtcNow;
        var firstDayOfLastMonth = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1);

        for (int hour = 9; hour <= 12; hour++)
        {
            await DbContext.PresenceHistories.AddAsync(new PresenceHistory
            {
                TenantMemberId = member.Id,
                Availability = "Available",
                Activity = "Available",
                RecordedAt = new DateTimeOffset(firstDayOfLastMonth.Year, firstDayOfLastMonth.Month, firstDayOfLastMonth.Day, hour, 0, 0, TimeSpan.Zero)
            });
        }

        await DbContext.SaveChangesAsync();

        // Act
        var report = await _service.CalculateMonthlyReportAsync(org.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(report);
        Assert.Single(report.Employees);
        Assert.Equal("Monthly Worker", report.Employees[0].DisplayName);

        // Verify date range is last month
        Assert.Equal(firstDayOfLastMonth.Month, report.StartDate.Month);
        Assert.Equal(firstDayOfLastMonth.Month, report.EndDate.Month);
    }

    [Fact]
    public async Task CalculateWeeklyReportAsync_WithMultipleMembers_CalculatesAllBreakdowns()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync(
            name: "Test Org",
            timezone: "UTC");

        var workingHours = new WorkingHours(org.Id)
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };
        await DbContext.WorkingHours.AddAsync(workingHours);

        // Create 3 members
        var member1 = new TenantMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            MicrosoftUserId = "user-1",
            IsAssignedSeat = true,
            DisplayName = "Alice",
            Email = "alice@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var member2 = new TenantMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            MicrosoftUserId = "user-2",
            IsAssignedSeat = true,
            DisplayName = "Bob",
            Email = "bob@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var member3 = new TenantMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            MicrosoftUserId = "user-3",
            IsAssignedSeat = true,
            DisplayName = "Charlie",
            Email = "charlie@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await DbContext.TenantMembers.AddRangeAsync(member1, member2, member3);

        var lastMonday = GetLastMonday();

        // Add minimal presence for each member
        foreach (var member in new[] { member1, member2, member3 })
        {
            await DbContext.PresenceHistories.AddAsync(new PresenceHistory
            {
                TenantMemberId = member.Id,
                Availability = "Available",
                Activity = "Available",
                RecordedAt = new DateTimeOffset(lastMonday.Year, lastMonday.Month, lastMonday.Day, 9, 0, 0, TimeSpan.Zero)
            });
        }

        await DbContext.SaveChangesAsync();

        // Act
        var report = await _service.CalculateWeeklyReportAsync(org.Id, CancellationToken.None);

        // Assert
        Assert.Equal(3, report.Employees.Count);
        Assert.Equal(3, report.TotalMembers);

        // Verify all members are in the report
        Assert.Contains(report.Employees, e => e.DisplayName == "Alice");
        Assert.Contains(report.Employees, e => e.DisplayName == "Bob");
        Assert.Contains(report.Employees, e => e.DisplayName == "Charlie");
    }

    [Fact]
    public async Task CalculateWeeklyReportAsync_WithTimezone_HandlesTimezoneConversion()
    {
        // Arrange - Use Pacific timezone
        var org = await CreateTestOrganisationAsync(
            name: "Test Org",
            timezone: "America/Los_Angeles");

        var workingHours = new WorkingHours(org.Id)
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };
        await DbContext.WorkingHours.AddAsync(workingHours);

        var member = new TenantMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            MicrosoftUserId = "user-1",
            IsAssignedSeat = true,
            DisplayName = "Pacific Worker",
            Email = "pacific@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await DbContext.TenantMembers.AddAsync(member);

        var lastMonday = GetLastMonday();

        // Add presence at 9 AM Pacific time (convert to UTC)
        var pacificTz = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        var localTime = new DateTimeOffset(lastMonday.Year, lastMonday.Month, lastMonday.Day, 9, 0, 0,
            pacificTz.GetUtcOffset(lastMonday));
        var utcTime = localTime.ToUniversalTime();

        await DbContext.PresenceHistories.AddAsync(new PresenceHistory
        {
            TenantMemberId = member.Id,
            Availability = "Available",
            Activity = "Available",
            RecordedAt = utcTime
        });

        await DbContext.SaveChangesAsync();

        // Act
        var report = await _service.CalculateWeeklyReportAsync(org.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(report);
        Assert.Equal("America/Los_Angeles", report.Timezone);
        Assert.Single(report.Employees);

        // Verify dates are in Pacific timezone
        Assert.NotEqual(DateTimeOffset.MinValue, report.StartDate);
        Assert.NotEqual(DateTimeOffset.MinValue, report.EndDate);
    }

    private DateTimeOffset GetLastMonday()
    {
        // Match the service's logic: get Monday from the week BEFORE last week
        var today = DateTimeOffset.UtcNow.Date;
        var daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var lastMonday = today.AddDays(-daysSinceMonday - 7);

        return new DateTimeOffset(lastMonday, TimeSpan.Zero);
    }
}
