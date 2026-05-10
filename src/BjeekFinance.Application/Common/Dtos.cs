using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Application.Common;

// ── Wallet DTOs ────────────────────────────────────────────────────────────────

public record WalletDto(
    Guid WalletId,
    ActorType ActorType,
    Guid ActorId,
    string Currency,
    decimal BalanceAvailable,
    decimal BalancePending,
    decimal BalanceHold,
    decimal CashReceivable,
    decimal BalanceRefundCredit,
    decimal BalancePromoCredit,
    decimal BalanceCourtesyCredit,
    int LoyaltyPoints,
    KycStatus KycStatus,
    InstantPayTier InstantPayTier,
    bool InstantPayEnabled,
    bool IsInDunning,
    DunningBucket? DunningBucket,
    int FraudScore,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record WalletSummaryDto(
    Guid WalletId,
    Guid ActorId,
    ActorType ActorType,
    decimal BalanceAvailable,
    decimal BalancePending,
    KycStatus KycStatus,
    bool IsInDunning,
    int FraudScore
);

// ── Payment DTOs ───────────────────────────────────────────────────────────────

public record CollectPaymentRequest(
    Guid RideId,
    Guid? OrderId,
    Guid DriverOrDeliveryActorId,
    Guid? MerchantActorId,
    Guid UserActorId,
    decimal GrossAmount,
    decimal CommissionRate,
    decimal TipAmount,
    decimal FleetFeePercent,
    PaymentMethod PaymentMethod,
    string? PromoCode,
    string? IdempotencyKey,
    string? PspTransactionId,
    Guid? CityId,
    string? ServiceType
);

public record CollectPaymentResultDto(
    Guid TransactionId,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal VatAmount,
    decimal TipAmount,
    decimal FleetFeeAmount,
    decimal NetDriverAmount,
    decimal NetMerchantAmount,
    PaymentMethod PaymentMethod,
    string InvoiceId,
    IEnumerable<WalletDeltaDto> WalletDeltas,
    DateTime Timestamp
);

public record WalletDeltaDto(
    Guid WalletId,
    ActorType ActorType,
    decimal Delta,
    string Description
);

public record AddTipRequest(
    Guid RideId,
    Guid DriverId,
    Guid UserId,
    decimal TipAmount,
    TipType TipType,
    PaymentMethod Source,
    string? IdempotencyKey
);

public record TipResultDto(
    Guid TipId,
    Guid RideId,
    Guid DriverId,
    decimal TipAmount,
    TipType TipType,
    PaymentMethod Source,
    DateTime Timestamp
);

public record TransactionDto(
    Guid TransactionId,
    Guid WalletId,
    Guid? RideId,
    Guid? OrderId,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal VatAmount,
    decimal TipAmount,
    decimal NetAmount,
    PaymentMethod PaymentMethod,
    string? PspTransactionId,
    string? InvoiceId,
    bool IsReversed,
    DateTime CreatedAt
);

// ── Payout DTOs ────────────────────────────────────────────────────────────────

public record InitiatePayoutRequest(
    Guid ActorId,
    Guid WalletId,
    Guid PayoutAccountId,
    decimal AmountRequested,
    Guid? CityId
);

public record PayoutRequestDto(
    Guid PayoutId,
    Guid ActorId,
    Guid WalletId,
    decimal AmountRequested,
    decimal FeeAmount,
    decimal NetTransferAmount,
    PayoutDestinationType DestinationType,
    PayoutStatus Status,
    SarieWindowStatus SarieWindowStatus,
    string? TransferReference,
    DateTime? ApprovedAt,
    DateTime? ScheduledAt,
    int RetryCount,
    DateTime CreatedAt
);

// ── Instant Pay DTOs ───────────────────────────────────────────────────────────

public record InstantPayRequest(
    Guid ActorId,
    Guid WalletId,
    decimal AmountRequested,
    Guid PayoutAccountId,
    bool IsAutoTriggered = false
);

public record InstantPayResultDto(
    Guid InstantPayId,
    Guid ActorId,
    decimal AmountRequested,
    decimal FeeAmount,
    decimal VatOnFee,
    decimal NetTransferAmount,
    TransferRail TransferRail,
    PayoutStatus TransferStatus,
    string? TransferReference,
    string? MicroInvoiceId,
    int DailyCountAfter,
    bool IsFallback,
    DateTime Timestamp
);

public record InstantPayCashoutDto(
    Guid InstantPayId,
    Guid ActorId,
    decimal AmountRequested,
    decimal FeeAmount,
    decimal NetTransferAmount,
    TransferRail TransferRail,
    PayoutStatus TransferStatus,
    bool IsAutoTriggered,
    bool IsFallback,
    DateTime Timestamp
);

public record EligibilityResultDto(
    bool IsEligible,
    string? FailureReason,
    decimal AvailableBalance,
    decimal MinimumRequired,
    int DailyCountRemaining,
    InstantPayTier Tier,
    decimal FeeAmount,
    decimal VatOnFee
);

// ── KYC / Payout Account DTOs ──────────────────────────────────────────────────

public record AddPayoutAccountRequest(
    Guid ActorId,
    PayoutDestinationType DestinationType,
    string AccountIdentifier,
    string AccountHolderName,
    IEnumerable<string> KycDocumentReferences,
    Guid? CityId
);

public record PayoutAccountDto(
    Guid AccountId,
    Guid ActorId,
    PayoutDestinationType DestinationType,
    string AccountHolderName,
    KycStatus VerificationStatus,
    string? RejectionReason,
    bool CardFastFundEligible,
    DateTime CreatedAt
);

public record KycWebhookPayload(
    Guid AccountId,
    KycStatus Status,
    string? RejectionReason,
    string PartnerReference,
    DateTime Timestamp
);

// ── Admin / Finance Ops DTOs ───────────────────────────────────────────────────

public record DunningStatusDto(
    Guid WalletId,
    Guid ActorId,
    bool IsInDunning,
    DunningBucket? Bucket,
    DateTime? DunningStartedAt,
    decimal CashReceivable,
    decimal BalanceAvailable
);

public record WriteOffRequest(
    Guid WalletId,
    decimal Amount,
    WriteOffReasonCode ReasonCode,
    string? Notes,
    Guid InitiatedByActorId
);

public record WriteOffResultDto(
    Guid WriteOffId,
    Guid WalletId,
    decimal Amount,
    WriteOffReasonCode ReasonCode,
    string Status,
    Guid? ApprovedByActorId,
    DateTime CreatedAt
);

public record BulkAdjustmentRequest(
    IEnumerable<WalletAdjustmentItem> Adjustments,
    string Reason,
    Guid InitiatedByActorId
);

public record WalletAdjustmentItem(
    Guid WalletId,
    decimal Delta,
    string Note
);

public record BulkAdjustmentResultDto(
    Guid BatchId,
    int TotalAdjustments,
    decimal TotalAmount,
    string Status,
    DateTime Timestamp
);

public record AuditLogEntryDto(
    Guid LogId,
    AuditEventType EventType,
    string EventSubtype,
    Guid ActorId,
    ActorRole ActorRole,
    Guid SubjectId,
    string SubjectType,
    string? Delta,
    DateTime Timestamp,
    string TamperHash
);

public record AuditLogRequest(
    AuditEventType EventType,
    string EventSubtype,
    Guid ActorId,
    ActorRole ActorRole,
    Guid SubjectId,
    string SubjectType,
    object? BeforeState,
    object? AfterState,
    Guid? CityId,
    string? IpAddress,
    string? DeviceId
);

public record AuditLogQuery(
    Guid? SubjectId,
    string? SubjectType,
    Guid? ActorId,
    AuditEventType? EventType,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 50
);

public record ReconciliationReportDto(
    Guid ReportId,
    DateTimeOffset From,
    DateTimeOffset To,
    decimal TotalCollected,
    decimal TotalCommission,
    decimal TotalVat,
    decimal TotalPayouts,
    decimal TotalInstantPayFees,
    decimal TotalRefunds,
    decimal TotalWriteOffs,
    IEnumerable<ReconciliationLineDto> Lines,
    DateTime GeneratedAt
);

public record ReconciliationLineDto(
    string Category,
    decimal Amount,
    int Count
);

public record FinanceParameterDto(
    Guid ParameterId,
    string Key,
    string Value,
    string? Description,
    Guid? CityId,
    string? ServiceType,
    int Version,
    DateTime EffectiveFrom
);

public record UpdateParameterRequest(
    string Key,
    string Value,
    Guid? CityId,
    string? ServiceType,
    Guid ChangedByActorId,
    string? Description
);

// ── Corporate Billing DTOs ─────────────────────────────────────────────────────

public record CreateCorporateAccountRequest(
    string CompanyName,
    string VatRegistrationNumber,
    string TradeLicenseNumber,
    string AuthorizedSignatoryId,
    CorporateBillingModel BillingModel,
    CorporatePaymentTerms PaymentTerms,
    decimal NegotiatedDiscountPercent,
    decimal CreditLimit,
    decimal LowBalanceAlertThreshold,
    decimal MonthlyBudgetCap,
    Guid CreatedByActorId
);

public record CorporateAccountDto(
    Guid AccountId,
    string CompanyName,
    string VatRegistrationNumber,
    CorporateBillingModel BillingModel,
    CorporatePaymentTerms PaymentTerms,
    decimal NegotiatedDiscountPercent,
    Guid WalletId,
    decimal WalletBalance,
    decimal CreditLimit,
    decimal MonthlyBudgetCap,
    decimal MonthlyBudgetConsumed,
    bool IsActive,
    int ContractVersion,
    DateTime CreatedAt
);

public record UpdateBillingModelRequest(
    CorporateBillingModel NewBillingModel,
    CorporatePaymentTerms NewPaymentTerms,
    Guid ChangedByAdminId,
    string ApprovalReference
);

public record BookingValidationResultDto(
    bool IsAllowed,
    string? BlockReason,
    decimal CompanyPayPortion,
    decimal EmployeePayPortion,
    decimal RemainingEmployeeBudget,
    decimal RemainingAccountBudget
);

public record ProcessSplitPayRequest(
    Guid CorporateAccountId,
    Guid EmployeeUserId,
    Guid RideId,
    decimal GrossFare,
    decimal CompanyCap,
    string? IdempotencyKey
);

public record SplitPayResultDto(
    decimal CompanyPortion,
    decimal EmployeePortion,
    string Status
);

public record CorporateInvoiceDto(
    Guid InvoiceId,
    Guid CorporateAccountId,
    string InvoiceNumber,
    decimal SubtotalAmount,
    decimal VatAmount,
    decimal TotalAmount,
    DateTime InvoiceDate,
    DateTime DueDate,
    bool IsPaid,
    DateTime? PaidAt
);

public record EmployeeBudgetDto(
    Guid UserId,
    Guid CorporateAccountId,
    string CostCenter,
    decimal MonthlyBudget,
    decimal MonthlyBudgetConsumed,
    decimal MonthlyAllowance,
    decimal AllowanceConsumed
);

public record DunningBucketInfo(string Bucket, DateTime Since);

// ── Refund DTOs (UC-FIN-REFUND-01) ──────────────────────────────────────────────

public record InitiateRefundRequest(
    Guid OriginalTransactionId,
    Guid ActorId,
    ActorRole ActorRole,
    string ReasonCode,
    string RefundType = "FULL",
    decimal? PartialAmount = null,
    string? ItemsRefunded = null,
    Guid? UserActorId = null
);

public record RefundDto(
    Guid RefundId,
    Guid OriginalTransactionId,
    string RefundType,
    decimal Amount,
    decimal? PartialAmount,
    string? ItemsRefunded,
    decimal CommissionReversalAmount,
    decimal VatReversalAmount,
    string ReasonCode,
    PayoutDestinationType DestinationMethod,
    RefundStatus Status,
    Guid ActorId,
    ActorRole ActorRole,
    string? PspReversalReference,
    DateTime CreatedAt,
    DateTime? CompletedAt
);
