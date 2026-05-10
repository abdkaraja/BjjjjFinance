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

    /// <summary>UC-AD-FIN-01: Search wallets with optional filters and pagination.</summary>
    Task<IEnumerable<WalletSummaryDto>> SearchWalletsAsync(Guid? actorId, ActorType? actorType, Guid? cityId, int skip = 0, int take = 50, CancellationToken ct = default);
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

    /// <summary>
    /// UC-AD-FIN-02: Approve a pending payout. Auto-executes transfer:
    /// STC Pay / open SARIE → Processing immediately.
    /// Closed SARIE → Queued with scheduled next window.
    /// Amount > superAdminThreshold requires actor with SuperAdmin role.
    /// </summary>
    Task<PayoutRequestDto> ApprovePayoutAsync(Guid payoutId, Guid approverActorId, bool scheduleForNextWindow = false, CancellationToken ct = default);

    /// <summary>
    /// UC-AD-FIN-02: Reject a pending payout with predefined reason code.
    /// Releases hold back to available. ReasonCode must be from PayoutRejectionReasonCode enum.
    /// </summary>
    Task<PayoutRequestDto> RejectPayoutAsync(Guid payoutId, Guid approverActorId, PayoutRejectionReasonCode reasonCode, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-02: Schedule an approved payout for a specific SARIE window.</summary>
    Task<PayoutRequestDto> SchedulePayoutAsync(Guid payoutId, DateTime scheduledUtc, CancellationToken ct = default);

    Task<IEnumerable<PayoutRequestDto>> GetPendingPayoutsAsync(CancellationToken ct = default);
    Task<IEnumerable<PayoutRequestDto>> GetByActorAsync(Guid actorId, CancellationToken ct = default);
    Task<PayoutRequestDto> GetByIdAsync(Guid payoutId, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-02: Pending queue sorted by amount desc, then oldest first.</summary>
    Task<IEnumerable<PendingPayoutQueueItemDto>> GetPendingQueueAsync(CancellationToken ct = default);

    /// <summary>UC-AD-FIN-02: Detailed payout review with wallet/account/KYC info.</summary>
    Task<PayoutReviewDto> GetPayoutReviewAsync(Guid payoutId, CancellationToken ct = default);

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

    /// <summary>UC-AD-FIN-06: Generate bulk platform reconciliation report.</summary>
    Task<BulkReconciliationReportDto> GenerateBulkReconciliationReportAsync(DateTime from, DateTime to, Guid? cityId, string? serviceType, Guid adminId, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-06: List previously generated bulk reconciliation reports.</summary>
    Task<IEnumerable<BulkReconciliationReportDto>> GetBulkReconciliationReportsAsync(DateTime from, DateTime to, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-06: Download bulk reconciliation report CSV.</summary>
    Task<string> GetBulkReconciliationReportCsvAsync(Guid reportId, CancellationToken ct = default);

    Task<ReconciliationReportDto> GenerateReconciliationReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    /// <summary>UC-AD-FIN-01: Export wallet data as CSV for selected actor type, city.</summary>
    Task<string> ExportWalletsCsvAsync(ActorType? actorType, Guid? cityId, CancellationToken ct = default);

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

// ── Cash Settlement Service (UC-FIN-CASH-01) ────────────────────────────────────

public interface ICashSettlementService
{
    /// <summary>
    /// UC-FIN-CASH-01: Driver submits daily cash settlement.
    /// System compares expected vs reported cash per trip.
    /// |Variance| ≤ SAR 3 → auto-adjust ledger.
    /// |Variance| > SAR 3 → flagged for Finance Admin review.
    /// On settlement completion: cash_receivable cleared.
    /// </summary>
    Task<CashSettlementDto> SubmitSettlementAsync(SubmitCashSettlementRequest request, CancellationToken ct = default);

    /// <summary>Finance Admin reviews and resolves a flagged settlement.</summary>
    Task<CashSettlementDto> ReviewSettlementAsync(Guid settlementId, Guid adminId, string resolutionNotes, CancellationToken ct = default);

    Task<CashSettlementDto> GetSettlementAsync(Guid settlementId, CancellationToken ct = default);
    Task<IEnumerable<CashSettlementDto>> GetByDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<IEnumerable<CashSettlementDto>> GetFlaggedForReviewAsync(CancellationToken ct = default);

    // ── UC-AD-FIN-03: Reconciliation Dashboard ───────────────────────────────

    /// <summary>Dashboard with variance severity buckets and counts.</summary>
    Task<ReconciliationDashboardDto> GetDashboardAsync(DateTime from, DateTime to, Guid? cityId = null, CancellationToken ct = default);

    /// <summary>Escalate a flagged settlement to the Fraud Detection service (UC-AD-FIN-05).</summary>
    Task<CashSettlementDto> EscalateToFraudAsync(Guid settlementId, Guid adminId, string notes, CancellationToken ct = default);

    /// <summary>Generate a reconciliation report for the given date range / city.</summary>
    Task<CashReconciliationReportDto> GenerateReportAsync(DateTime from, DateTime to, Guid? cityId, Guid adminId, CancellationToken ct = default);

    /// <summary>List previously generated reconciliation reports.</summary>
    Task<IEnumerable<CashReconciliationReportDto>> GetReportsAsync(DateTime from, DateTime to, Guid? cityId = null, CancellationToken ct = default);
}

// ── VAT Report Service (UC-AD-FIN-04) ──────────────────────────────────────────

public interface IVatReportService
{
    /// <summary>
    /// UC-AD-FIN-04: Generate ZATCA-compliant VAT report.
    /// Aggregates rides, orders, Instant Pay fee VAT, cancellation fees.
    /// Merchant-specific filter available. Missing tax config → flagged.
    /// CSV stored with report. ZATCA-compliant fields included.
    /// </summary>
    Task<VatReportDto> GenerateVatReportAsync(DateTime periodStart, DateTime periodEnd, Guid? merchantActorId = null, string? serviceType = null, CancellationToken ct = default);

    /// <summary>List previously generated VAT reports.</summary>
    Task<IEnumerable<VatReportDto>> GetVatReportsAsync(DateTime from, DateTime to, Guid? merchantActorId = null, CancellationToken ct = default);

    /// <summary>Get CSV content for a stored report by ID.</summary>
    Task<string> GetVatReportCsvAsync(Guid reportId, CancellationToken ct = default);
}

// ── Fraud Detection Service (UC-AD-FIN-05) ─────────────────────────────────────

public interface IFraudDetectionService
{
    /// <summary>Evaluate an event against active fraud rules. Creates case if triggered.</summary>
    Task<FraudCaseDto?> EvaluateEventAsync(string ruleKey, Guid actorId, string actorType, string triggerEvent, Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken ct = default);

    /// <summary>Manually create a fraud case (admin escalation).</summary>
    Task<FraudCaseDto> CreateCaseAsync(CreateFraudCaseRequest request, CancellationToken ct = default);

    /// <summary>Assign a fraud case to a team member.</summary>
    Task<FraudCaseDto> AssignCaseAsync(Guid caseId, AssignFraudCaseRequest request, CancellationToken ct = default);

    /// <summary>Add investigation note to a case.</summary>
    Task<FraudCaseDto> AddNoteAsync(Guid caseId, AddInvestigationNoteRequest request, CancellationToken ct = default);

    /// <summary>Resolve a fraud case with resolution code.</summary>
    Task<FraudCaseDto> ResolveCaseAsync(Guid caseId, ResolveFraudCaseRequest request, Guid resolvedByActorId, CancellationToken ct = default);

    /// <summary>Archive a fraud case.</summary>
    Task<FraudCaseDto> ArchiveCaseAsync(Guid caseId, Guid archivedByActorId, CancellationToken ct = default);

    /// <summary>Get dashboard with open cases grouped by severity.</summary>
    Task<IEnumerable<FraudCaseDto>> GetOpenCasesAsync(FraudSeverity minSeverity = FraudSeverity.Medium, CancellationToken ct = default);

    /// <summary>Get cases for a specific actor.</summary>
    Task<IEnumerable<FraudCaseDto>> GetCasesByActorAsync(Guid actorId, CancellationToken ct = default);

    /// <summary>Get fraud case by ID.</summary>
    Task<FraudCaseDto> GetCaseAsync(Guid caseId, CancellationToken ct = default);

    // ── Rule management ──────────────────────────────────────────────────────

    Task<IEnumerable<FraudRuleDto>> GetActiveRulesAsync(string? domain = null, CancellationToken ct = default);
    Task<FraudRuleDto> UpdateRuleAsync(string ruleKey, UpdateFraudRuleRequest request, CancellationToken ct = default);
    Task<FraudRuleDto> GetRuleAsync(string ruleKey, CancellationToken ct = default);
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
