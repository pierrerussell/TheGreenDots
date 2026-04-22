using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.EfCore.Microsoft;

public class MicrosoftConnectionRepository : IMicrosoftConnectionRepository
{
    private readonly AppDbContext _dbContext;
    
    public MicrosoftConnectionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<MicrosoftConnection?> FindAsync(Guid guid, CancellationToken ct = default(CancellationToken))
    {
        return await _dbContext.MicrosoftConnections.FindAsync([guid], ct);
        
    }

    public Task SaveChangesAsync(CancellationToken ct = default(CancellationToken))
    {
        return _dbContext.SaveChangesAsync(ct);
    }
}