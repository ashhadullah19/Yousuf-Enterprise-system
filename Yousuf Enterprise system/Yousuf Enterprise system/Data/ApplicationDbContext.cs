using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OwnedPurchase> OwnedPurchases => Set<OwnedPurchase>();
    public DbSet<ConsignmentReceipt> ConsignmentReceipts => Set<ConsignmentReceipt>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<FinancialVoucher> FinancialVouchers => Set<FinancialVoucher>();
    public DbSet<BackupLog> BackupLogs => Set<BackupLog>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Party>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<OwnedPurchase>()
            .HasOne(p => p.Vendor)
            .WithMany()
            .HasForeignKey(p => p.VendorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<OwnedPurchase>()
            .HasOne(p => p.Product)
            .WithMany()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SalesInvoice>()
            .HasOne(s => s.Buyer)
            .WithMany()
            .HasForeignKey(s => s.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SalesInvoice>()
            .HasOne(s => s.Product)
            .WithMany()
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SalesInvoice>()
            .HasOne(s => s.BankAccount)
            .WithMany()
            .HasForeignKey(s => s.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<FinancialVoucher>()
            .HasOne(v => v.Party)
            .WithMany()
            .HasForeignKey(v => v.PartyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<BankTransaction>()
            .HasOne(t => t.BankAccount)
            .WithMany()
            .HasForeignKey(t => t.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<BankTransaction>()
            .HasOne(t => t.SalesInvoice)
            .WithMany()
            .HasForeignKey(t => t.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OwnedPurchase>().Property(p => p.Quantity).HasPrecision(18, 4);
        builder.Entity<Product>().Property(p => p.MinimumStockAlertQty).HasPrecision(18, 4);
        builder.Entity<SystemSetting>().Property(s => s.DefaultGstPercentage).HasPrecision(5, 2);
    }
}
