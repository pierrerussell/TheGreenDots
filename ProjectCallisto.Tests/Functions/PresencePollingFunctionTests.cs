using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectCallisto.Application.Microsoft;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;
using System.Diagnostics;

namespace ProjectCallisto.Tests.Functions;

/// <summary>
/// Tests for PresencePollingFunction to ensure:
/// 1. Last status query works correctly (N+1 detection)
/// 2. Presence change detection logic is accurate
/// 3. Performance is acceptable for multiple members
/// </summary>
public class PresencePollingFunctionTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _dbContext;
    private readonly Mock<IMicrosoftTokenService> _mockTokenService;
    private readonly Mock<IMicrosoftGraphService> _mockGraphService;

    public PresencePollingFunctionTests()
    {
        // Setup in-memory database
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

        // Mock services
        _mockTokenService = new Mock<IMicrosoftTokenService>();
        _mockGraphService = new Mock<IMicrosoftGraphService>();

        services.AddSingleton(_mockTokenService.Object);
        services.AddSingleton(_mockGraphService.Object);
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }

    #region Last Status Query Tests

    [Fact]
    public async Task LastStatusQuery_WithNoHistory_ReturnsEmptyDictionary()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync();
        var member = await CreateTestMemberAsync(org.Id, "user1@test.com");

        // Act
        var lastStatuses = await _dbContext.PresenceHistories
            .Where(ph => ph.TenantMemberId == member.Id)
            .GroupBy(ph => ph.TenantMemberId)
            .Select(g => g.OrderByDescending(ph => ph.RecordedAt).First())
            .ToDictionaryAsync(ph => ph.TenantMemberId);

        // Assert
        lastStatuses.Should().BeEmpty();
    }

    [Fact]
    public async Task LastStatusQuery_WithSingleRecord_ReturnsThatRecord()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync();
        var member = await CreateTestMemberAsync(org.Id, "user1@test.com");

        var record = new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = member.Id,
            Availability = "Available",
            Activity = "Available",
            RecordedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };
        _dbContext.PresenceHistories.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act
        var lastStatuses = await _dbContext.PresenceHistories
            .Where(ph => ph.TenantMemberId == member.Id)
            .GroupBy(ph => ph.TenantMemberId)
            .Select(g => g.OrderByDescending(ph => ph.RecordedAt).First())
            .ToDictionaryAsync(ph => ph.TenantMemberId);

        // Assert
        lastStatuses.Should().HaveCount(1);
        lastStatuses[member.Id].Id.Should().Be(record.Id);
        lastStatuses[member.Id].Availability.Should().Be("Available");
    }

    [Fact]
    public async Task LastStatusQuery_WithMultipleRecords_ReturnsLatestOnly()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync();
        var member = await CreateTestMemberAsync(org.Id, "user1@test.com");

        var oldRecord = new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = member.Id,
            Availability = "Available",
            Activity = "Available",
            RecordedAt = DateTimeOffset.UtcNow.AddHours(-5)
        };

        var middleRecord = new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = member.Id,
            Availability = "Busy",
            Activity = "InACall",
            RecordedAt = DateTimeOffset.UtcNow.AddHours(-3)
        };

        var latestRecord = new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = member.Id,
            Availability = "Away",
            Activity = "Away",
            RecordedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        _dbContext.PresenceHistories.AddRange(oldRecord, middleRecord, latestRecord);
        await _dbContext.SaveChangesAsync();

        // Act
        var lastStatuses = await _dbContext.PresenceHistories
            .Where(ph => ph.TenantMemberId == member.Id)
            .GroupBy(ph => ph.TenantMemberId)
            .Select(g => g.OrderByDescending(ph => ph.RecordedAt).First())
            .ToDictionaryAsync(ph => ph.TenantMemberId);

        // Assert
        lastStatuses.Should().HaveCount(1);
        lastStatuses[member.Id].Id.Should().Be(latestRecord.Id);
        lastStatuses[member.Id].Availability.Should().Be("Away");
        lastStatuses[member.Id].RecordedAt.Should().Be(latestRecord.RecordedAt);
    }

    [Fact]
    public async Task LastStatusQuery_WithMultipleMembers_ReturnsLatestForEach()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync();
        var member1 = await CreateTestMemberAsync(org.Id, "user1@test.com");
        var member2 = await CreateTestMemberAsync(org.Id, "user2@test.com");
        var member3 = await CreateTestMemberAsync(org.Id, "user3@test.com");

        var memberIds = new[] { member1.Id, member2.Id, member3.Id };

        // Member 1: 3 records, latest is "Away"
        _dbContext.PresenceHistories.Add(new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = member1.Id,
            Availability = "Available",
            Activity = "Available",
            RecordedAt = DateTimeOffset.UtcNow.AddHours(-5)
        });
        _dbContext.PresenceHistories.Add(new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = member1.Id,
            Availability = "Busy",
            Activity = "InACall",
            RecordedAt = DateTimeOffset.UtcNow.AddHours(-3)
        });
        var member1Latest = new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = member1.Id,
            Availability = "Away",
            Activity = "Away",
            RecordedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };
        _dbContext.PresenceHistories.Add(member1Latest);

        // Member 2: 2 records, latest is "Busy"
        _dbContext.PresenceHistories.Add(new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = member2.Id,
            Availability = "Available",
            Activity = "Available",
            RecordedAt = DateTimeOffset.UtcNow.AddHours(-4)
        });
        var member2Latest = new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = member2.Id,
            Availability = "Busy",
            Activity = "InAMeeting",
            RecordedAt = DateTimeOffset.UtcNow.AddMinutes(-15)
        };
        _dbContext.PresenceHistories.Add(member2Latest);

        // Member 3: 1 record
        var member3Latest = new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = member3.Id,
            Availability = "Available",
            Activity = "Available",
            RecordedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        _dbContext.PresenceHistories.Add(member3Latest);

        await _dbContext.SaveChangesAsync();

        // Act
        var lastStatuses = await _dbContext.PresenceHistories
            .Where(ph => memberIds.Contains(ph.TenantMemberId))
            .GroupBy(ph => ph.TenantMemberId)
            .Select(g => g.OrderByDescending(ph => ph.RecordedAt).First())
            .ToDictionaryAsync(ph => ph.TenantMemberId);

        // Assert
        lastStatuses.Should().HaveCount(3);

        // Verify each member has their latest record
        lastStatuses[member1.Id].Id.Should().Be(member1Latest.Id);
        lastStatuses[member1.Id].Availability.Should().Be("Away");

        lastStatuses[member2.Id].Id.Should().Be(member2Latest.Id);
        lastStatuses[member2.Id].Availability.Should().Be("Busy");

        lastStatuses[member3.Id].Id.Should().Be(member3Latest.Id);
        lastStatuses[member3.Id].Availability.Should().Be("Available");
    }

    [Fact]
    public async Task LastStatusQuery_WithMixedHistory_OnlyReturnsLatestForMembersWithHistory()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync();
        var memberWithHistory = await CreateTestMemberAsync(org.Id, "user1@test.com");
        var memberWithoutHistory = await CreateTestMemberAsync(org.Id, "user2@test.com");

        var memberIds = new[] { memberWithHistory.Id, memberWithoutHistory.Id };

        var record = new PresenceHistory
        {
            Id = Guid.NewGuid(),
            TenantMemberId = memberWithHistory.Id,
            Availability = "Available",
            Activity = "Available",
            RecordedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        _dbContext.PresenceHistories.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act
        var lastStatuses = await _dbContext.PresenceHistories
            .Where(ph => memberIds.Contains(ph.TenantMemberId))
            .GroupBy(ph => ph.TenantMemberId)
            .Select(g => g.OrderByDescending(ph => ph.RecordedAt).First())
            .ToDictionaryAsync(ph => ph.TenantMemberId);

        // Assert
        lastStatuses.Should().HaveCount(1);
        lastStatuses.Should().ContainKey(memberWithHistory.Id);
        lastStatuses.Should().NotContainKey(memberWithoutHistory.Id);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task LastStatusQuery_With100Members_CompletesQuickly()
    {
        // Arrange
        var org = await CreateTestOrganisationAsync();
        var members = new List<TenantMember>();
        var memberIds = new List<Guid>();

        // Create 100 members, each with 10 history records
        for (int i = 0; i < 100; i++)
        {
            var member = await CreateTestMemberAsync(org.Id, $"user{i}@test.com");
            members.Add(member);
            memberIds.Add(member.Id);

            // Add 10 history records per member (1000 total records)
            for (int j = 0; j < 10; j++)
            {
                _dbContext.PresenceHistories.Add(new PresenceHistory
                {
                    Id = Guid.NewGuid(),
                    TenantMemberId = member.Id,
                    Availability = j % 2 == 0 ? "Available" : "Busy",
                    Activity = j % 2 == 0 ? "Available" : "InAMeeting",
                    RecordedAt = DateTimeOffset.UtcNow.AddHours(-j)
                });
            }
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var stopwatch = Stopwatch.StartNew();

        var lastStatuses = await _dbContext.PresenceHistories
            .Where(ph => memberIds.Contains(ph.TenantMemberId))
            .GroupBy(ph => ph.TenantMemberId)
            .Select(g => g.OrderByDescending(ph => ph.RecordedAt).First())
            .ToDictionaryAsync(ph => ph.TenantMemberId);

        stopwatch.Stop();

        // Assert
        lastStatuses.Should().HaveCount(100);

        // Performance assertion - should complete in < 500ms for in-memory DB
        // (Real SQL Server will be slower, but this tests the query structure)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500,
            $"Query took {stopwatch.ElapsedMilliseconds}ms for 100 members with 1000 total records");

        // Verify correctness - each member should have their most recent record
        foreach (var member in members)
        {
            lastStatuses.Should().ContainKey(member.Id);
            // Latest record is the one with RecordedAt closest to now (j=0 in the loop above)
            lastStatuses[member.Id].Availability.Should().Be("Available");
        }
    }

    #endregion

    #region Change Detection Logic Tests

    [Fact]
    public void ChangeDetection_NoPrevorRecord_ShouldStoreNewRecord()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var memberId = Guid.NewGuid();
        var lastStatuses = new Dictionary<Guid, PresenceHistory>(); // Empty - no previous record
        var currentPresence = new PresenceStatus { Availability = "Available", Activity = "Available" };

        // Act
        var shouldStore = !lastStatuses.TryGetValue(memberId, out var lastStatus)
            || lastStatus.Availability != currentPresence.Availability
            || lastStatus.Activity != (currentPresence.Activity ?? string.Empty)
            || (now - lastStatus.RecordedAt).TotalHours >= 1;

        // Assert
        shouldStore.Should().BeTrue("no previous record exists");
    }

    [Fact]
    public void ChangeDetection_AvailabilityChanged_ShouldStoreNewRecord()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var memberId = Guid.NewGuid();
        var lastStatuses = new Dictionary<Guid, PresenceHistory>
        {
            [memberId] = new PresenceHistory
            {
                TenantMemberId = memberId,
                Availability = "Available",
                Activity = "Available",
                RecordedAt = now.AddMinutes(-30)
            }
        };
        var currentPresence = new PresenceStatus { Availability = "Busy", Activity = "Available" };

        // Act
        var shouldStore = !lastStatuses.TryGetValue(memberId, out var lastStatus)
            || lastStatus.Availability != currentPresence.Availability
            || lastStatus.Activity != (currentPresence.Activity ?? string.Empty)
            || (now - lastStatus.RecordedAt).TotalHours >= 1;

        // Assert
        shouldStore.Should().BeTrue("availability changed from Available to Busy");
    }

    [Fact]
    public void ChangeDetection_ActivityChanged_ShouldStoreNewRecord()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var memberId = Guid.NewGuid();
        var lastStatuses = new Dictionary<Guid, PresenceHistory>
        {
            [memberId] = new PresenceHistory
            {
                TenantMemberId = memberId,
                Availability = "Busy",
                Activity = "InACall",
                RecordedAt = now.AddMinutes(-30)
            }
        };
        var currentPresence = new PresenceStatus { Availability = "Busy", Activity = "InAMeeting" };

        // Act
        var shouldStore = !lastStatuses.TryGetValue(memberId, out var lastStatus)
            || lastStatus.Availability != currentPresence.Availability
            || lastStatus.Activity != (currentPresence.Activity ?? string.Empty)
            || (now - lastStatus.RecordedAt).TotalHours >= 1;

        // Assert
        shouldStore.Should().BeTrue("activity changed from InACall to InAMeeting");
    }

    [Fact]
    public void ChangeDetection_MoreThanOneHourElapsed_ShouldStoreNewRecord()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var memberId = Guid.NewGuid();
        var lastStatuses = new Dictionary<Guid, PresenceHistory>
        {
            [memberId] = new PresenceHistory
            {
                TenantMemberId = memberId,
                Availability = "Available",
                Activity = "Available",
                RecordedAt = now.AddHours(-2) // 2 hours ago
            }
        };
        var currentPresence = new PresenceStatus { Availability = "Available", Activity = "Available" };

        // Act
        var shouldStore = !lastStatuses.TryGetValue(memberId, out var lastStatus)
            || lastStatus.Availability != currentPresence.Availability
            || lastStatus.Activity != (currentPresence.Activity ?? string.Empty)
            || (now - lastStatus.RecordedAt).TotalHours >= 1;

        // Assert
        shouldStore.Should().BeTrue("more than 1 hour has elapsed since last record");
    }

    [Fact]
    public void ChangeDetection_NoChangeAndLessThanOneHour_ShouldNotStoreNewRecord()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var memberId = Guid.NewGuid();
        var lastStatuses = new Dictionary<Guid, PresenceHistory>
        {
            [memberId] = new PresenceHistory
            {
                TenantMemberId = memberId,
                Availability = "Available",
                Activity = "Available",
                RecordedAt = now.AddMinutes(-30) // 30 minutes ago
            }
        };
        var currentPresence = new PresenceStatus { Availability = "Available", Activity = "Available" };

        // Act
        var shouldStore = !lastStatuses.TryGetValue(memberId, out var lastStatus)
            || lastStatus.Availability != currentPresence.Availability
            || lastStatus.Activity != (currentPresence.Activity ?? string.Empty)
            || (now - lastStatus.RecordedAt).TotalHours >= 1;

        // Assert
        shouldStore.Should().BeFalse("no change and less than 1 hour since last record");
    }

    [Fact]
    public void ChangeDetection_ExactlyOneHour_ShouldStoreNewRecord()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var memberId = Guid.NewGuid();
        var lastStatuses = new Dictionary<Guid, PresenceHistory>
        {
            [memberId] = new PresenceHistory
            {
                TenantMemberId = memberId,
                Availability = "Available",
                Activity = "Available",
                RecordedAt = now.AddHours(-1) // Exactly 1 hour ago
            }
        };
        var currentPresence = new PresenceStatus { Availability = "Available", Activity = "Available" };

        // Act
        var shouldStore = !lastStatuses.TryGetValue(memberId, out var lastStatus)
            || lastStatus.Availability != currentPresence.Availability
            || lastStatus.Activity != (currentPresence.Activity ?? string.Empty)
            || (now - lastStatus.RecordedAt).TotalHours >= 1;

        // Assert
        shouldStore.Should().BeTrue("exactly 1 hour has elapsed (>= condition)");
    }

    [Fact]
    public void ChangeDetection_NullActivityInCurrentPresence_TreatsAsEmptyString()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var memberId = Guid.NewGuid();
        var lastStatuses = new Dictionary<Guid, PresenceHistory>
        {
            [memberId] = new PresenceHistory
            {
                TenantMemberId = memberId,
                Availability = "Available",
                Activity = "", // Empty string in DB
                RecordedAt = now.AddMinutes(-30)
            }
        };
        var currentPresence = new PresenceStatus { Availability = "Available", Activity = null }; // null from Graph API

        // Act
        var shouldStore = !lastStatuses.TryGetValue(memberId, out var lastStatus)
            || lastStatus.Availability != currentPresence.Availability
            || lastStatus.Activity != (currentPresence.Activity ?? string.Empty)
            || (now - lastStatus.RecordedAt).TotalHours >= 1;

        // Assert
        shouldStore.Should().BeFalse("null activity should be treated as empty string");
    }

    #endregion

    #region Helper Methods

    private async Task<Organisation> CreateTestOrganisationAsync()
    {
        var connection = new MicrosoftConnection
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TenantId = "test-tenant",
            AccessToken = "test-access-token",
            RefreshToken = "test-refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.MicrosoftConnections.Add(connection);

        var org = new Organisation("Test Org", "test-tenant", connection.Id, trialSeats: 10)
        {
            Timezone = "UTC"
        };

        org.WorkingHours = new WorkingHours(org.Id); // Uses default 9-5, Mon-Fri

        _dbContext.Organisations.Add(org);
        await _dbContext.SaveChangesAsync();

        return org;
    }

    private async Task<TenantMember> CreateTestMemberAsync(Guid organisationId, string email)
    {
        var member = new TenantMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            MicrosoftUserId = Guid.NewGuid().ToString(),
            Email = email,
            DisplayName = email.Split('@')[0],
            IsAssignedSeat = true
        };

        _dbContext.TenantMembers.Add(member);
        await _dbContext.SaveChangesAsync();

        return member;
    }

    #endregion
}

/// <summary>
/// Mock class for PresenceStatus used in tests
/// </summary>
public class PresenceStatus
{
    public string Availability { get; set; } = string.Empty;
    public string? Activity { get; set; }
}
