using Microsoft.EntityFrameworkCore;
using CashManagementSystem.Models;

namespace CashManagementSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Institute> Institutes => Set<Institute>();
        public DbSet<InstituteUnit> InstituteUnits => Set<InstituteUnit>();
        public DbSet<OrganizationalUnit> OrganizationalUnits => Set<OrganizationalUnit>();
        public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<Bill> Bills => Set<Bill>();
        public DbSet<BillPayment> BillPayments => Set<BillPayment>();
        public DbSet<FinancialCategory> FinancialCategories => Set<FinancialCategory>();
        public DbSet<NotificationItem> Notifications => Set<NotificationItem>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.Property(t => t.Amount).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Bill>(entity =>
            {
                entity.Property(b => b.TotalAmount).HasPrecision(18, 2);
                entity.Property(b => b.PaidAmount).HasPrecision(18, 2);
                entity.HasMany(b => b.Payments)
                      .WithOne(p => p.Bill)
                      .HasForeignKey(p => p.BillId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BillPayment>(entity =>
            {
                entity.Property(bp => bp.Amount).HasPrecision(18, 2);
            });

            modelBuilder.Entity<BankAccount>(entity =>
            {
                entity.Property(ba => ba.OpeningBalance).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Institute>(entity =>
            {
                entity.HasMany(i => i.Units)
                      .WithOne(u => u.Institute)
                      .HasForeignKey(u => u.InstituteId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(i => i.Admins)
                      .WithOne(u => u.Institute)
                      .HasForeignKey(u => u.InstituteId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
