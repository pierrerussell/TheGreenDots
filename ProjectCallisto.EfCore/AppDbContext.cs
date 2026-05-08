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
    public DbSet<WorkingHours> WorkingHours { get; set; }
    public DbSet<EmailReportSettings> EmailReportSettings { get; set; }
    public DbSet<EmailRecipient> EmailRecipients { get; set; }
    public DbSet<WebhookEvent> WebhookEvents { get; set; }
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

                // Timezone & Country
                builder.Property(x => x.Country)
                    .HasMaxLength(2); // ISO 3166-2 (2 chars)
                builder.Property(x => x.CountryDetectedFrom)
                    .HasMaxLength(50);
                builder.Property(x => x.Timezone)
                    .HasMaxLength(100); // IANA timezones can be long
                builder.Property(x => x.TimezoneDetectedFrom)
                    .HasMaxLength(50);

                // One-to-one relationships
                builder.HasOne(o => o.WorkingHours)
                    .WithOne(wh => wh.Organisation)
                    .HasForeignKey<WorkingHours>(wh => wh.OrganisationId)
                    .OnDelete(DeleteBehavior.Cascade);

                // One-to-many relationship for EmailReportSettings
                builder.HasMany(o => o.EmailReportSettings)
                    .WithOne(ers => ers.Organisation)
                    .HasForeignKey(ers => ers.OrganisationId)
                    .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<WorkingHours>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.StartTime)
                .IsRequired();
            builder.Property(x => x.EndTime)
                .IsRequired();
            builder.Property(x => x.WorkingDays)
                .HasConversion<int>() // Store as int in DB
                .IsRequired();
            builder.HasIndex(x => x.OrganisationId)
                .IsUnique(); // One-to-one constraint
        });

        modelBuilder.Entity<EmailReportSettings>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.IsEnabled)
                .IsRequired();
            builder.Property(x => x.Frequency)
                .HasConversion<int>()
                .IsRequired();
            builder.Property(x => x.TimeOfDay)
                .IsRequired();
            // Non-unique index for query performance (organisation can have multiple settings)
            builder.HasIndex(x => x.OrganisationId);
            // For efficient querying of enabled reports by frequency
            builder.HasIndex(x => new { x.IsEnabled, x.Frequency, x.DayOfWeek });
            // Navigation to recipients
            builder.HasMany(ers => ers.Recipients)
                .WithOne(r => r.EmailReportSettings)
                .HasForeignKey(r => r.EmailReportSettingsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailRecipient>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Email)
                .HasMaxLength(256)
                .IsRequired();
            builder.Property(x => x.Name)
                .HasMaxLength(256);
            // Prevent duplicate emails per settings
            builder.HasIndex(x => new { x.EmailReportSettingsId, x.Email })
                .IsUnique();
        });

        modelBuilder.Entity<WebhookEvent>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.StripeEventId)
                .HasMaxLength(256)
                .IsRequired();
            builder.Property(x => x.EventType)
                .HasMaxLength(128)
                .IsRequired();
            builder.Property(x => x.ProcessedAt)
                .IsRequired();
            // Unique constraint - prevents duplicate event processing
            builder.HasIndex(x => x.StripeEventId)
                .IsUnique();
        });

    }
    
}