using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Application.Services;

/// <summary>
/// UC-FIN-REFUND-ENGINE-01 §13.2: Auto-Approval Rule Engine.
/// Evaluates whether a refund qualifies for auto-approval based on
/// amount, customer VIP tier, fraud score, and refund category.
/// Business rules:
///   - VIP customers (Silver, Gold, Platinum) receive elevated thresholds.
///   - Fraud score >= 50 blocks auto-approval regardless of amount or VIP tier.
///   - Safety Concern category always requires manual review.
///   - Amount must be <= VIP-tier threshold.
/// </summary>
public interface IRefundAutoApprovalEngine
{
    Task<AutoApprovalResult> EvaluateAsync(
        decimal amount,
        CustomerVipTier vipTier,
        int fraudScore,
        RefundCategory category,
        Guid? cityId,
        string? serviceType,
        CancellationToken ct = default);
}

public record AutoApprovalResult(
    bool IsAutoApproved,
    string? BlockReason,
    decimal ApplicableThreshold
);

public class RefundAutoApprovalEngine : IRefundAutoApprovalEngine
{
    private readonly IUnitOfWork _uow;

    public RefundAutoApprovalEngine(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<AutoApprovalResult> EvaluateAsync(
        decimal amount,
        CustomerVipTier vipTier,
        int fraudScore,
        RefundCategory category,
        Guid? cityId,
        string? serviceType,
        CancellationToken ct = default)
    {
        // BR5: Fraud score >= 50 blocks auto-approval
        if (fraudScore >= 50)
            return new AutoApprovalResult(false, $"Fraud score ({fraudScore}) >= 50. Manual review required.", 0);

        // Safety Concern always manual
        if (category == RefundCategory.SafetyConcern)
            return new AutoApprovalResult(false, "Safety Concern category requires manual review.", 0);

        // Resolve VIP-tier threshold
        var tierLabel = vipTier switch
        {
            CustomerVipTier.Platinum => "platinum",
            CustomerVipTier.Gold => "gold",
            CustomerVipTier.Silver => "silver",
            _ => "standard"
        };

        var threshold = await _uow.FinanceParameters.GetDecimalAsync(
            $"refund_auto_approve_threshold_{tierLabel}",
            GetDefaultThreshold(vipTier),
            cityId, serviceType, ct);

        if (amount > threshold)
            return new AutoApprovalResult(false,
                $"Refund amount ({amount:F2} SAR) exceeds {tierLabel} auto-approval threshold ({threshold:F2} SAR).",
                threshold);

        return new AutoApprovalResult(true, null, threshold);
    }

    private static decimal GetDefaultThreshold(CustomerVipTier tier) => tier switch
    {
        CustomerVipTier.Platinum => 1000m,
        CustomerVipTier.Gold => 500m,
        CustomerVipTier.Silver => 250m,
        _ => 100m
    };
}
