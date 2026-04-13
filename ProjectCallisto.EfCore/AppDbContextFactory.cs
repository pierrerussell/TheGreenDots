using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectCallisto.EfCore;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder  = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer("Server=tcp:thegreendots.database.windows.net,1433;Initial Catalog=thegreendots;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=\"Active Directory Default\";");
        return new AppDbContext(optionsBuilder.Options);
    }
}