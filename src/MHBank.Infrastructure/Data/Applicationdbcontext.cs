using Microsoft.EntityFrameworkCore;
using MHBank.Core.Entities;

namespace MHBank.Infrastructure.Data;

/// <summary>
/// سياق قاعدة البيانات الرئيسي
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // الجداول
    public DbSet<User> Users => Set<User>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notification> Notifications => Set<Notification>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ═══════════════════════════════════════════
        // User Configuration
        // ═══════════════════════════════════════════
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.PhoneNumber).IsUnique();

            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(15);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);

            // العلاقة: User لديه عدة Accounts
            entity.HasMany(e => e.Accounts)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ═══════════════════════════════════════════
        // BankAccount Configuration
        // ═══════════════════════════════════════════
        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AccountNumber).IsUnique();
            entity.HasIndex(e => e.IBAN).IsUnique();

            entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.IBAN).IsRequired().HasMaxLength(34);
            entity.Property(e => e.Balance).HasPrecision(18, 2);

            // العلاقة: Account لديه عدة Transactions
            entity.HasMany(e => e.Transactions)
                .WithOne(e => e.Account)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ═══════════════════════════════════════════
        // Transaction Configuration
        // ═══════════════════════════════════════════
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReferenceNumber).IsUnique();
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.ReferenceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
        });

        // ═══════════════════════════════════════════
        // Card Configuration
        // ═══════════════════════════════════════════
        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CardNumber).IsUnique();
            entity.HasIndex(e => e.AccountId);

            entity.Property(e => e.CardNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CardHolderName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CVV).IsRequired().HasMaxLength(4);
            entity.Property(e => e.DailyLimit).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyLimit).HasPrecision(18, 2);

            // العلاقة: Card ينتمي لـ Account واحد
            entity.HasOne(e => e.Account)
                .WithMany(e => e.Cards)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        // ═══════════════════════════════════════════
        // RefreshToken Configuration
        // ═══════════════════════════════════════════
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);

            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);

            // العلاقة: RefreshToken ينتمي لـ User واحد
            entity.HasOne(e => e.User)
                .WithMany(e => e.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        // ═══════════════════════════════════════════
        // Notification Configuration
        // ═══════════════════════════════════════════
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IsRead);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);

            // العلاقة: Notification ينتمي لـ User واحد
            entity.HasOne(e => e.User)
                .WithMany(e => e.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}