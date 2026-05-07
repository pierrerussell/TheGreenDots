namespace ProjectCallisto.Domain.Organisations;

public class MicrosoftConnection
{
    public Guid Id { get; set; }
    // User who OAuthed to microsoft to get this token
    public Guid UserId { get; set; }
    // Microsoft entra tenant id that this user OAuthed to
    public string TenantId { get; set; } = null!;

    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public interface IMicrosoftConnectionRepository 
{
    Task<MicrosoftConnection?> FindAsync(Guid guid, CancellationToken ct = default(CancellationToken));
    
    Task SaveChangesAsync(CancellationToken ct = default(CancellationToken));
}