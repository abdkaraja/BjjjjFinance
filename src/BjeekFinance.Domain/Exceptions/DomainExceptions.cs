using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Domain.Exceptions;

public abstract class DomainException : Exception
{
    public string ErrorCode { get; }
    protected DomainException(string errorCode, string message) : base(message)
        => ErrorCode = errorCode;
}

public class InsufficientBalanceException : DomainException
{
    public InsufficientBalanceException(decimal available, decimal requested)
        : base("INSUFFICIENT_BALANCE",
            $"Insufficient available balance. Available: {available:F2} SAR, Requested: {requested:F2} SAR.") { }
}

public class KycNotVerifiedException : DomainException
{
    public KycNotVerifiedException()
        : base("KYC_NOT_VERIFIED", "KYC verification is required before payout, Instant Pay, or wallet withdrawal.") { }
}

public class InstantPayNotEligibleException : DomainException
{
    public InstantPayNotEligibleException(string reason)
        : base("INSTANT_PAY_NOT_ELIGIBLE", $"Instant Pay eligibility check failed: {reason}") { }
}

public class InstantPayDailyLimitExceededException : DomainException
{
    public InstantPayDailyLimitExceededException(int limit, DateTime resetAt)
        : base("INSTANT_PAY_DAILY_LIMIT",
            $"Daily Instant Pay limit of {limit} cashouts reached. Resets at {resetAt:HH:mm} local time.") { }
}

public class DunningHoldException : DomainException
{
    public DunningHoldException(DunningBucketInfo bucket)
        : base("DUNNING_HOLD", $"Wallet is in dunning hold ({bucket}). Payouts and cashouts are suspended.") { }
}

public record DunningBucketInfo(string Bucket, DateTime Since);

public class SarieWindowClosedException : DomainException
{
    public SarieWindowClosedException(DateTime nextWindow)
        : base("SARIE_WINDOW_CLOSED",
            $"SARIE transfer window is closed. Transfer queued for next window: {nextWindow:dd MMM yyyy HH:mm} AST.") { }
}

public class WalletFrozenException : DomainException
{
    public WalletFrozenException()
        : base("WALLET_FROZEN", "All financial operations are suspended due to a critical fraud flag. Fraud Manager review required.") { }
}

public class IbanValidationException : DomainException
{
    public IbanValidationException()
        : base("IBAN_VALIDATION_FAILED", "IBAN bank code not recognized by SAMA registry.") { }
}

public class PayoutBelowMinimumException : DomainException
{
    public PayoutBelowMinimumException(decimal minimum)
        : base("PAYOUT_BELOW_MINIMUM", $"Payout amount is below the minimum threshold of {minimum:F2} SAR.") { }
}

public class CorporateBudgetExceededException : DomainException
{
    public CorporateBudgetExceededException(string scope)
        : base("CORPORATE_BUDGET_EXCEEDED", $"Trip blocked: {scope} budget has been exhausted.") { }
}

public class CorporateWalletInsufficientException : DomainException
{
    public CorporateWalletInsufficientException()
        : base("CORPORATE_WALLET_INSUFFICIENT", "Corporate Wallet balance is insufficient for the company-pay portion. Trip blocked at booking time.") { }
}

public class RefundWindowExpiredException : DomainException
{
    public RefundWindowExpiredException(string serviceType, int windowDays)
        : base("REFUND_WINDOW_EXPIRED", $"Refund window for {serviceType} has expired ({windowDays} days).") { }
}

public class IdempotencyConflictException : DomainException
{
    public IdempotencyConflictException(string key)
        : base("IDEMPOTENCY_CONFLICT", $"A transaction with idempotency key '{key}' has already been processed.") { }
}

// ── UC-FIN-REFUND-ENGINE-01 Exceptions ──────────────────────────────────────────

public class RefundPreFlightFailedException : DomainException
{
    public RefundPreFlightFailedException(string detail)
        : base("REFUND_PRE_FLIGHT_FAILED", $"Pre-flight check failed: {detail}") { }
}

public class RefundAmountExceedsAvailableException : DomainException
{
    public RefundAmountExceedsAvailableException(decimal requested, decimal available)
        : base("REFUND_AMOUNT_EXCEEDS_AVAILABLE",
            $"Refund amount ({requested:F2} SAR) exceeds available-for-refund balance ({available:F2} SAR).") { }
}

public class RefundAgentAuthorityExceededException : DomainException
{
    public RefundAgentAuthorityExceededException(decimal agentLimit)
        : base("REFUND_AGENT_AUTHORITY_EXCEEDED",
            $"Refund amount exceeds your request authority of {agentLimit:F2} SAR. Please route to a Finance Officer.") { }
}

public class RefundFraudScoreBlockedException : DomainException
{
    public RefundFraudScoreBlockedException(int fraudScore)
        : base("REFUND_FRAUD_SCORE_BLOCKED",
            $"Customer fraud score ({fraudScore}) exceeds the auto-approval threshold. Manual review required.") { }
}

public class RefundSlaBreachException : DomainException
{
    public RefundSlaBreachException(Guid refundId, ApprovalTier currentTier, ApprovalTier escalatedTo)
        : base("REFUND_SLA_BREACH",
            $"Refund {refundId} SLA breached at tier {currentTier}. Auto-escalated to {escalatedTo}.") { }
}

public class RefundAlreadyCompletedException : DomainException
{
    public RefundAlreadyCompletedException(Guid refundId)
        : base("REFUND_ALREADY_COMPLETED", $"Refund {refundId} has already been completed.") { }
}

public class RefundWalletCreditFailedException : DomainException
{
    public RefundWalletCreditFailedException(Guid refundId, string detail)
        : base("REFUND_WALLET_CREDIT_FAILED", $"Wallet credit for refund {refundId} failed: {detail}") { }
}

public class RefundCardReversalFailedException : DomainException
{
    public RefundCardReversalFailedException(Guid refundId, string detail)
        : base("REFUND_CARD_REVERSAL_FAILED", $"Card reversal for refund {refundId} failed at gateway: {detail}") { }
}
