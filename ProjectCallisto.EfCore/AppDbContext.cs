using Microsoft.EntityFrameworkCore;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.Domain.Users;

namespace ProjectCallisto.EfCore;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Organisation> Organisations { get; set; }
    public DbSet<OrganisationUser> OrganisationUsers { get; set; }
    public DbSet<MicrosoftConnection> MicrosoftConnections { get; set; }
    public DbSet<TenantMember> TenantMembers { get; set; }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Email)
                .HasMaxLength(128)
                .IsRequired();
            builder.Property(x => x.Name)
                .HasMaxLength(128);
            builder.Property(x => x.SubjectId)
                .HasMaxLength(256)
                .IsRequired();
            builder.HasIndex(x => x.SubjectId).IsUnique();
        });

        modelBuilder.Entity<Organisation>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Name)
                    .HasMaxLength(256);
                builder.Property(x => x.TenantId)
                    .HasMaxLength(256);
                builder.HasOne<MicrosoftConnection>()
                    .WithMany()
                    .HasForeignKey(x => x.ActiveConnectionId);
            }
        );

        modelBuilder.Entity<OrganisationUser>(builder =>
        {
            builder.HasKey(x => new { x.OrganisationId, x.UserId });
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.HasOne<Organisation>()
                .WithMany()
                .HasForeignKey(x => x.OrganisationId)
                .OnDelete(DeleteBehavior.NoAction);

        });
            
        modelBuilder.Entity<MicrosoftConnection>(builder =>
        {
            builder.Property(x => x.TenantId)
                .HasMaxLength(256);
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId);
            builder.HasKey(x => x.Id);
        });

        modelBuilder.Entity<TenantMember>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MicrosoftUserId)
                .HasMaxLength(256)
                .IsRequired();
            builder.Property(x => x.DisplayName)
                .HasMaxLength(256)
                .IsRequired();
            builder.Property(x => x.Email)
                .HasMaxLength(256);
            builder.Property(x => x.JobTitle)
                .HasMaxLength(256);
            builder.HasOne<Organisation>()
                .WithMany()
                .HasForeignKey(x => x.OrganisationId);
            builder.HasIndex(x => new { x.OrganisationId, x.MicrosoftUserId }).IsUnique();
        });

    }
    
}