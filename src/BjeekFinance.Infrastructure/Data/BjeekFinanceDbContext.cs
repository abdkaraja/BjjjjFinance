using BjeekFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BjeekFinance.Infrastructure.Data;

public class BjeekFinanceDbContext : DbContext
{
    public BjeekFinanceDbContext(DbContextOptions<BjeekFinanceDbContext> options) : base(options) { }

    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<PayoutAccount> PayoutAccounts => Set<PayoutAccount>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PayoutRequest> PayoutRequests => Set<PayoutRequest>();
    public DbSet<InstantPayCashout> InstantPayCashouts => Set<InstantPayCashout>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<CorporateAccount> CorporateAccounts => Set<CorporateAccount>();
    public DbSet<CorporateEmployee> CorporateEmployees => Set<CorporateEmployee>();
    public DbSet<CorporateInvoice> CorporateInvoices => Set<CorporateInvoice>();
    public DbSet<FinanceParameter> FinanceParameters => Set<FinanceParameter>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<CashSettlement> CashSettlements => Set<CashSettlement>();
    public DbSet<ReconciliationReport> ReconciliationReports => Set<ReconciliationReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BjeekFinanceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-update UpdatedAt on all BaseEntity changes
        foreach (var entry in ChangeTracker.Entries<Domain.Entities.BaseEntity>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
