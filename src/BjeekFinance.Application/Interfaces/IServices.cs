using BjeekFinance.Application.Common;
using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Application.Interfaces;

// ── Wallet Service ─────────────────────────────────────────────────────────────

public interface IWalletService
{
    Task<WalletDto> GetWalletAsync(Guid actorId, ActorType actorType, CancellationToken ct = default);
    Task<WalletDto> GetWalletByIdAsync(Guid walletId, CancellationToken ct = default);

    /// <summary>Credit wallet — records audit log entry synchronously before returning.</summary>
    Task CreditAsync(Guid walletId, decimal amount, string subtype, Guid initiatorId, ActorRole initiatorRole, CancellationToken ct = default);

    /// <summary>Debit wallet respecting credit consumption order for User wallets.</summary>
    Task DebitAsync(Guid walletId, decimal amount, string subtype, Guid initiatorId, ActorRole initiatorRole, CancellationToken ct = default);

    Task HoldAsync(Guid walletId, decimal amount, string reason, Guid initiatorId, ActorRole initiatorRole, CancellationToken ct = default);
    Task ReleaseHoldAsync(Guid walletId, decimal amount, string reason, Guid initiatorId, ActorRole initiatorRole, CancellationToken ct = default);

    /// <summary>
    /// Finance Admin only. Balance correction with immutable audit log.
    /// Requires before/after state capture.
    /// </summary>
    Task AdminBalanceCorrectionAsync(Guid walletId, decimal correctionAmount, string reason, Guid adminId, CancellationToken ct = default);

    /// <summary>Promote PENDING balance to AVAILABLE after 15-minute settlement window.</summary>
    Task SettlePendingEarningsAsync(Guid walletId, CancellationToken ct = default);

    /// <summary>
    /// Batch settlement: finds all wallets with pending earnings older than the configured
    /// settlement window and promotes their PENDING balance to AVAILABLE.
    /// Called by the background hosted service every minute.
    /// </summary>
    Task SettlePendingForAllEligibleWalletsAsync(CancellationToken ct = default);

    /// <summary>Record cash commission receivable — reduces AVAILABLE immediately.</summary>
    Task RecordCashCommissionReceivableAsync(Guid walletId, decimal commissionAmount, CancellationToken ct = default);

    /// <summary>
    /// Collect cash commission from driver — reduces CashReceivable and restores AVAILABLE.
    /// Called when driver settles outstanding cash commission (e.g., at cash collection point).
    /// </summary>
    Task CollectCashCommissionAsync(Guid walletId, decimal amount, CancellationToken ct = default);

    /// <summary>
    /// Process a chargeback / dispute reversal.
    /// If earnings are still PENDING, reduces PENDING. Otherwise reduces AVAILABLE.
    /// If AVAILABLE is insufficient, creates a negative receivable (dunning).
    /// </summary>
    Task ProcessChargebackAsync(Guid walletId, decimal amount, Guid transactionId, CancellationToken ct = default);

    Task<IEnumerable<WalletSummaryDto>> GetWalletsByActorTypeAsync(ActorType actorType, CancellationToken ct = default);
}

// ── Payment Collection Service ─────────────────────────────────────────────────

