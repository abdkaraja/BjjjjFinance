using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Application.Interfaces;

/// <summary>Generic repository contract.</summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}

public interface IWalletRepository : IRepository<Wallet>
{
    Task<Wallet?> GetByActorAsync(Guid actorId, ActorType actorType, CancellationToken ct = default);
    Task<IEnumerable<Wallet>> GetByActorOnlyAsync(Guid actorId, CancellationToken ct = default);
    Task<IEnumerable<Wallet>> GetWalletsInDunningAsync(CancellationToken ct = default);
    Task<IEnumerable<Wallet>> GetByInstantPayTierAsync(InstantPayTier tier, CancellationToken ct = default);

    /// <summary>
    /// Locks wallet row for update (SELECT FOR UPDATE) — required for
    /// Saga-pattern atomic balance writes (SRS-FIN-001 §UC-FIN-COLLECT-01).
    /// </summary>
    Task<Wallet?> GetByIdWithLockAsync(Guid walletId, CancellationToken ct = default);

    /// <summary>
    /// Returns wallets whose PENDING balance has aged past the specified minutes threshold.
    /// Used by the background settlement service to auto-settle earnings.
    /// </summary>
    Task<IEnumerable<Wallet>> GetWalletsWithPendingOlderThanAsync(int minutes, CancellationToken ct = default);

    /// <summary>
    /// UC-FIN-INSTANT-01 AF3: Returns wallets where AutoCashoutThreshold is set,
    /// InstantPayEnabled, KYC verified, not TierA, and available >= threshold.
    /// </summary>
    Task<IEnumerable<Wallet>> GetWalletsWithAutoCashoutEnabledAsync(CancellationToken ct = default);
}

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<Transaction?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default);
    Task<IEnumerable<Transaction>> GetByWalletAsync(Guid walletId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default);
    Task<IEnumerable<Transaction>> GetByRideAsync(Guid rideId, CancellationToken ct = default);
    Task<IEnumerable<Transaction>> GetByOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<int> GetByActorCountAsync(Guid actorId, CancellationToken ct = default);
}

public interface IPayoutRequestRepository : IRepository<PayoutRequest>
{
    Task<IEnumerable<PayoutRequest>> GetPendingPayoutsAsync(CancellationToken ct = default);
    Task<IEnumerable<PayoutRequest>> GetByActorAsync(Guid actorId, CancellationToken ct = default);
    Task<IEnumerable<PayoutRequest>> GetQueuedSariePayoutsAsync(CancellationToken ct = default);
    Task<IEnumerable<PayoutRequest>> GetAboveThresholdAsync(decimal threshold, CancellationToken ct = default);
}

public interface IInstantPayRepository : IRepository<InstantPayCashout>
{
    Task<IEnumerable<InstantPayCashout>> GetByActorAsync(Guid actorId, CancellationToken ct = default);
    Task<int> GetDailyCountAsync(Guid actorId, DateTime localDate, CancellationToken ct = default);
    Task<decimal> GetWeeklyTotalAsync(Guid actorId, DateTime weekStart, CancellationToken ct = default);

    /// <summary>Find the cashout linked to a fallback PayoutRequest.</summary>
    Task<InstantPayCashout?> GetByPayoutRequestIdAsync(Guid payoutRequestId, CancellationToken ct = default);
}

public interface IPayoutAccountRepository : IRepository<PayoutAccount>
{
    Task<IEnumerable<PayoutAccount>> GetByActorAsync(Guid actorId, CancellationToken ct = default);
    Task<PayoutAccount?> GetVerifiedAsync(Guid actorId, CancellationToken ct = default);
    Task<bool> IbanExistsForOtherActorAsync(string iban, Guid actorId, CancellationToken ct = default);
}

public interface IAuditLogRepository : IRepository<AuditLogEntry>
{
    Task<IEnumerable<AuditLogEntry>> GetBySubjectAsync(Guid subjectId, string subjectType, CancellationToken ct = default);
    Task<IEnumerable<AuditLogEntry>> GetByActorAsync(Guid actorId, CancellationToken ct = default);
    Task<IEnumerable<AuditLogEntry>> GetByEventTypeAsync(AuditEventType eventType, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    /// <summary>Validate tamper hash chain — called nightly by reconciliation job.</summary>
    Task<IEnumerable<Guid>> GetHashValidationFailuresAsync(CancellationToken ct = default);
}

public interface ICorporateAccountRepository : IRepository<CorporateAccount>
{
    Task<CorporateAccount?> GetByWalletAsync(Guid walletId, CancellationToken ct = default);
    Task<IEnumerable<CorporateAccount>> GetBelowAlertThresholdAsync(CancellationToken ct = default);
    Task<CorporateEmployee?> GetEmployeeAsync(Guid corporateAccountId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<CorporateInvoice>> GetOverdueInvoicesAsync(CancellationToken ct = default);
}

public interface IRefundRepository : IRepository<Refund>
{
    Task<IEnumerable<Refund>> GetByOriginalTransactionAsync(Guid originalTransactionId, CancellationToken ct = default);
    Task<IEnumerable<Refund>> GetByActorAsync(Guid actorId, CancellationToken ct = default);

    /// <summary>Sum of all refund amounts for a transaction (for cumulative cap enforcement).</summary>
    Task<decimal> GetTotalRefundedAmountAsync(Guid originalTransactionId, CancellationToken ct = default);
}

public interface ICashSettlementRepository : IRepository<CashSettlement>
{
    Task<IEnumerable<CashSettlement>> GetByDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<IEnumerable<CashSettlement>> GetFlaggedForReviewAsync(CancellationToken ct = default);
    Task<CashSettlement?> GetPendingByDriverAsync(Guid driverId, CancellationToken ct = default);
}

public interface IFinanceParameterRepository : IRepository<FinanceParameter>
{
    Task<FinanceParameter?> GetActiveAsync(string key, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, decimal defaultValue, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int defaultValue, Guid? cityId = null, CancellationToken ct = default);
}

public interface IUnitOfWork : IAsyncDisposable
{
    IWalletRepository Wallets { get; }
    ITransactionRepository Transactions { get; }
    IPayoutRequestRepository PayoutRequests { get; }
    IInstantPayRepository InstantPay { get; }
    IPayoutAccountRepository PayoutAccounts { get; }
    IAuditLogRepository AuditLogs { get; }
    ICorporateAccountRepository CorporateAccounts { get; }
    IRefundRepository Refunds { get; }
    ICashSettlementRepository CashSettlements { get; }
    IFinanceParameterRepository FinanceParameters { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begin a database transaction for Saga-pattern atomic multi-wallet writes.
    /// All wallet deltas inside a Saga must succeed or all must roll back.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
