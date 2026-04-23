using Microsoft.EntityFrameworkCore;
using Blueprint1989.Api.Models;

namespace Blueprint1989.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<RoleEntity> RoleEntities { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;
    public DbSet<NotificationTypeEntity> NotificationTypes { get; set; } = null!;
    public DbSet<UserDevice> UserDevices { get; set; } = null!;
    public DbSet<WebAuthnCredential> WebAuthnCredentials { get; set; } = null!;
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = null!;
    public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; } = null!;
    public DbSet<AppConfigEntry> AppConfigEntries { get; set; } = null!;
    public DbSet<UserNotificationPreference> UserNotificationPreferences { get; set; } = null!;

    // ── Feedback ───────────────────────────────────────────────────────────
    public DbSet<FeatureBugReport>         FeatureBugReports         { get; set; } = null!;
    public DbSet<FeatureBugReportCategory> FeatureBugReportCategories { get; set; } = null!;

    // ── File storage ──────────────────────────────────────────────────────
    public DbSet<UploadedFile>             UploadedFiles             { get; set; } = null!;

    // ── Email log ─────────────────────────────────────────────────────────
    public DbSet<SentEmail>                SentEmails                { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUserEntity(modelBuilder);
        ConfigureRoleEntity(modelBuilder);
        ConfigureNotificationTypeEntity(modelBuilder);
        ConfigureNotificationEntity(modelBuilder);
        ConfigurePushSubscriptionEntity(modelBuilder);
        ConfigureUserDeviceEntity(modelBuilder);
        ConfigureWebAuthnCredentialEntity(modelBuilder);
        ConfigurePasswordResetTokenEntity(modelBuilder);
        ConfigureEmailVerificationTokenEntity(modelBuilder);
        ConfigureAppConfigEntryEntity(modelBuilder);
        ConfigureUserNotificationPreferenceEntity(modelBuilder);

        // Feedback
        ConfigureFeatureBugReportEntity(modelBuilder);
        ConfigureFeatureBugReportCategoryEntity(modelBuilder);

        // Email log
        ConfigureSentEmailEntity(modelBuilder);

    }

    private static void ConfigureUserEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Role).IsRequired();
        });
    }

    private static void ConfigureRoleEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoleEntity>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(r => r.Name).IsUnique();
        });
    }

    private static void ConfigureNotificationTypeEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationTypeEntity>(entity =>
        {
            entity.ToTable("NotificationTypes");
            entity.HasKey(nt => nt.Id);
            entity.Property(nt => nt.Name).IsRequired().HasMaxLength(100);
        });
    }

    private static void ConfigureNotificationEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(n => n.Id);
            entity.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePushSubscriptionEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.ToTable("PushSubscriptions");
            entity.HasKey(ps => ps.Id);
            entity.HasOne(ps => ps.User).WithMany().HasForeignKey(ps => ps.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(ps => ps.Endpoint).IsRequired();
        });
    }

    private static void ConfigureUserDeviceEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserDevice>(entity =>
        {
            entity.ToTable("UserDevices");
            entity.HasKey(ud => ud.Id);
            entity.HasOne(ud => ud.User).WithMany().HasForeignKey(ud => ud.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureWebAuthnCredentialEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebAuthnCredential>(entity =>
        {
            entity.ToTable("WebAuthnCredentials");
            entity.HasKey(wc => wc.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(wc => wc.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePasswordResetTokenEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens");
            entity.HasKey(t => t.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(t => t.Token).IsRequired();
            entity.HasIndex(t => t.Token).IsUnique();
        });
    }

    private static void ConfigureEmailVerificationTokenEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.ToTable("EmailVerificationTokens");
            entity.HasKey(t => t.Id);
            entity.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(t => t.Token).IsRequired();
            entity.HasIndex(t => t.Token).IsUnique();
        });
    }

    private static void ConfigureAppConfigEntryEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppConfigEntry>(entity =>
        {
            entity.ToTable("AppConfigEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Value).HasMaxLength(4000);
        });
    }

    private static void ConfigureUserNotificationPreferenceEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserNotificationPreference>(entity =>
        {
            entity.ToTable("UserNotificationPreferences");
            entity.HasKey(unp => new { unp.UserId, unp.NotificationTypeId });
            entity.HasOne(unp => unp.User).WithMany().HasForeignKey(unp => unp.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(unp => unp.NotificationType).WithMany().HasForeignKey(unp => unp.NotificationTypeId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    // ── Feedback ───────────────────────────────────────────────────────────

    private static void ConfigureFeatureBugReportEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeatureBugReport>(entity =>
        {
            entity.ToTable("FeatureBugReports");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Title).IsRequired().HasMaxLength(200);
            entity.Property(r => r.Description).IsRequired().HasMaxLength(2000);
            entity.Property(r => r.VersionNumber).HasMaxLength(20);
            entity.HasOne(r => r.SubmittedByUser).WithMany().HasForeignKey(r => r.SubmittedByUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.ImageFile).WithMany().HasForeignKey(r => r.ImageFileId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureFeatureBugReportCategoryEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeatureBugReportCategory>(entity =>
        {
            entity.ToTable("FeatureBugReportCategories");
            entity.HasKey(rc => new { rc.FeatureBugReportId, rc.Category });
            entity.HasOne(rc => rc.Report).WithMany(r => r.Categories).HasForeignKey(rc => rc.FeatureBugReportId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSentEmailEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SentEmail>(entity =>
        {
            entity.ToTable("sent_emails");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ToAddress).IsRequired().HasMaxLength(255).HasColumnName("to_address");
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(500).HasColumnName("subject");
            entity.Property(e => e.HtmlBody).IsRequired().HasColumnType("text").HasColumnName("html_body");
            entity.Property(e => e.PlainBody).HasColumnType("text").HasColumnName("plain_body");
            entity.Property(e => e.BccAddress).HasMaxLength(255).HasColumnName("bcc_address");
            entity.Property(e => e.SentAt).HasColumnName("sent_at");
            entity.Property(e => e.IsSuccess).HasColumnName("is_success");
            entity.Property(e => e.ErrorMessage).HasColumnType("text").HasColumnName("error_message");
            entity.HasIndex(e => e.SentAt).HasDatabaseName("IX_sent_emails_sent_at");
        });
    }

}