public interface IPaymentCollectionService
{
    /// <summary>
    /// UC-FIN-COLLECT-01: Collect payment and distribute proceeds across all
    /// wallets atomically using Saga pattern. Transaction atomicity is non-negotiable.
    /// </summary>
    Task<CollectPaymentResultDto> CollectPaymentAsync(CollectPaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// UC-FIN-TIP-01: Add tip for driver. Tips NOT subject to platform commission.
    /// Tip window duration is admin-configurable (default: 2 hours post-ride).
    /// </summary>
    Task<TipResultDto> AddTipAsync(AddTipRequest request, CancellationToken ct = default);

    Task<TransactionDto> GetTransactionAsync(Guid transactionId, CancellationToken ct = default);
    Task<IEnumerable<TransactionDto>> GetTransactionsByWalletAsync(Guid walletId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
}

// ── Payout Service ─────────────────────────────────────────────────────────────

public interface IPayoutService
{
    /// <summary>
    /// UC-FIN-PAYOUT-01: Initiate standard payout request.
    /// Checks KYC, AVAILABLE balance, dunning, SARIE window.
    /// Above threshold → routes to Finance Admin approval queue.
    /// </summary>
    Task<PayoutRequestDto> InitiatePayoutAsync(InitiatePayoutRequest request, CancellationToken ct = default);

    Task<PayoutRequestDto> ApprovePayoutAsync(Guid payoutId, Guid approverActorId, CancellationToken ct = default);
    Task<PayoutRequestDto> RejectPayoutAsync(Guid payoutId, Guid approverActorId, string reasonCode, CancellationToken ct = default);

    Task<IEnumerable<PayoutRequestDto>> GetPendingPayoutsAsync(CancellationToken ct = default);
    Task<IEnumerable<PayoutRequestDto>> GetByActorAsync(Guid actorId, CancellationToken ct = default);
    Task<PayoutRequestDto> GetByIdAsync(Guid payoutId, CancellationToken ct = default);

    /// <summary>Scheduled SARIE batch processor — processes queued transfers in open window.</summary>
    Task ProcessSarieQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// UC-FIN-PAYOUT-01: PSP/gateway webhook — confirms transfer completed.
    /// Releases balance_hold, sets final status, records PSP reference.
    /// </summary>
    Task<PayoutRequestDto> CompletePayoutAsync(Guid payoutId, string pspTransactionId, string transferReference, CancellationToken ct = default);

    /// <summary>
    /// UC-FIN-PAYOUT-01 EX2: Retry a failed payout with exponential backoff.
    /// RetryCount incremented; after 3 failures, hold released and admin notified.
    /// </summary>
    Task<PayoutRequestDto> RetryPayoutAsync(Guid payoutId, CancellationToken ct = default);
}

// ── Instant Pay Service ────────────────────────────────────────────────────────

public interface IInstantPayService
{
    /// <summary>
    /// UC-FIN-INSTANT-01: On-demand cashout from AVAILABLE balance only.
    /// Separate eligibility engine replaces admin approval gate.
    /// Fee + ZATCA-compliant micro-invoice generated per cashout.
    /// </summary>
    Task<InstantPayResultDto> InitiateCashoutAsync(InstantPayRequest request, CancellationToken ct = default);

    /// <summary>Check real-time eligibility — balance, tier, daily limit, fraud score, flags.</summary>
    Task<EligibilityResultDto> CheckEligibilityAsync(Guid actorId, decimal amount, CancellationToken ct = default);

    /// <summary>Nightly job: recalculate trust tier for all driver/delivery wallets.</summary>
    Task RecalculateTiersAsync(CancellationToken ct = default);

    /// <summary>Admin: revoke Instant Pay with reason category code and resolution timeline.</summary>
    Task RevokeInstantPayAsync(Guid walletId, string reasonCode, string estimatedResolution, Guid adminId, CancellationToken ct = default);

    Task<IEnumerable<InstantPayCashoutDto>> GetByActorAsync(Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// UC-FIN-INSTANT-01: PSP/gateway webhook — confirms Instant Pay transfer completed.
    /// Releases balance_hold, sets completed status, records PSP reference.
    /// </summary>
    Task<InstantPayResultDto> CompleteCashoutAsync(Guid cashoutId, string pspTransactionId, string transferReference, CancellationToken ct = default);

    /// <summary>
    /// UC-FIN-INSTANT-01 AF2: Primary rail failed — fallback to standard IBAN transfer (T+1).
    /// Creates a standard PayoutRequest queued for SARIE processing.
    /// EX4: If fallback rail also fails, releases hold and marks cashout as Failed.
    /// </summary>
    Task<InstantPayCashoutDto> FailCashoutAsync(Guid cashoutId, string failureReason, CancellationToken ct = default);

    /// <summary>
    /// UC-FIN-INSTANT-01 EX3: Account flagged during transfer.
    /// Cancels cashout, releases balance_hold back to balance_available, raises fraud alert.
    /// </summary>
    Task<InstantPayCashoutDto> CancelCashoutAsync(Guid cashoutId, string reasonCode, CancellationToken ct = default);

    /// <summary>
    /// UC-FIN-INSTANT-01 AF3: Auto-cashout mode — scans wallets with AutoCashoutThreshold
    /// set and available balance ≥ threshold. Auto-initiates cashout for eligible wallets.
    /// </summary>
    Task<int> ProcessAutoCashoutsAsync(CancellationToken ct = default);
}

// ── Admin / Finance Ops Service ────────────────────────────────────────────────

public interface IAdminFinanceService
{
    Task<DunningStatusDto> GetDunningStatusAsync(Guid walletId, CancellationToken ct = default);
    Task<IEnumerable<DunningStatusDto>> GetAllDunningWalletsAsync(CancellationToken ct = default);

    /// <summary>Nightly dunning batch: classify wallets into buckets and apply actions.</summary>
    Task RunDunningBatchAsync(CancellationToken ct = default);

    Task<WriteOffResultDto> InitiateWriteOffAsync(WriteOffRequest request, CancellationToken ct = default);
    Task<WriteOffResultDto> ApproveWriteOffAsync(Guid writeOffId, Guid approverActorId, CancellationToken ct = default);

    Task<BulkAdjustmentResultDto> ExecuteBulkAdjustmentAsync(BulkAdjustmentRequest request, CancellationToken ct = default);

    Task<IEnumerable<AuditLogEntryDto>> GetAuditLogsAsync(Guid subjectId, string subjectType, CancellationToken ct = default);
    Task<IEnumerable<AuditLogEntryDto>> GetAuditLogsByEventTypeAsync(AuditEventType eventType, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    Task<ReconciliationReportDto> GenerateReconciliationReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    Task<FinanceParameterDto> GetParameterAsync(string key, Guid? cityId, string? serviceType, CancellationToken ct = default);
    Task<FinanceParameterDto> UpdateParameterAsync(UpdateParameterRequest request, CancellationToken ct = default);
    Task<IEnumerable<FinanceParameterDto>> GetAllParametersAsync(CancellationToken ct = default);
}

// ── Corporate Billing Service ──────────────────────────────────────────────────

public interface ICorporateBillingService
{
    Task<CorporateAccountDto> GetAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<CorporateAccountDto> CreateAccountAsync(CreateCorporateAccountRequest request, CancellationToken ct = default);
    Task<CorporateAccountDto> UpdateBillingModelAsync(Guid accountId, UpdateBillingModelRequest request, CancellationToken ct = default);

    /// <summary>
    /// Validates booking at booking time — blocks if Corporate Wallet insufficient
    /// or employee/cost-center/account budget exceeded.
    /// </summary>
    Task<BookingValidationResultDto> ValidateBookingAsync(Guid corporateAccountId, Guid employeeUserId, decimal estimatedFare, CancellationToken ct = default);

    /// <summary>
    /// Atomic split-pay calculation — both company and employee portions must
    /// succeed or both are reversed.
    /// </summary>
    Task<SplitPayResultDto> ProcessSplitPayAsync(ProcessSplitPayRequest request, CancellationToken ct = default);

    Task<CorporateInvoiceDto> GenerateInvoiceAsync(Guid corporateAccountId, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken ct = default);
    Task<IEnumerable<CorporateInvoiceDto>> GetInvoicesAsync(Guid corporateAccountId, CancellationToken ct = default);
    Task<CorporateInvoiceDto> GetInvoiceAsync(Guid invoiceId, CancellationToken ct = default);

    Task<EmployeeBudgetDto> GetEmployeeBudgetAsync(Guid corporateAccountId, Guid userId, CancellationToken ct = default);
    Task UpdateEmployeeBudgetAsync(Guid corporateAccountId, Guid userId, decimal newBudget, Guid changedByAdminId, CancellationToken ct = default);

    Task<IEnumerable<CorporateAccountDto>> GetAccountsBelowAlertThresholdAsync(CancellationToken ct = default);
}

// ── Refund Service (UC-FIN-REFUND-01) ──────────────────────────────────────────

public interface IRefundService
{
    /// <summary>
    /// UC-FIN-REFUND-01: Initiate full refund.
    /// Validates refund window, calculates commission reversal, routes refund
    /// via original payment method (Card → gateway reversal, Wallet → instant credit).
    /// Driver/merchant wallet debited for net; platform adjusted for commission reversal.
    /// All wallet updates atomic via Saga pattern.
    /// </summary>
    Task<RefundDto> InitiateRefundAsync(InitiateRefundRequest request, CancellationToken ct = default);

    /// <summary>Get refund by ID.</summary>
    Task<RefundDto> GetRefundAsync(Guid refundId, CancellationToken ct = default);

    /// <summary>Get all refunds for a transaction.</summary>
    Task<RefundDto?> GetRefundByTransactionAsync(Guid transactionId, CancellationToken ct = default);

    /// <summary>Get all refunds initiated by an actor.</summary>
    Task<IEnumerable<RefundDto>> GetRefundsByActorAsync(Guid actorId, CancellationToken ct = default);
}

// ── KYC / Payout Account Service ──────────────────────────────────────────────

public interface IKycService
{
    Task<PayoutAccountDto> AddPayoutAccountAsync(AddPayoutAccountRequest request, CancellationToken ct = default);
    Task<PayoutAccountDto> GetPayoutAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<IEnumerable<PayoutAccountDto>> GetPayoutAccountsByActorAsync(Guid actorId, CancellationToken ct = default);

    /// <summary>Webhook handler: KYC partner response → set VERIFIED or REJECTED.</summary>
    Task HandleKycWebhookAsync(KycWebhookPayload payload, CancellationToken ct = default);

    /// <summary>Validate IBAN at save time against SAMA National IBAN Registry.</summary>
    Task<bool> ValidateIbanAsync(string iban, CancellationToken ct = default);
}

// ── Audit Service ──────────────────────────────────────────────────────────────

public interface IAuditService
{
    /// <summary>
    /// Write immutable audit log entry synchronously before response is returned.
    /// SHA-256 tamper hash computed per entry.
    /// </summary>
    Task WriteAsync(AuditLogRequest request, CancellationToken ct = default);

    Task<IEnumerable<AuditLogEntryDto>> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
    Task ValidateTamperHashesAsync(CancellationToken ct = default);
}
