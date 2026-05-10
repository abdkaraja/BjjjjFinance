namespace BjeekFinance.Domain.Enums;

public enum ActorType
{
    User,
    Driver,
    Delivery,
    Merchant,
    Platform,
    Fleet,
    Corporate
}

public enum KycStatus
{
    Unverified,
    Pending,
    Verified,
    Rejected
}

public enum InstantPayTier
{
    TierA,
    TierB,
    TierC
}

public enum PayoutDestinationType
{
    SaudiIban,
    StcPay,
    Urpay,
    DebitCard
}

public enum PayoutStatus
{
    Pending,
    Approved,
    Processing,
    Completed,
    Failed,
    Queued,
    Rejected
}

public enum PaymentMethod
{
    Card,
    Wallet,
    Cash,
    PartialWalletCard,
    Corporate
}

public enum TransferRail
{
    StcPay,
    IbanFast,
    IbanStandardFallback
}

public enum SarieWindowStatus
{
    Open,
    Closed,
    Queued
}

public enum ActorRole
{
    Driver,
    User,
    Merchant,
    FinanceAdmin,
    FinanceManager,
    FinanceOfficer,
    VpFinance,
    Cfo,
    SuperAdmin,
    FraudOfficer,
    FraudManager,
    CorporateAccountManager,
    CorporateHrAdmin,
    SupportAgent,
    System
}

public enum AuditEventType
{
    Payment,
    Wallet,
    Payout,
    InstantPay,
    Refund,
    Kyc,
    CashSettlement,
    Fraud,
    Vat,
    Config,
    AdminOverride
}

public enum DunningBucket
{
    Notify,
    HoldPayout,
    HoldAssignments
}

public enum CorporateBillingModel
{
    CompanyPay,
    SplitPay,
    VoucherAllowance,
    Reimbursement
}

public enum CorporatePaymentTerms
{
    Net30,
    Net15,
    DueOnReceipt
}

public enum RefundStatus
{
    Pending,
    AutoApproved,
    AwaitingApproval,
    Approved,
    Rejected,
    Processing,
    Completed
}

public enum TipType
{
    Fixed,
    Custom
}

public enum FraudSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum FraudCaseStatus
{
    Open,
    Investigating,
    Resolved,
    Archived,
    FalsePositive
}

public enum FraudAutoAction
{
    NotifyOnly,
    SuspendInstantPay,
    FreezeWallet
}

public enum FraudResolutionCode
{
    NoActionTaken,
    DriverRefunded,
    MerchantAdjusted,
    AccountSuspended,
    LegalEscalated,
    Whitelisted,
    Other
}

public enum CashSettlementStatus
{
    Pending,
    Submitted,
    AutoAdjusted,
    FlaggedForReview,
    Completed
}

public enum WriteOffReasonCode
{
    BadDebtDriverDeparted,
    DisputedUnresolvable,
    FraudLossUnrecoverable,
    TechnicalErrorAdjudicated,
    Other
}

public enum PayoutRejectionReasonCode
{
    InsufficientKyc,
    SuspectedFraud,
    InsufficientBalance,
    InvalidDestination,
    DocumentationPending,
    ComplianceHold,
    DuplicateRequest,
    Other
}
