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

    /// <summary>
    /// UC-AD-FIN-01: Search wallets by actor ID, actor type, and city.
    /// Name/phone search requires user service integration.
    /// </summary>
    Task<IEnumerable<Wallet>> SearchWalletsAsync(Guid? actorId, ActorType? actorType, Guid? cityId, int skip, int take, CancellationToken ct = default);

    /// <summary>Bulk load wallets by IDs for admin views.</summary>
    Task<IEnumerable<Wallet>> GetByIdsAsync(IEnumerable<Guid> walletIds, CancellationToken ct = default);
}

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<Transaction?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default);
    Task<IEnumerable<Transaction>> GetByWalletAsync(Guid walletId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default);
    Task<IEnumerable<Transaction>> GetByRideAsync(Guid rideId, CancellationToken ct = default);
    Task<IEnumerable<Transaction>> GetByOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<int> GetByActorCountAsync(Guid actorId, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-04: Get transactions by date range with wallet data for VAT reporting.</summary>
    Task<IEnumerable<Transaction>> GetByDateRangeWithWalletAsync(DateTime from, DateTime to, string? serviceType = null, CancellationToken ct = default);
}

public interface IPayoutRequestRepository : IRepository<PayoutRequest>
{
    Task<IEnumerable<PayoutRequest>> GetPendingPayoutsAsync(CancellationToken ct = default);
    Task<IEnumerable<PayoutRequest>> GetByActorAsync(Guid actorId, CancellationToken ct = default);
    Task<IEnumerable<PayoutRequest>> GetQueuedSariePayoutsAsync(CancellationToken ct = default);
    Task<IEnumerable<PayoutRequest>> GetAboveThresholdAsync(decimal threshold, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-02: Pending queue sorted by amount desc, then oldest first.</summary>
    Task<IEnumerable<PayoutRequest>> GetPendingQueueOrderedAsync(CancellationToken ct = default);

    /// <summary>UC-AD-FIN-02: Load payout with its destination account for admin review.</summary>
    Task<PayoutRequest?> GetByIdWithAccountAsync(Guid payoutId, CancellationToken ct = default);
}

public interface IInstantPayRepository : IRepository<InstantPayCashout>
{
    Task<IEnumerable<InstantPayCashout>> GetByActorAsync(Guid actorId, CancellationToken ct = default);
    Task<int> GetDailyCountAsync(Guid actorId, DateTime localDate, CancellationToken ct = default);
    Task<decimal> GetWeeklyTotalAsync(Guid actorId, DateTime weekStart, CancellationToken ct = default);

    /// <summary>Find the cashout linked to a fallback PayoutRequest.</summary>
    Task<InstantPayCashout?> GetByPayoutRequestIdAsync(Guid payoutRequestId, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-04: Get Instant Pay cashouts in a date range with VatOnFee data.</summary>
    Task<IEnumerable<InstantPayCashout>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);
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

public interface IFraudRuleRepository : IRepository<FraudRule>
{
    Task<IEnumerable<FraudRule>> GetActiveRulesAsync(string? domain = null, CancellationToken ct = default);
    Task<FraudRule?> GetByKeyAsync(string ruleKey, CancellationToken ct = default);
}

public interface IFraudCaseRepository : IRepository<FraudCase>
{
    Task<IEnumerable<FraudCase>> GetByActorAsync(Guid actorId, CancellationToken ct = default);
    Task<IEnumerable<FraudCase>> GetByStatusAsync(FraudCaseStatus status, CancellationToken ct = default);
    Task<IEnumerable<FraudCase>> GetBySeverityAsync(FraudSeverity severity, CancellationToken ct = default);
    Task<IEnumerable<FraudCase>> GetOpenBySeverityAsync(FraudSeverity minSeverity, CancellationToken ct = default);
}

public interface IVatReportRepository : IRepository<VatReport>
{
    Task<IEnumerable<VatReport>> GetByPeriodAsync(DateTime from, DateTime to, Guid? merchantActorId = null, CancellationToken ct = default);
}

public interface ICashSettlementRepository : IRepository<CashSettlement>
{
    Task<IEnumerable<CashSettlement>> GetByDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<IEnumerable<CashSettlement>> GetFlaggedForReviewAsync(CancellationToken ct = default);
    Task<CashSettlement?> GetPendingByDriverAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-03: Query settlements by date range and optional city filter.</summary>
    Task<IEnumerable<CashSettlement>> GetByDateRangeAsync(DateTime from, DateTime to, Guid? cityId = null, CancellationToken ct = default);
}

public interface IReconciliationReportRepository : IRepository<ReconciliationReport>
{
    Task<IEnumerable<ReconciliationReport>> GetByDateRangeAsync(DateTime from, DateTime to, Guid? cityId = null, CancellationToken ct = default);
}

public interface IBulkReconciliationReportRepository : IRepository<BulkReconciliationReport>
{
    Task<IEnumerable<BulkReconciliationReport>> GetByPeriodAsync(DateTime from, DateTime to, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default);
}

public interface IFinanceParameterRepository : IRepository<FinanceParameter>
{
    Task<FinanceParameter?> GetActiveAsync(string key, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-07: Scoped lookup with ActorType and Tier dimensions.</summary>
    Task<FinanceParameter?> GetActiveScopedAsync(string key, Guid? cityId, string? serviceType, ActorType? actorType, string? tier, CancellationToken ct = default);

    Task<decimal> GetDecimalAsync(string key, decimal defaultValue, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int defaultValue, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-07: Get all active parameters for a given category.</summary>
    Task<IEnumerable<FinanceParameter>> GetByCategoryAsync(string category, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-07: Get version history for a parameter key (all scopes).</summary>
    Task<IEnumerable<FinanceParameter>> GetHistoryAsync(string key, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-07: Get the previous active version for rollback.</summary>
    Task<FinanceParameter?> GetPreviousVersionAsync(Guid currentId, CancellationToken ct = default);
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
    IReconciliationReportRepository ReconciliationReports { get; }
    IVatReportRepository VatReports { get; }
    IFraudRuleRepository FraudRules { get; }
    IFraudCaseRepository FraudCases { get; }
    IBulkReconciliationReportRepository BulkReconciliationReports { get; }
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
