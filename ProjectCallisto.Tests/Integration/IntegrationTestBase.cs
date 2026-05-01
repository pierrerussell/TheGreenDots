using Microsoft.EntityFrameworkCore;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.Domain.Users;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.Tests.Integration;

/// <summary>
/// Base class for integration tests that provides an in-memory database context.
/// </summary>
public class IntegrationTestBase : IDisposable
{
    protected readonly AppDbContext DbContext;

    public IntegrationTestBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        DbContext = new AppDbContext(options);
    }

    protected async Task<Organisation> CreateTestOrganisationAsync(
        string name = "Test Organisation",
        string? country = null,
        string? timezone = null)
    {
        var connection = new MicrosoftConnection
        {
            UserId = Guid.NewGuid(),
            TenantId = "test-tenant-id",
            AccessToken = "test-access-token",
            RefreshToken = "test-refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow
        };
        await DbContext.MicrosoftConnections.AddAsync(connection);

        var organisation = new Organisation(
            name: name,
            microsoftTenantId: "test-tenant-id",
            connectionId: connection.Id,
            trialSeats: 999)
        {
            Country = country,
            Timezone = timezone
        };

        await DbContext.Organisations.AddAsync(organisation);
        await DbContext.SaveChangesAsync();

        return organisation;
    }

    protected async Task<User> CreateTestUserAsync(string subjectId = "test-subject-id")
    {
        var user = new User
        {
            SubjectId = subjectId,
            Email = "test@example.com",
            Name = "Test User",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await DbContext.Users.AddAsync(user);
        await DbContext.SaveChangesAsync();

        return user;
    }

    public void Dispose()
    {
        DbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
