using Microsoft.EntityFrameworkCore;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.Domain.Users;

namespace ProjectCallisto.EfCore;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Organisation> Organisations { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<OrganisationUser> OrganisationUsers { get; set; }
    public DbSet<MicrosoftConnection> MicrosoftConnections { get; set; }
    public DbSet<TenantMember> TenantMembers { get; set; }
    public DbSet<PresenceHistory> PresenceHistories { get; set; }
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
                builder.Navigation(o => o.Subscription).AutoInclude();
            }
        );

        modelBuilder.Entity<Subscription>(builder =>
        {
            builder.ToTable("Subscriptions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Status).IsRequired();
            builder.Property(x => x.PaidSeats).IsRequired();
            builder.Property(x => x.CreatedAt).IsRequired();
            
            builder.HasOne(s => s.Organisation)
                .WithOne(s => s.Subscription)
                .HasForeignKey<Subscription>(s => s.OrganisationId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

        });

        modelBuilder.Entity<OrganisationUser>(builder =>
        {
            builder.HasKey(x => new { x.OrganisationId, x.UserId });
            builder.Property(x => x.Role)
                .HasConversion<string>()
                .IsRequired();
            builder.HasOne(ou => ou.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(ou => ou.Organisation)
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
            builder.HasOne(tm => tm.Organisation)
                .WithMany()
                .HasForeignKey(x => x.OrganisationId);
            builder.HasIndex(x => new { x.OrganisationId, x.MicrosoftUserId }).IsUnique();
        });

        modelBuilder.Entity<PresenceHistory>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Availability)
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(x => x.Activity)
                .HasMaxLength(64)
                .IsRequired();
            builder.HasOne<TenantMember>()
                .WithMany()
                .HasForeignKey(x => x.TenantMemberId);
            // Index for querying a member's history chronologically
            builder.HasIndex(x => new { x.TenantMemberId, x.RecordedAt });
        });

    }
    
}