using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Domain.Entities;

/// <summary>
/// UC-AD-FIN-05: Fraud case with full incident lifecycle.
/// Created by rule engine evaluation or manual escalation.
/// Lifecycle: OPEN → INVESTIGATING → RESOLVED | ARCHIVED | FALSE_POSITIVE.
/// </summary>
public class FraudCase : BaseEntity
{
    public string RuleKey { get; set; } = string.Empty;

    /// <summary>Subject of the investigation.</summary>
    public Guid ActorId { get; set; }
    public ActorType ActorType { get; set; }

    /// <summary>JSON serialized event data that triggered the case.</summary>
    public string TriggerEvent { get; set; } = string.Empty;

    public FraudSeverity Severity { get; set; }
    public FraudCaseStatus Status { get; set; } = FraudCaseStatus.Open;

    /// <summary>Auto-action taken (none/suspend/freeze).</summary>
    public FraudAutoAction AutoActionTaken { get; set; } = FraudAutoAction.NotifyOnly;

    /// <summary>Fraud team member assigned to investigate.</summary>
    public Guid? AssignedToActorId { get; set; }

    /// <summary>JSON array of investigation note entries.</summary>
    public string InvestigationNotesJson { get; set; } = "[]";

    public FraudResolutionCode? ResolutionCode { get; set; }
    public string? ResolutionNotes { get; set; }

    /// <summary>If FalsePositive, optionally whitelist the actor for this rule.</summary>
    public bool Whitelisted { get; set; }

    /// <summary>Link to related entity (settlement, cashout, transaction).</summary>
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByActorId { get; set; }

    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedByActorId { get; set; }
}
