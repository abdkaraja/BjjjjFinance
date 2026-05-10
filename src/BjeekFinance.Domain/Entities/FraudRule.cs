using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Domain.Entities;

/// <summary>
/// UC-AD-FIN-05: Admin-configurable fraud detection rule.
/// Each rule defines a trigger pattern, threshold, severity, and auto-action.
/// Domain-specific rules maintained separately per service type.
/// </summary>
public class FraudRule : BaseEntity
{
    /// <summary>Unique rule identifier used in trigger events (e.g. "CASH_VARIANCE_HIGH").</summary>
    public string RuleKey { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>The domain this rule applies to: "ride", "delivery", "grocery", "wallet", "all".</summary>
    public string Domain { get; set; } = "all";

    /// <summary>Threshold value that triggers this rule.</summary>
    public decimal Threshold { get; set; }

    public FraudSeverity Severity { get; set; } = FraudSeverity.Medium;

    public FraudAutoAction AutoAction { get; set; } = FraudAutoAction.NotifyOnly;

    /// <summary>Window in hours for time-based rules (e.g. 7-day = 168h).</summary>
    public int? WindowHours { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid ChangedByActorId { get; set; }
    public DateTime? DeactivatedAt { get; set; }
}
