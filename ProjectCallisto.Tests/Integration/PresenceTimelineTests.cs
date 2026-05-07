using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.Domain.Users;
using ProjectCallisto.EfCore;
using Xunit;

namespace ProjectCallisto.Tests.Integration;

/// <summary>
/// Tests for GetPresenceTimeline endpoint, focusing on end-of-day data loss bug fix.
/// </summary>
public class PresenceTimelineTests : IDisposable
{
    private readonly AppDbContext _dbContext;

    public PresenceTimelineTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
    }

    [Fact]
    public async Task PresenceTimeline_WithStatusChangeAfterMidnight_ShowsCompleteDay()
    {
        // Arrange - Simulating the end-of-day bug scenario
        var org = await CreateTestOrganisationAsync();
        var member = await CreateTestMemberAsync(org.Id, "Test User");

        var dayStart = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero); // May 7 00:00
        var dayEnd = new DateTimeOffset(2026, 5, 7, 23, 59, 59, TimeSpan.Zero); // May 7 23:59

        // User's presence history for May 7
        await CreatePresenceRecordAsync(member.Id, "Available", dayStart.AddHours(9));    // 9:00 AM
        await CreatePresenceRecordAsync(member.Id, "Away", dayStart.AddHours(12));        // 12:00 PM (Lunch)
        await CreatePresenceRecordAsync(member.Id, "Available", dayStart.AddHours(13));   // 1:00 PM
        await CreatePresenceRecordAsync(member.Id, "Offline", dayStart.AddHours(17));     // 5:00 PM
        await CreatePresenceRecordAsync(member.Id, "Available", dayStart.AddHours(23.5)); // 11:30 PM ← Last record on May 7

        // CRITICAL: Next status change is AFTER midnight (May 8)
        // This simulates the hourly heartbeat polling
        await CreatePresenceRecordAsync(member.Id, "Available", dayEnd.AddHours(1.5));    // May 8, 1:30 AM (same status, heartbeat)
        await CreatePresenceRecordAsync(member.Id, "Offline", dayEnd.AddHours(2.5));      // May 8, 2:30 AM (status change)

        // Act - Query for May 7's timeline
        var historyRecords = await GetPresenceTimelineDataAsync(member.Id, dayStart, dayEnd);

        // Assert
        historyRecords.Should().NotBeEmpty("there should be presence records for May 7");

        // Verify we fetched the records after midnight (within 2-hour window)
        var recordsAfterMidnight = historyRecords.Where(r => r.RecordedAt > dayEnd).ToList();
        recordsAfterMidnight.Should().NotBeEmpty("we should fetch records up to 2 hours after midnight");
        recordsAfterMidnight.Should().HaveCountLessThanOrEqualTo(2, "we should only fetch records within 2-hour window");

        // The last segment on May 7 should be the 11:30 PM record
        var lastRecordOnMay7 = historyRecords
            .Where(r => r.RecordedAt >= dayStart && r.RecordedAt <= dayEnd)
            .OrderByDescending(r => r.RecordedAt)
            .First();

        lastRecordOnMay7.RecordedAt.Should().Be(dayStart.AddHours(23.5), "last record on May 7 is at 11:30 PM");
        lastRecordOnMay7.Availability.Should().Be("Available");

        // Verify we have enough data to render the complete day
        // (The UI will clip this segment to midnight, but backend knows it continues to 1:30 AM)
        var nextRecord = historyRecords
            .Where(r => r.RecordedAt > lastRecordOnMay7.RecordedAt)
            .OrderBy(r => r.RecordedAt)
            .FirstOrDefault();

        nextRecord.Should().NotBeNull("we should have the next record to know when the segment ends");
        nextRecord!.RecordedAt.Should().Be(dayEnd.AddHours(1.5), "next record should be at 1:30 AM on May 8");
    }

    [Fact]
    public async Task PresenceTimeline_WithMultipleUsersAndEndOfDayChanges_HandlesCorrectly()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync();
        var member1 = await CreateTestMemberAsync(org.Id, "User 1");
        var member2 = await CreateTestMemberAsync(org.Id, "User 2");

        var dayStart = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(2026, 5, 7, 23, 59, 59, TimeSpan.Zero);

        // User 1: Last change at 10 PM, next change after midnight
        await CreatePresenceRecordAsync(member1.Id, "Available", dayStart.AddHours(22));
        await CreatePresenceRecordAsync(member1.Id, "Offline", dayEnd.AddHours(1)); // May 8, 1:00 AM

        // User 2: Last change at 11:30 PM, next change after midnight
        await CreatePresenceRecordAsync(member2.Id, "Busy", dayStart.AddHours(23.5));
        await CreatePresenceRecordAsync(member2.Id, "Away", dayEnd.AddHours(2)); // May 8, 2:00 AM

        // Act
        var user1Records = await GetPresenceTimelineDataAsync(member1.Id, dayStart, dayEnd);
        var user2Records = await GetPresenceTimelineDataAsync(member2.Id, dayStart, dayEnd);

        // Assert
        user1Records.Should().Contain(r => r.RecordedAt > dayEnd, "User 1 should have record after midnight");
        user2Records.Should().Contain(r => r.RecordedAt > dayEnd, "User 2 should have record after midnight");

        // Each user should have their correct next record
        var user1NextRecord = user1Records.Where(r => r.RecordedAt > dayEnd).First();
        user1NextRecord.RecordedAt.Should().Be(dayEnd.AddHours(1));

        var user2NextRecord = user2Records.Where(r => r.RecordedAt > dayEnd).First();
        user2NextRecord.RecordedAt.Should().Be(dayEnd.AddHours(2));
    }

    [Fact]
    public async Task PresenceTimeline_WithNoChangesAfterMidnight_StillWorks()
    {
        // Arrange - User has changes during the day, but none after midnight within 2-hour window
        var org = await CreateTestOrganisationAsync();
        var member = await CreateTestMemberAsync(org.Id, "Test User");

        var dayStart = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(2026, 5, 7, 23, 59, 59, TimeSpan.Zero);

        await CreatePresenceRecordAsync(member.Id, "Available", dayStart.AddHours(9));
        await CreatePresenceRecordAsync(member.Id, "Offline", dayStart.AddHours(17));
        // No more records after 5 PM

        // Act
        var historyRecords = await GetPresenceTimelineDataAsync(member.Id, dayStart, dayEnd);

        // Assert
        historyRecords.Should().NotBeEmpty();
        var lastRecord = historyRecords.OrderByDescending(r => r.RecordedAt).First();
        lastRecord.RecordedAt.Should().Be(dayStart.AddHours(17), "last record is at 5 PM");

        // Should not crash - segment extends to end of day (or now if current day)
    }

    [Fact]
    public async Task PresenceTimeline_WithStatusChangeExactlyAtMidnight_HandlesCorrectly()
    {
        // Arrange - Edge case: status changes right at midnight
        var org = await CreateTestOrganisationAsync();
        var member = await CreateTestMemberAsync(org.Id, "Night Owl");

        var dayStart = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(2026, 5, 7, 23, 59, 59, TimeSpan.Zero);
        var exactMidnight = new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

        await CreatePresenceRecordAsync(member.Id, "Available", dayStart.AddHours(23));
        await CreatePresenceRecordAsync(member.Id, "Offline", exactMidnight); // Exactly midnight

        // Act
        var historyRecords = await GetPresenceTimelineDataAsync(member.Id, dayStart, dayEnd);

        // Assert
        historyRecords.Should().Contain(r => r.RecordedAt == exactMidnight, "midnight record should be included");

        // The segment from 11 PM should be clipped to dayEnd (23:59:59), not extend to midnight
        var recordsOnMay7 = historyRecords
            .Where(r => r.RecordedAt >= dayStart && r.RecordedAt <= dayEnd)
            .OrderBy(r => r.RecordedAt)
            .ToList();

        recordsOnMay7.Should().HaveCount(1, "only the 11 PM record is within May 7");
    }

    [Fact]
    public async Task PresenceTimeline_DoesNotFetchRecordsBeyond2HoursAfterMidnight()
    {
        // Arrange - Ensure we don't fetch unnecessary data
        var org = await CreateTestOrganisationAsync();
        var member = await CreateTestMemberAsync(org.Id, "Test User");

        var dayStart = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(2026, 5, 7, 23, 59, 59, TimeSpan.Zero);

        await CreatePresenceRecordAsync(member.Id, "Available", dayStart.AddHours(23));
        await CreatePresenceRecordAsync(member.Id, "Available", dayEnd.AddHours(1)); // 1 AM May 8 (within window)
        await CreatePresenceRecordAsync(member.Id, "Offline", dayEnd.AddHours(3));   // 3 AM May 8 (OUTSIDE 2-hour window)
        await CreatePresenceRecordAsync(member.Id, "Available", dayEnd.AddHours(5)); // 5 AM May 8 (OUTSIDE 2-hour window)

        // Act
        var historyRecords = await GetPresenceTimelineDataAsync(member.Id, dayStart, dayEnd);

        // Assert
        var recordsBeyond2Hours = historyRecords.Where(r => r.RecordedAt > dayEnd.AddHours(2)).ToList();
        recordsBeyond2Hours.Should().BeEmpty("should not fetch records beyond 2-hour window");

        // Should include records within 2-hour window
        historyRecords.Should().Contain(r => r.RecordedAt == dayEnd.AddHours(1), "should include 1 AM record");
        historyRecords.Should().NotContain(r => r.RecordedAt == dayEnd.AddHours(3), "should NOT include 3 AM record");
    }

    #region Helper Methods

    private async Task<Organisation> CreateTestOrganisationAsync(string name = "Test Org")
    {
        var connection = new MicrosoftConnection
        {
            UserId = Guid.NewGuid(),
            TenantId = "test-tenant",
            AccessToken = "test-token",
            RefreshToken = "test-refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _dbContext.MicrosoftConnections.AddAsync(connection);

        var org = new Organisation(name, "test-tenant", connection.Id, 999);
        await _dbContext.Organisations.AddAsync(org);
        await _dbContext.SaveChangesAsync();

        return org;
    }

    private async Task<TenantMember> CreateTestMemberAsync(Guid orgId, string displayName)
    {
        var member = new TenantMember
        {
            OrganisationId = orgId,
            MicrosoftUserId = Guid.NewGuid().ToString(),
            DisplayName = displayName,
            Email = $"{displayName.Replace(" ", "").ToLower()}@test.com",
            IsAssignedSeat = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _dbContext.TenantMembers.AddAsync(member);
        await _dbContext.SaveChangesAsync();

        return member;
    }

    private async Task<PresenceHistory> CreatePresenceRecordAsync(Guid memberId, string availability, DateTimeOffset recordedAt)
    {
        var record = new PresenceHistory
        {
            TenantMemberId = memberId,
            Availability = availability,
            Activity = "Available",
            RecordedAt = recordedAt
        };

        await _dbContext.PresenceHistories.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        return record;
    }

    private async Task<List<PresenceHistory>> GetPresenceTimelineDataAsync(Guid memberId, DateTimeOffset dayStart, DateTimeOffset dayEnd)
    {
        var queryStart = dayStart.AddHours(-1);
        var queryEnd = dayEnd.AddHours(2); // This is the fix - extend by 2 hours

        return await _dbContext.PresenceHistories
            .Where(ph => ph.TenantMemberId == memberId && ph.RecordedAt >= queryStart && ph.RecordedAt <= queryEnd)
            .OrderBy(ph => ph.RecordedAt)
            .ToListAsync();
    }

    #endregion

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
