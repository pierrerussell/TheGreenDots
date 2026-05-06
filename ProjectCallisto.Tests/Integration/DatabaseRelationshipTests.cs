using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectCallisto.Domain.Organisations;
using Xunit;

namespace ProjectCallisto.Tests.Integration;

public class DatabaseRelationshipTests : IntegrationTestBase
{
    [Fact]
    public async Task Organisation_WorkingHours_OneToOneRelationship()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var workingHours = new WorkingHours(organisation.Id);
        await DbContext.WorkingHours.AddAsync(workingHours);
        await DbContext.SaveChangesAsync();

        // Act
        var loadedOrg = await DbContext.Organisations
            .Include(o => o.WorkingHours)
            .FirstAsync(o => o.Id == organisation.Id);

        // Assert
        loadedOrg.WorkingHours.Should().NotBeNull();
        loadedOrg.WorkingHours!.Id.Should().Be(workingHours.Id);
        loadedOrg.WorkingHours.OrganisationId.Should().Be(organisation.Id);
    }
    

    [Fact]
    public async Task EmailReportSettings_EmailRecipients_OneToManyRelationship()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var settings = new EmailReportSettings(organisation.Id);
        settings.Recipients.Add(new EmailRecipient(settings.Id, "user1@example.com", "User 1"));
        settings.Recipients.Add(new EmailRecipient(settings.Id, "user2@example.com", "User 2"));
        settings.Recipients.Add(new EmailRecipient(settings.Id, "user3@example.com", "User 3"));
        await DbContext.EmailReportSettings.AddAsync(settings);
        await DbContext.SaveChangesAsync();

        // Act
        var loadedSettings = await DbContext.EmailReportSettings
            .Include(s => s.Recipients)
            .FirstAsync(s => s.Id == settings.Id);

        // Assert
        loadedSettings.Recipients.Should().HaveCount(3);
        loadedSettings.Recipients.Should().Contain(r => r.Email == "user1@example.com");
        loadedSettings.Recipients.Should().Contain(r => r.Email == "user2@example.com");
        loadedSettings.Recipients.Should().Contain(r => r.Email == "user3@example.com");
    }

    [Fact]
    public async Task DeleteOrganisation_CascadesTo_WorkingHours()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var workingHours = new WorkingHours(organisation.Id);
        await DbContext.WorkingHours.AddAsync(workingHours);
        await DbContext.SaveChangesAsync();

        var workingHoursId = workingHours.Id;

        // Act
        DbContext.Organisations.Remove(organisation);
        await DbContext.SaveChangesAsync();

        // Assert
        var deletedWorkingHours = await DbContext.WorkingHours
            .FirstOrDefaultAsync(wh => wh.Id == workingHoursId);
        deletedWorkingHours.Should().BeNull();
    }

    [Fact]
    public async Task DeleteOrganisation_CascadesTo_EmailReportSettings()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var settings = new EmailReportSettings(organisation.Id);
        await DbContext.EmailReportSettings.AddAsync(settings);
        await DbContext.SaveChangesAsync();

        var settingsId = settings.Id;

        // Act
        DbContext.Organisations.Remove(organisation);
        await DbContext.SaveChangesAsync();

        // Assert
        var deletedSettings = await DbContext.EmailReportSettings
            .FirstOrDefaultAsync(s => s.Id == settingsId);
        deletedSettings.Should().BeNull();
    }

    [Fact]
    public async Task DeleteOrganisation_CascadesTo_EmailRecipients()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var settings = new EmailReportSettings(organisation.Id);
        settings.Recipients.Add(new EmailRecipient(settings.Id, "test@example.com", "Test User"));
        await DbContext.EmailReportSettings.AddAsync(settings);
        await DbContext.SaveChangesAsync();

        var recipientId = settings.Recipients.First().Id;

        // Act
        DbContext.Organisations.Remove(organisation);
        await DbContext.SaveChangesAsync();

        // Assert
        var deletedRecipient = await DbContext.EmailRecipients
            .FirstOrDefaultAsync(r => r.Id == recipientId);
        deletedRecipient.Should().BeNull();
    }

    [Fact]
    public async Task DeleteEmailReportSettings_CascadesTo_EmailRecipients()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var settings = new EmailReportSettings(organisation.Id);
        settings.Recipients.Add(new EmailRecipient(settings.Id, "user1@example.com", "User 1"));
        settings.Recipients.Add(new EmailRecipient(settings.Id, "user2@example.com", "User 2"));
        await DbContext.EmailReportSettings.AddAsync(settings);
        await DbContext.SaveChangesAsync();

        var recipientIds = settings.Recipients.Select(r => r.Id).ToList();

        // Act
        DbContext.EmailReportSettings.Remove(settings);
        await DbContext.SaveChangesAsync();

        // Assert
        foreach (var recipientId in recipientIds)
        {
            var deletedRecipient = await DbContext.EmailRecipients
                .FirstOrDefaultAsync(r => r.Id == recipientId);
            deletedRecipient.Should().BeNull();
        }
    }

    [Fact]
    public async Task Organisation_CanHaveMultipleSettings_ButNotRequired()
    {
        // Arrange & Act
        var organisation = await CreateTestOrganisationAsync();

        // Assert
        var loadedOrg = await DbContext.Organisations
            .Include(o => o.WorkingHours)
            .Include(o => o.EmailReportSettings)
            .FirstAsync(o => o.Id == organisation.Id);

        loadedOrg.WorkingHours.Should().BeNull();
        loadedOrg.EmailReportSettings.Should().BeEmpty(); // EmailReportSettings is initialized as empty list, not null
    }

    [Fact]
    public async Task Organisation_CanHaveBoth_WorkingHoursAndEmailSettings()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var workingHours = new WorkingHours(organisation.Id);
        var settings = new EmailReportSettings(organisation.Id);

        await DbContext.WorkingHours.AddAsync(workingHours);
        await DbContext.EmailReportSettings.AddAsync(settings);
        await DbContext.SaveChangesAsync();

        // Act
        var loadedOrg = await DbContext.Organisations
            .Include(o => o.WorkingHours)
            .Include(o => o.EmailReportSettings)
            .FirstAsync(o => o.Id == organisation.Id);

        // Assert
        loadedOrg.WorkingHours.Should().NotBeNull();
        loadedOrg.EmailReportSettings.Should().NotBeNull();
    }

    // NOTE: These tests are commented out because InMemory database doesn't enforce unique constraints
    // the same way as SQL Server. In production, these constraints are enforced by the database.

    // [Fact]
    // public async Task WorkingHours_OrganisationId_IsUnique()
    // {
    //     // This test would pass with a real SQL Server database but not with InMemory
    // }

    // [Fact]
    // public async Task EmailReportSettings_OrganisationId_IsUnique()
    // {
    //     // This test would pass with a real SQL Server database but not with InMemory
    // }

    // [Fact]
    // public async Task EmailRecipients_Email_IsUniquePerSettings()
    // {
    //     // This test would pass with a real SQL Server database but not with InMemory
    // }

    [Fact]
    public async Task EmailRecipients_SameEmail_DifferentSettings_IsAllowed()
    {
        // Arrange
        var organisation1 = await CreateTestOrganisationAsync("Org 1");
        var organisation2 = await CreateTestOrganisationAsync("Org 2");

        var settings1 = new EmailReportSettings(organisation1.Id);
        var settings2 = new EmailReportSettings(organisation2.Id);

        settings1.Recipients.Add(new EmailRecipient(settings1.Id, "shared@example.com", "User 1"));
        settings2.Recipients.Add(new EmailRecipient(settings2.Id, "shared@example.com", "User 2"));

        await DbContext.EmailReportSettings.AddAsync(settings1);
        await DbContext.EmailReportSettings.AddAsync(settings2);

        // Act
        var action = async () => await DbContext.SaveChangesAsync();

        // Assert
        await action.Should().NotThrowAsync();

        var recipients = await DbContext.EmailRecipients
            .Where(r => r.Email == "shared@example.com")
            .ToListAsync();
        recipients.Should().HaveCount(2);
    }

    [Fact]
    public async Task WorkingHours_DefaultValues_ArePersisted()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var workingHours = new WorkingHours(organisation.Id);
        await DbContext.WorkingHours.AddAsync(workingHours);
        await DbContext.SaveChangesAsync();

        // Clear tracking
        DbContext.ChangeTracker.Clear();

        // Act
        var loaded = await DbContext.WorkingHours.FirstAsync(wh => wh.Id == workingHours.Id);

        // Assert
        loaded.StartTime.Should().Be(new TimeOnly(9, 0));
        loaded.EndTime.Should().Be(new TimeOnly(17, 0));
        loaded.WorkingDays.Should().Be(
            WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
            WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
            WorkingDaysFlags.Friday);
    }

    [Fact]
    public async Task EmailReportSettings_DefaultValues_ArePersisted()
    {
        // Arrange
        var organisation = await CreateTestOrganisationAsync();
        var settings = new EmailReportSettings(organisation.Id);
        await DbContext.EmailReportSettings.AddAsync(settings);
        await DbContext.SaveChangesAsync();

        // Clear tracking
        DbContext.ChangeTracker.Clear();

        // Act
        var loaded = await DbContext.EmailReportSettings.FirstAsync(s => s.Id == settings.Id);

        // Assert
        loaded.IsEnabled.Should().BeFalse();
        loaded.Frequency.Should().Be(ReportFrequency.Weekly);
        loaded.DayOfWeek.Should().Be(DayOfWeek.Monday);
        loaded.DayOfMonth.Should().Be(1);
        loaded.TimeOfDay.Should().Be(new TimeOnly(9, 0));
        loaded.LastSentAt.Should().BeNull();
    }
}
