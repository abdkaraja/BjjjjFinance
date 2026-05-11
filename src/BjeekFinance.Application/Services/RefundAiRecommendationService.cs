using System.Text.Json;
using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Application.Services;

/// <summary>
/// UC-FIN-REFUND-ENGINE-01: AI Recommendation engine (advisory only).
/// Generates a suggested approve/deny decision with confidence, suggested amount,
/// and reasoning. The approver is NOT bound by this recommendation.
///
/// Currently uses deterministic rules as a stub. Future: integrate ML model endpoint.
/// </summary>
public interface IRefundAiRecommendationService
{
    Task<AiRecommendation> GenerateAsync(
        decimal requestedAmount,
        CustomerVipTier vipTier,
        int fraudScore,
        RefundCategory category,
        int refundCountLast12Months,
        decimal refundRatePercent,
        decimal lifetimeSpend,
        CancellationToken ct = default);

    string Serialize(AiRecommendation rec);
    AiRecommendation? Deserialize(string json);
}

public record AiRecommendation(
    string SuggestedDecision,    // "APPROVE" | "DENY"
    int ConfidencePercent,
    decimal? SuggestedAmount,
    string Reasoning
);

public class RefundAiRecommendationService : IRefundAiRecommendationService
{
    public Task<AiRecommendation> GenerateAsync(
        decimal requestedAmount,
        CustomerVipTier vipTier,
        int fraudScore,
        RefundCategory category,
        int refundCountLast12Months,
        decimal refundRatePercent,
        decimal lifetimeSpend,
        CancellationToken ct = default)
    {
        var reasons = new List<string>();
        var confidence = 75;
        var suggestedAmount = (decimal?)null;
        var suggestedDecision = "APPROVE";

        // Fraud score assessment
        if (fraudScore >= 40)
        {
            suggestedDecision = "DENY";
            reasons.Add($"Fraud score ({fraudScore}) is elevated.");
            confidence = 85;
        }
        else if (fraudScore >= 30)
        {
            reasons.Add($"Fraud score ({fraudScore}) is moderate.");
            confidence = 65;
        }

        // Refund rate assessment
        if (refundRatePercent > 20m)
        {
            suggestedDecision = "DENY";
            reasons.Add($"Refund rate ({refundRatePercent:F1}%) exceeds 20% threshold.");
            confidence = Math.Min(confidence + 10, 95);
        }
        else if (refundRatePercent > 10m)
        {
            reasons.Add($"Refund rate ({refundRatePercent:F1}%) is above average.");
            confidence -= 10;
        }

        // VIP consideration
        if (vipTier is CustomerVipTier.Gold or CustomerVipTier.Platinum && suggestedDecision == "APPROVE")
        {
            reasons.Add($"Customer is {vipTier} tier with lifetime spend of {lifetimeSpend:F2} SAR.");
            confidence = Math.Min(confidence + 10, 95);
        }

        // Category-specific logic
        if (category == RefundCategory.SafetyConcern)
        {
            suggestedDecision = "APPROVE";
            reasons.Add("Safety Concern category — recommend approve subject to review.");
            confidence = Math.Max(confidence - 15, 50);
        }

        // Suggested amount adjustment (advisory)
        if (suggestedDecision == "APPROVE" && refundRatePercent > 15m)
        {
            suggestedAmount = Math.Round(requestedAmount * 0.9m, 2);
            reasons.Add($"Suggested 10% reduction due to elevated refund rate.");
        }

        if (reasons.Count == 0)
            reasons.Add("No adverse indicators detected.");

        var result = new AiRecommendation(
            suggestedDecision,
            confidence,
            suggestedAmount,
            string.Join(" ", reasons));

        return Task.FromResult(result);
    }

    public string Serialize(AiRecommendation rec)
        => JsonSerializer.Serialize(rec);

    public AiRecommendation? Deserialize(string json)
        => JsonSerializer.Deserialize<AiRecommendation>(json);
}
