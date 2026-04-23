using Microsoft.EntityFrameworkCore;

namespace Project400API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<StoredCredential> StoredCredentials => Set<StoredCredential>();
    public DbSet<UnlockToken> UnlockTokens => Set<UnlockToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Keycard> Keycards => Set<Keycard>();
    public DbSet<Door> Doors => Set<Door>();
    public DbSet<UnlockRequest> UnlockRequests => Set<UnlockRequest>();
public DbSet<TailgateAlert> TailgateAlerts => Set<TailgateAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.BleDeviceId).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // StoredCredential configuration
        modelBuilder.Entity<StoredCredential>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CredentialId).IsUnique();
            entity.Property(e => e.CredentialId).IsRequired();
            entity.Property(e => e.PublicKey).IsRequired();
            entity.Property(e => e.UserHandle).IsRequired();
            entity.Property(e => e.CredType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Credentials)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UnlockToken configuration
        modelBuilder.Entity<UnlockToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DeviceId, e.ExpiresAt, e.Consumed });
            entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Result).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DeviceId).HasMaxLength(100);
        });

        // Keycard configuration
        modelBuilder.Entity<Keycard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CardUid).IsUnique();
            entity.Property(e => e.CardUid).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Door configuration
        modelBuilder.Entity<Door>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId).IsUnique();
            entity.Property(e => e.DoorName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // UnlockRequest configuration
        modelBuilder.Entity<UnlockRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Status, e.ExpiresAt });
            entity.Property(e => e.Challenge).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Door)
                .WithMany()
                .HasForeignKey(e => e.DoorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TailgateAlert configuration
        modelBuilder.Entity<TailgateAlert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DeviceId);
            entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CameraDeviceId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.ReviewedBy).HasMaxLength(200);
            entity.Property(e => e.ImageUrl).HasMaxLength(2000);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

    }
}
