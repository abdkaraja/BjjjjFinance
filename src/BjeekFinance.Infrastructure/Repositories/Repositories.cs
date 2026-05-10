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
            w.FraudScore < 50)
            .ToListAsync(ct);
    }
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

// ── Finance Parameter Repository ───────────────────────────────────────────────

public class FinanceParameterRepository : Repository<FinanceParameter>, IFinanceParameterRepository
{
    public FinanceParameterRepository(BjeekFinanceDbContext ctx) : base(ctx) { }

    public async Task<FinanceParameter?> GetActiveAsync(string key, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default)
    {
        // Priority: city+serviceType → city → global
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

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default)
    {
        var param = await GetActiveAsync(key, cityId, serviceType, ct);
        return param is not null && decimal.TryParse(param.ParameterValue, out var val) ? val : defaultValue;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue, Guid? cityId = null, CancellationToken ct = default)
    {
        var param = await GetActiveAsync(key, cityId, null, ct);
        return param is not null && int.TryParse(param.ParameterValue, out var val) ? val : defaultValue;
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
