using Microsoft.EntityFrameworkCore;
using Divvy.Api.Models;

namespace Divvy.Api.Data;

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

    // ── Divvy domain ──────────────────────────────────────────────────────────
    public DbSet<ExpenseCycle>     ExpenseCycles     { get; set; } = null!;
    public DbSet<CycleMember>      CycleMembers      { get; set; } = null!;
    public DbSet<Expense>          Expenses          { get; set; } = null!;
    public DbSet<MemberObligation> MemberObligations { get; set; } = null!;
    public DbSet<Payment>          Payments          { get; set; } = null!;

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

        // Divvy domain
        ConfigureExpenseCycleEntity(modelBuilder);
        ConfigureCycleMemberEntity(modelBuilder);
        ConfigureExpenseEntity(modelBuilder);
        ConfigureMemberObligationEntity(modelBuilder);
        ConfigurePaymentEntity(modelBuilder);
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
            entity.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
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

    // ── Divvy domain configure methods ────────────────────────────────────────

    private static void ConfigureExpenseCycleEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseCycle>(entity =>
        {
            entity.ToTable("ExpenseCycles");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(150);
            entity.HasOne<User>().WithMany().HasForeignKey(c => c.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCycleMemberEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CycleMember>(entity =>
        {
            entity.ToTable("CycleMembers");
            entity.HasKey(m => new { m.ExpenseCycleId, m.UserId });
            entity.HasOne<ExpenseCycle>().WithMany().HasForeignKey(m => m.ExpenseCycleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureExpenseEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.HasOne<ExpenseCycle>().WithMany().HasForeignKey(e => e.ExpenseCycleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.PaidByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMemberObligationEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MemberObligation>(entity =>
        {
            entity.ToTable("MemberObligations");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.AmountOwed).HasColumnType("decimal(18,2)");
            entity.HasOne<Expense>().WithMany().HasForeignKey(o => o.ExpenseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePaymentEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            entity.HasOne<ExpenseCycle>().WithMany().HasForeignKey(p => p.ExpenseCycleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(p => p.PayerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(p => p.PayeeId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
