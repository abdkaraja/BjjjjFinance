using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Domain.Entities;

/// <summary>
/// UC-FIN-REFUND-01/02: Full or partial refund record.
/// Tracks the complete lifecycle of a payment reversal including
/// commission reversal, VAT reversal, and destination method.
/// </summary>
public class Refund : BaseEntity
{
    public Guid OriginalTransactionId { get; set; }

    /// <summary>FULL or PARTIAL.</summary>
    public string RefundType { get; set; } = "FULL";

    public decimal Amount { get; set; }

    /// <summary>Partial refund amount (null for FULL).</summary>
    public decimal? PartialAmount { get; set; }

    /// <summary>JSON array of refunded items (null for FULL).</summary>
    public string? ItemsRefunded { get; set; }

    /// <summary>Commission reversed back to actor (100% for full refund; proportional for partial).</summary>
    public decimal CommissionReversalAmount { get; set; }

    /// <summary>VAT component of commission being reversed.</summary>
    public decimal VatReversalAmount { get; set; }

    public string ReasonCode { get; set; } = string.Empty;
    public PayoutDestinationType DestinationMethod { get; set; }
    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    public Guid? ApprovedByActorId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReasonCode { get; set; }

    /// <summary>Actor who initiated the refund (user or admin).</summary>
    public Guid ActorId { get; set; }
    public ActorRole ActorRole { get; set; }

    /// <summary>User wallet that received the refund credit (null for card refunds).</summary>
    public Guid? UserWalletId { get; set; }

    /// <summary>Driver/merchant wallet debited for the net amount.</summary>
    public Guid WalletId { get; set; }

    /// <summary>Platform wallet that absorbed the commission reversal.</summary>
    public Guid PlatformWalletId { get; set; }

    /// <summary>PSP reversal reference (card transactions).</summary>
    public string? PspReversalReference { get; set; }

    public Guid? AuditLogEntryId { get; set; }
    public DateTime? CompletedAt { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────────
    public Wallet Wallet { get; set; } = null!;
    public Wallet? UserWallet { get; set; }
    public Transaction OriginalTransaction { get; set; } = null!;
}
