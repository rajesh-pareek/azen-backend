using Azen.Domain.Entities.Auth;
using Microsoft.EntityFrameworkCore;

namespace Azen.Infrastructure.Persistence;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    //DbSets - one per entity/table

    public DbSet<User> Users { get; set; }
    public DbSet<OtpRequest> OtpRequests { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // -- Users --
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Phone).HasMaxLength(15).IsRequired();
            entity.HasIndex(e => e.Phone).IsUnique();

            entity.Property(e => e.Name).HasMaxLength(200).IsRequired().HasDefaultValue("");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).IsRequired().HasDefaultValueSql("now()");
        });

        // -- OTP Requests --
        modelBuilder.Entity<OtpRequest>(entity =>
        {
            entity.ToTable("otp_requests");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Phone).HasMaxLength(15).IsRequired();
            entity.Property(e => e.OtpHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.IsUsed).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.AttemptCount).IsRequired().HasDefaultValue(0);

            entity.Property(e => e.AuthCodeHash).HasMaxLength(255);
            entity.Property(e => e.AuthCodeExpiresAt);
            entity.Property(e => e.AuthCodeUsed).IsRequired().HasDefaultValue(false);

            entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("now()");

            // composite index for fast OTP lookup
            entity.HasIndex(e => new { e.Phone, e.IsUsed, e.ExpiresAt });
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.RevokedAt);
            entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("now()");

            // FK to User - cascade delete
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrgId is cross-Db reference, no FK constraint - just a plain column
            entity.Property(e => e.OrgId).IsRequired();

            // Index for finding active tokens by user
            entity.HasIndex(e => new { e.UserId, e.RevokedAt });
        });
    }
}
