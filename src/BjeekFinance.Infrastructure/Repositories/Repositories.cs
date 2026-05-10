using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using BjeekFinance.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BjeekFinance.Infrastructure.Repositories;

// ── Generic base repository ────────────────────────────────────────────────────

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly BjeekFinanceDbContext _ctx;
    protected readonly DbSet<T> _set;

    public Repository(BjeekFinanceDbContext ctx)
    {
        _ctx = ctx;
        _set = ctx.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _set.FindAsync([id], ct);

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => await _set.AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await _set.AddAsync(entity, ct);

    public virtual void Update(T entity)
        => _set.Update(entity);

    public virtual void Delete(T entity)
        => _set.Remove(entity);
}

// ── Wallet Repository ──────────────────────────────────────────────────────────

public class WalletRepository : Repository<Wallet>, IWalletRepository
{
    public WalletRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<Wallet?> GetByActorAsync(Guid actorId, ActorType actorType, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(w => w.ActorId == actorId && w.ActorType == actorType, ct);

    public async Task<IEnumerable<Wallet>> GetByActorOnlyAsync(Guid actorId, CancellationToken ct = default)
        => await _set.Where(w => w.ActorId == actorId).ToListAsync(ct);

    public async Task<IEnumerable<Wallet>> GetWalletsInDunningAsync(CancellationToken ct = default)
        => await _set.Where(w => w.IsInDunning).ToListAsync(ct);

    public async Task<IEnumerable<Wallet>> GetByInstantPayTierAsync(InstantPayTier tier, CancellationToken ct = default)
        => await _set.Where(w => w.InstantPayTier == tier).ToListAsync(ct);

    public async Task<Wallet?> GetByIdWithLockAsync(Guid walletId, CancellationToken ct = default)
    {
        return await _set.FromSqlRaw(
            "SELECT * FROM Wallets WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", walletId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<Wallet>> GetWalletsWithPendingOlderThanAsync(int minutes, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
        return await _set.Where(w => w.BalancePending > 0 && w.PendingSince != null && w.PendingSince <= cutoff)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Wallet>> GetWalletsWithAutoCashoutEnabledAsync(CancellationToken ct = default)
    {
        return await _set.Where(w =>
            w.AutoCashoutThreshold != null &&
            w.BalanceAvailable >= w.AutoCashoutThreshold!.Value &&
            w.InstantPayEnabled &&
            w.KycStatus == KycStatus.Verified &&
            w.InstantPayTier != InstantPayTier.TierA &&
            !w.IsInDunning &&
            w.FraudScore < 50)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Wallet>> SearchWalletsAsync(Guid? actorId, ActorType? actorType, Guid? cityId, int skip, int take, CancellationToken ct = default)
    {
        var query = _set.AsQueryable();
        if (actorId.HasValue) query = query.Where(w => w.ActorId == actorId.Value);
        if (actorType.HasValue) query = query.Where(w => w.ActorType == actorType.Value);
        if (cityId.HasValue) query = query.Where(w => w.CityId == cityId.Value);
        return await query.OrderBy(w => w.ActorType).ThenBy(w => w.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<IEnumerable<Wallet>> GetByIdsAsync(IEnumerable<Guid> walletIds, CancellationToken ct = default)
        => await _set.Where(w => walletIds.Contains(w.Id)).ToListAsync(ct);
}

// ── Transaction Repository ─────────────────────────────────────────────────────

public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    public TransactionRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<Transaction?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(t => t.IdempotencyKey == key, ct);

    public async Task<IEnumerable<Transaction>> GetByWalletAsync(Guid walletId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var query = _set.Where(t => t.WalletId == walletId);
        if (from.HasValue) query = query.Where(t => t.CreatedAt >= from.Value.UtcDateTime);
        if (to.HasValue) query = query.Where(t => t.CreatedAt <= to.Value.UtcDateTime);
        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
    }

    public async Task<IEnumerable<Transaction>> GetByRideAsync(Guid rideId, CancellationToken ct = default)
        => await _set.Where(t => t.RideId == rideId).ToListAsync(ct);

    public async Task<IEnumerable<Transaction>> GetByOrderAsync(Guid orderId, CancellationToken ct = default)
        => await _set.Where(t => t.OrderId == orderId).ToListAsync(ct);

    public async Task<int> GetByActorCountAsync(Guid actorId, CancellationToken ct = default)
        => await _set.CountAsync(t => t.Wallet.ActorId == actorId, ct);

    public async Task<IEnumerable<Transaction>> GetByDateRangeWithWalletAsync(DateTime from, DateTime to, string? serviceType = null, CancellationToken ct = default)
    {
        var query = _set.Include(t => t.Wallet).Where(t => t.CreatedAt >= from && t.CreatedAt <= to);
        if (!string.IsNullOrEmpty(serviceType))
        {
            query = serviceType.ToLowerInvariant() switch
            {
                "ride" => query.Where(t => t.RideId != null),
                "delivery" or "food" or "grocery" => query.Where(t => t.OrderId != null),
                _ => query
            };
        }
        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
    }
}

// ── Payout Request Repository ──────────────────────────────────────────────────

public class PayoutRequestRepository : Repository<PayoutRequest>, IPayoutRequestRepository
{
    public PayoutRequestRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<PayoutRequest>> GetPendingPayoutsAsync(CancellationToken ct = default)
        => await _set.Where(p => p.Status == PayoutStatus.Pending).ToListAsync(ct);

    public async Task<IEnumerable<PayoutRequest>> GetByActorAsync(Guid actorId, CancellationToken ct = default)
        => await _set.Where(p => p.ActorId == actorId).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

    public async Task<IEnumerable<PayoutRequest>> GetQueuedSariePayoutsAsync(CancellationToken ct = default)
        => await _set.Where(p => p.Status == PayoutStatus.Approved && p.SarieWindowStatus == SarieWindowStatus.Queued).ToListAsync(ct);

    public async Task<IEnumerable<PayoutRequest>> GetAboveThresholdAsync(decimal threshold, CancellationToken ct = default)
        => await _set.Where(p => p.AmountRequested > threshold && p.Status == PayoutStatus.Pending).ToListAsync(ct);

    public async Task<IEnumerable<PayoutRequest>> GetPendingQueueOrderedAsync(CancellationToken ct = default)
        => await _set.Where(p => p.Status == PayoutStatus.Pending)
            .OrderByDescending(p => p.AmountRequested)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<PayoutRequest?> GetByIdWithAccountAsync(Guid payoutId, CancellationToken ct = default)
        => await _set.Include(p => p.PayoutAccount).FirstOrDefaultAsync(p => p.Id == payoutId, ct);
}

// ── Instant Pay Repository ─────────────────────────────────────────────────────

public class InstantPayRepository : Repository<InstantPayCashout>, IInstantPayRepository
{
    public InstantPayRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<InstantPayCashout>> GetByActorAsync(Guid actorId, CancellationToken ct = default)
        => await _set.Where(i => i.ActorId == actorId).OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

    public async Task<int> GetDailyCountAsync(Guid actorId, DateTime localDate, CancellationToken ct = default)
        => await _set.CountAsync(i => i.ActorId == actorId
            && i.CityLocalTime.Date == localDate.Date
            && i.TransferStatus != PayoutStatus.Failed, ct);

    public async Task<decimal> GetWeeklyTotalAsync(Guid actorId, DateTime weekStart, CancellationToken ct = default)
        => await _set.Where(i => i.ActorId == actorId
            && i.CityLocalTime >= weekStart
            && i.TransferStatus != PayoutStatus.Failed)
            .SumAsync(i => i.AmountRequested, ct);

    public async Task<InstantPayCashout?> GetByPayoutRequestIdAsync(Guid payoutRequestId, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(i => i.PayoutRequestId == payoutRequestId, ct);

    public async Task<IEnumerable<InstantPayCashout>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await _set.Where(i => i.CreatedAt >= from && i.CreatedAt <= to).OrderByDescending(i => i.CreatedAt).ToListAsync(ct);
}

// ── Payout Account Repository ──────────────────────────────────────────────────

public class PayoutAccountRepository : Repository<PayoutAccount>, IPayoutAccountRepository
{
    public PayoutAccountRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<PayoutAccount>> GetByActorAsync(Guid actorId, CancellationToken ct = default)
        => await _set.Where(a => a.ActorId == actorId).ToListAsync(ct);

    public async Task<PayoutAccount?> GetVerifiedAsync(Guid actorId, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(a => a.ActorId == actorId && a.VerificationStatus == KycStatus.Verified, ct);

    public async Task<bool> IbanExistsForOtherActorAsync(string iban, Guid actorId, CancellationToken ct = default)
        => await _set.AnyAsync(a => a.AccountIdentifier == iban && a.ActorId != actorId, ct);
}

// ── Audit Log Repository ───────────────────────────────────────────────────────

public class AuditLogRepository : Repository<AuditLogEntry>, IAuditLogRepository
{
    public AuditLogRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<AuditLogEntry>> GetBySubjectAsync(Guid subjectId, string subjectType, CancellationToken ct = default)
        => await _set.Where(a => a.SubjectId == subjectId && a.SubjectType == subjectType)
            .OrderByDescending(a => a.Timestamp).ToListAsync(ct);

    public async Task<IEnumerable<AuditLogEntry>> GetByActorAsync(Guid actorId, CancellationToken ct = default)
        => await _set.Where(a => a.ActorId == actorId).OrderByDescending(a => a.Timestamp).ToListAsync(ct);

    public async Task<IEnumerable<AuditLogEntry>> GetByEventTypeAsync(AuditEventType eventType, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        => await _set.Where(a => a.EventType == eventType
            && a.Timestamp >= from.UtcDateTime
            && a.Timestamp <= to.UtcDateTime)
            .OrderByDescending(a => a.Timestamp).ToListAsync(ct);

    public async Task<IEnumerable<Guid>> GetHashValidationFailuresAsync(CancellationToken ct = default)
    {
        // Real impl: recompute hash for each entry and compare
        // Returns IDs of entries where stored hash doesn't match computed hash
        await Task.CompletedTask;
        return Enumerable.Empty<Guid>();
    }
}

// ── Corporate Account Repository ───────────────────────────────────────────────

public class CorporateAccountRepository : Repository<CorporateAccount>, ICorporateAccountRepository
{
    public CorporateAccountRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<CorporateAccount?> GetByWalletAsync(Guid walletId, CancellationToken ct = default)
        => await _set.Include(c => c.Employees).Include(c => c.Invoices)
            .FirstOrDefaultAsync(c => c.WalletId == walletId, ct);

    public async Task<IEnumerable<CorporateAccount>> GetBelowAlertThresholdAsync(CancellationToken ct = default)
    {
        // Returns accounts whose wallet balance is below LowBalanceAlertThreshold
        return await _set
            .Join(_ctx.Set<Wallet>(), c => c.WalletId, w => w.Id, (c, w) => new { c, w })
            .Where(x => x.w.BalanceAvailable < x.c.LowBalanceAlertThreshold)
            .Select(x => x.c).ToListAsync(ct);
    }

    public async Task<CorporateEmployee?> GetEmployeeAsync(Guid corporateAccountId, Guid userId, CancellationToken ct = default)
        => await _ctx.Set<CorporateEmployee>()
            .FirstOrDefaultAsync(e => e.CorporateAccountId == corporateAccountId && e.UserId == userId, ct);

    public async Task<IEnumerable<CorporateInvoice>> GetOverdueInvoicesAsync(CancellationToken ct = default)
        => await _ctx.Set<CorporateInvoice>()
            .Where(i => !i.IsPaid && i.DueDate < DateTime.UtcNow).ToListAsync(ct);
}

// ── Cash Settlement Repository ──────────────────────────────────────────────────

public class CashSettlementRepository : Repository<CashSettlement>, ICashSettlementRepository
{
    public CashSettlementRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<CashSettlement>> GetByDriverAsync(Guid driverId, CancellationToken ct = default)
        => await _set.Where(s => s.DriverId == driverId).OrderByDescending(s => s.CreatedAt).ToListAsync(ct);

    public async Task<IEnumerable<CashSettlement>> GetFlaggedForReviewAsync(CancellationToken ct = default)
        => await _set.Where(s => s.VarianceFlag && s.Status == CashSettlementStatus.FlaggedForReview).ToListAsync(ct);

    public async Task<CashSettlement?> GetPendingByDriverAsync(Guid driverId, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(s => s.DriverId == driverId && s.Status == CashSettlementStatus.Submitted, ct);

    public async Task<IEnumerable<CashSettlement>> GetByDateRangeAsync(DateTime from, DateTime to, Guid? cityId = null, CancellationToken ct = default)
    {
        var query = _set.Where(s => s.CreatedAt >= from && s.CreatedAt <= to);
        if (cityId.HasValue) query = query.Where(s => s.CityId == cityId.Value);
        return await query.OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
    }
}

// ── Fraud Rule Repository ──────────────────────────────────────────────────────

public class FraudRuleRepository : Repository<FraudRule>, IFraudRuleRepository
{
    public FraudRuleRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<FraudRule>> GetActiveRulesAsync(string? domain = null, CancellationToken ct = default)
    {
        var query = _set.Where(r => r.IsActive);
        if (!string.IsNullOrEmpty(domain) && domain != "all")
            query = query.Where(r => r.Domain == domain || r.Domain == "all");
        return await query.ToListAsync(ct);
    }

    public async Task<FraudRule?> GetByKeyAsync(string ruleKey, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(r => r.RuleKey == ruleKey, ct);
}

// ── Fraud Case Repository ──────────────────────────────────────────────────────

public class FraudCaseRepository : Repository<FraudCase>, IFraudCaseRepository
{
    public FraudCaseRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<FraudCase>> GetByActorAsync(Guid actorId, CancellationToken ct = default)
        => await _set.Where(c => c.ActorId == actorId).OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

    public async Task<IEnumerable<FraudCase>> GetByStatusAsync(FraudCaseStatus status, CancellationToken ct = default)
        => await _set.Where(c => c.Status == status).OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

    public async Task<IEnumerable<FraudCase>> GetBySeverityAsync(FraudSeverity severity, CancellationToken ct = default)
        => await _set.Where(c => c.Severity == severity).OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

    public async Task<IEnumerable<FraudCase>> GetOpenBySeverityAsync(FraudSeverity minSeverity, CancellationToken ct = default)
    {
        var severities = new[] { FraudSeverity.High, FraudSeverity.Critical };
        if (minSeverity == FraudSeverity.Medium)
            severities = new[] { FraudSeverity.Medium, FraudSeverity.High, FraudSeverity.Critical };
        if (minSeverity == FraudSeverity.Low)
            severities = new[] { FraudSeverity.Low, FraudSeverity.Medium, FraudSeverity.High, FraudSeverity.Critical };
        return await _set.Where(c => c.Status == FraudCaseStatus.Open && severities.Contains(c.Severity))
            .OrderByDescending(c => c.Severity).ThenByDescending(c => c.CreatedAt).ToListAsync(ct);
    }
}

// ── Vat Report Repository ──────────────────────────────────────────────────────

public class VatReportRepository : Repository<VatReport>, IVatReportRepository
{
    public VatReportRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<VatReport>> GetByPeriodAsync(DateTime from, DateTime to, Guid? merchantActorId = null, CancellationToken ct = default)
    {
        var query = _set.Where(r => r.PeriodStart >= from && r.PeriodEnd <= to);
        if (merchantActorId.HasValue) query = query.Where(r => r.MerchantActorId == merchantActorId.Value);
        return await query.OrderByDescending(r => r.GeneratedAt).ToListAsync(ct);
    }
}

// ── Reconciliation Report Repository ────────────────────────────────────────────

public class ReconciliationReportRepository : Repository<ReconciliationReport>, IReconciliationReportRepository
{
    public ReconciliationReportRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<ReconciliationReport>> GetByDateRangeAsync(DateTime from, DateTime to, Guid? cityId = null, CancellationToken ct = default)
    {
        var query = _set.Where(r => r.DateFrom >= from && r.DateTo <= to);
        if (cityId.HasValue) query = query.Where(r => r.CityId == cityId.Value);
        return await query.OrderByDescending(r => r.GeneratedAt).ToListAsync(ct);
    }
}

// ── Refund Repository ───────────────────────────────────────────────────────────

public class RefundRepository : Repository<Refund>, IRefundRepository
{
    public RefundRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<Refund>> GetByOriginalTransactionAsync(Guid originalTransactionId, CancellationToken ct = default)
        => await _set.Where(r => r.OriginalTransactionId == originalTransactionId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task<IEnumerable<Refund>> GetByActorAsync(Guid actorId, CancellationToken ct = default)
        => await _set.Where(r => r.ActorId == actorId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task<decimal> GetTotalRefundedAmountAsync(Guid originalTransactionId, CancellationToken ct = default)
        => await _set.Where(r => r.OriginalTransactionId == originalTransactionId && r.Status == RefundStatus.Completed)
            .SumAsync(r => r.Amount, ct);
}

// ── Bulk Reconciliation Report Repository ───────────────────────────────────────

public class BulkReconciliationReportRepository : Repository<BulkReconciliationReport>, IBulkReconciliationReportRepository
{
    public BulkReconciliationReportRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<BulkReconciliationReport>> GetByPeriodAsync(DateTime from, DateTime to, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default)
    {
        var query = _set.Where(r => r.DateFrom >= from && r.DateTo <= to);
        if (cityId.HasValue) query = query.Where(r => r.CityId == cityId.Value);
        if (!string.IsNullOrEmpty(serviceType)) query = query.Where(r => r.ServiceType == serviceType);
        return await query.OrderByDescending(r => r.GeneratedAt).ToListAsync(ct);
    }
}

// ── Finance Parameter Repository ───────────────────────────────────────────────

public class FinanceParameterRepository : Repository<FinanceParameter>, IFinanceParameterRepository
{
    public FinanceParameterRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<FinanceParameter?> GetActiveAsync(string key, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default)
    {
        // Priority: city+serviceType → city → global (backward-compatible)
        var query = _set.Where(p => p.ParameterKey == key && p.IsActive);

        if (cityId.HasValue && serviceType is not null)
        {
            var specific = await query.FirstOrDefaultAsync(p => p.CityId == cityId && p.ServiceType == serviceType, ct);
            if (specific is not null) return specific;
        }

        if (cityId.HasValue)
        {
            var cityParam = await query.FirstOrDefaultAsync(p => p.CityId == cityId && p.ServiceType == null, ct);
            if (cityParam is not null) return cityParam;
        }

        return await query.FirstOrDefaultAsync(p => p.CityId == null, ct);
    }

    public async Task<FinanceParameter?> GetActiveScopedAsync(string key, Guid? cityId, string? serviceType, ActorType? actorType, string? tier, CancellationToken ct = default)
    {
        // Full scoped resolution: (city+serviceType+actorType+tier) → fallback tiers
        var query = _set.Where(p => p.ParameterKey == key && p.IsActive);

        var scoped = query;
        if (cityId.HasValue) scoped = scoped.Where(p => p.CityId == cityId || p.CityId == null);
        if (serviceType is not null) scoped = scoped.Where(p => p.ServiceType == serviceType || p.ServiceType == null);
        if (actorType.HasValue) scoped = scoped.Where(p => p.ActorType == actorType || p.ActorType == null);
        if (tier is not null) scoped = scoped.Where(p => p.Tier == tier || p.Tier == null);

        // Order by specificity: most specific match first
        var results = await scoped
            .OrderByDescending(p => p.CityId == cityId ? 1 : 0)
            .ThenByDescending(p => p.ServiceType == serviceType ? 1 : 0)
            .ThenByDescending(p => p.ActorType == actorType ? 1 : 0)
            .ThenByDescending(p => p.Tier == tier ? 1 : 0)
            .ThenByDescending(p => p.Version)
            .ToListAsync(ct);

        return results.FirstOrDefault();
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default)
    {
        var param = await GetActiveAsync(key, cityId, serviceType, ct);
        return param is not null && decimal.TryParse(param.ParameterValue, out var val) ? val : defaultValue;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default)
    {
        var param = await GetActiveAsync(key, cityId, serviceType, ct);
        return param is not null && int.TryParse(param.ParameterValue, out var val) ? val : defaultValue;
    }

    public async Task<IEnumerable<FinanceParameter>> GetByCategoryAsync(string category, CancellationToken ct = default)
        => await _set.Where(p => p.Category == category && p.IsActive).OrderBy(p => p.ParameterKey).ToListAsync(ct);

    public async Task<IEnumerable<FinanceParameter>> GetHistoryAsync(string key, CancellationToken ct = default)
        => await _set.Where(p => p.ParameterKey == key).OrderByDescending(p => p.Version).ToListAsync(ct);

    public async Task<FinanceParameter?> GetPreviousVersionAsync(Guid currentId, CancellationToken ct = default)
    {
        var current = await _set.FindAsync([currentId], ct);
        if (current is null) return null;
        return await _set.Where(p => p.ParameterKey == current.ParameterKey
            && p.Version < current.Version
            && p.CityId == current.CityId
            && p.ServiceType == current.ServiceType)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(ct);
    }
}

// ── Unit of Work ───────────────────────────────────────────────────────────────

public class UnitOfWork : IUnitOfWork
{
    private readonly BjeekFinanceDbContext _ctx;
    private IDbContextTransaction? _transaction;

    public IWalletRepository Wallets { get; }
    public ITransactionRepository Transactions { get; }
    public IPayoutRequestRepository PayoutRequests { get; }
    public IInstantPayRepository InstantPay { get; }
    public IPayoutAccountRepository PayoutAccounts { get; }
    public IAuditLogRepository AuditLogs { get; }
    public ICorporateAccountRepository CorporateAccounts { get; }
    public IRefundRepository Refunds { get; }
    public ICashSettlementRepository CashSettlements { get; }
    public IReconciliationReportRepository ReconciliationReports { get; }
    public IVatReportRepository VatReports { get; }
    public IFraudRuleRepository FraudRules { get; }
    public IFraudCaseRepository FraudCases { get; }
    public IBulkReconciliationReportRepository BulkReconciliationReports { get; }
    public IFinanceParameterRepository FinanceParameters { get; }

    public UnitOfWork(BjeekFinanceDbContext ctx)
    {
        _ctx = ctx;
        Wallets = new WalletRepository(ctx);
        Transactions = new TransactionRepository(ctx);
        PayoutRequests = new PayoutRequestRepository(ctx);
        InstantPay = new InstantPayRepository(ctx);
        PayoutAccounts = new PayoutAccountRepository(ctx);
        AuditLogs = new AuditLogRepository(ctx);
        CorporateAccounts = new CorporateAccountRepository(ctx);
        Refunds = new RefundRepository(ctx);
        CashSettlements = new CashSettlementRepository(ctx);
        ReconciliationReports = new ReconciliationReportRepository(ctx);
        VatReports = new VatReportRepository(ctx);
        FraudRules = new FraudRuleRepository(ctx);
        FraudCases = new FraudCaseRepository(ctx);
        BulkReconciliationReports = new BulkReconciliationReportRepository(ctx);
        FinanceParameters = new FinanceParameterRepository(ctx);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _ctx.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _ctx.Database.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null) await _transaction.DisposeAsync();
        await _ctx.DisposeAsync();
    }
}
