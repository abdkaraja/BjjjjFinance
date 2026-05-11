using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Domain.Entities;

/// <summary>
/// UC-FIN-REFUND-ENGINE-01: Refund Request & Auto-Routing.
/// Tracks complete lifecycle: agent submission → pre-flight → auto-approval or
/// approval-tier routing → approver review → execution → audit.
/// </summary>
public class Refund : BaseEntity
{
    // ── Transaction context ────────────────────────────────────────────────────
    public Guid OriginalTransactionId { get; set; }

    /// <summary>FULL or PARTIAL.</summary>
    public string RefundType { get; set; } = "FULL";

    public decimal Amount { get; set; }

    /// <summary>Partial refund amount (null for FULL).</summary>
    public decimal? PartialAmount { get; set; }

    /// <summary>JSON array of refunded items (null for FULL).</summary>
    public string? ItemsRefunded { get; set; }

    // ── UC-FIN-REFUND-ENGINE-01 fields ─────────────────────────────────────────
    public RefundCategory RefundCategory { get; set; }

    /// <summary>Minimum 50-character justification from support agent.</summary>
    public string Justification { get; set; } = string.Empty;

    /// <summary>JSON array of evidence URLs (optional).</summary>
    public string? EvidenceUrls { get; set; }

    /// <summary>Support agent who initiated the request.</summary>
    public Guid InitiatedBySupportAgentId { get; set; }

    /// <summary>Customer's user actor ID from the original transaction.</summary>
    public Guid UserActorId { get; set; }

    /// <summary>Customer's VIP tier at time of request.</summary>
    public CustomerVipTier CustomerVipTier { get; set; } = CustomerVipTier.Standard;

    /// <summary>Fraud score captured at time of request gate.</summary>
    public int FraudScoreAtRequest { get; set; }

    // ── Pre-flight & auto-approval ─────────────────────────────────────────────
    /// <summary>Available-for-refund balance at time of request.</summary>
    public decimal AvailableForRefundBalance { get; set; }

    /// <summary>True when auto-approval rule engine approved (no human approver).</summary>
    public bool IsAutoApproved { get; set; }

    // ── Approval routing ───────────────────────────────────────────────────────
    /// <summary>Which approval tier this was routed to (null if auto-approved).</summary>
    public ApprovalTier? ApprovalTier { get; set; }

    /// <summary>Actor ID of the assigned approver.</summary>
    public Guid? AssignedApproverActorId { get; set; }

    /// <summary>When the request was assigned to the approver queue.</summary>
    public DateTime? AssignedAt { get; set; }

    // ── AI Recommendation (advisory only) ──────────────────────────────────────
    /// <summary>JSON: { suggestedDecision, confidencePercent, suggestedAmount, reasoning }.</summary>
    public string? AiRecommendationJson { get; set; }

    // ── Approver decision ──────────────────────────────────────────────────────
    /// <summary>APPROVE | DENY | REQUEST_MORE_INFO.</summary>
    public string? FinalDecision { get; set; }

    /// <summary>If approver adjusted the refund amount (different from requested).</summary>
    public decimal? ApprovedAdjustedAmount { get; set; }

    public string? ApproverNotes { get; set; }

    /// <summary>Actor ID of who made the decision.</summary>
    public Guid? ApprovedByActorId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReasonCode { get; set; }

    /// <summary>Customer wallet delta on approval execution.</summary>
    public decimal? CustomerWalletDelta { get; set; }

    // ── Driver warning ─────────────────────────────────────────────────────────
    /// <summary>True if approver selected the "warn driver" checkbox.</summary>
    public bool WarningSentToDriver { get; set; }

    // ── Request More Info flow ─────────────────────────────────────────────────
    public string? RequestMoreInfoNotes { get; set; }
    public DateTime? RequestMoreInfoRequestedAt { get; set; }
    public DateTime? RequestMoreInfoRespondedAt { get; set; }

    // ── SLA tracking ───────────────────────────────────────────────────────────
    /// <summary>Target SLA in hours (admin-configurable).</summary>
    public int SlaTargetHours { get; set; }
    public DateTime? SlaAssignedAt { get; set; }
    public DateTime? SlaReminderSentAt { get; set; }   // 75% breach → reminder
    public DateTime? SlaBreachedAt { get; set; }       // 100% → auto-escalate

    /// <summary>When SLA was paused (approver requested more info).</summary>
    public DateTime? SlaPausedAt { get; set; }
    public DateTime? SlaResumedAt { get; set; }

    // ── Escalation ─────────────────────────────────────────────────────────────
    /// <summary>Which actor this was escalated from (AF5 auto-escalation).</summary>
    public Guid? EscalatedFromActorId { get; set; }

    /// <summary>Approval tier that was breached due to SLA.</summary>
    public ApprovalTier? EscalatedFromTier { get; set; }

    // ── Financial reversal fields (existing) ────────────────────────────────────
    public decimal CommissionReversalAmount { get; set; }
    public decimal VatReversalAmount { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public PayoutDestinationType DestinationMethod { get; set; }
    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    public Guid ActorId { get; set; }
    public ActorRole ActorRole { get; set; }

    public Guid? UserWalletId { get; set; }
    public Guid WalletId { get; set; }
    public Guid PlatformWalletId { get; set; }

    public string? PspReversalReference { get; set; }
    public Guid? AuditLogEntryId { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>ID of the support ticket auto-closed on completion.</summary>
    public string? SupportTicketId { get; set; }

    /// <summary>Retry count for wallet credit / card reversal operations.</summary>
    public int RetryCount { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────────
    public Wallet Wallet { get; set; } = null!;
    public Wallet? UserWallet { get; set; }
    public Transaction OriginalTransaction { get; set; } = null!;
}
