using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Domain.Entities;

/// <summary>
/// Verified payout destination (IBAN or mobile wallet).
/// IBAN validated at save time against SAMA National IBAN Registry — not at payout time.
/// AES-256 encryption applied to all stored KYC data.
/// </summary>
public class PayoutAccount : BaseEntity
{
    public Guid ActorId { get; set; }
    public PayoutDestinationType DestinationType { get; set; }

    /// <summary>Encrypted IBAN (24-char SA format) or mobile number.</summary>
    public string AccountIdentifier { get; set; } = string.Empty;

    public string AccountHolderName { get; set; } = string.Empty;
    public KycStatus VerificationStatus { get; set; } = KycStatus.Pending;
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Checked via payment processor API at registration time.
    /// Stored as flag — driver informed upfront if card does not support fast funds.
    /// </summary>
    public bool CardFastFundEligible { get; set; } = false;

    public string? KycDocumentReferences { get; set; } // JSON array of doc refs
    public Guid? AuditLogEntryId { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────────
    public ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
    public ICollection<PayoutRequest> PayoutRequests { get; set; } = new List<PayoutRequest>();
}

/// <summary>
/// Immutable ledger transaction record.
/// Every multi-wallet write uses Saga pattern with full rollback — no partial states.
/// </summary>
public class Transaction : BaseEntity
{
    public Guid WalletId { get; set; }
    public Guid? RideId { get; set; }
    public Guid? OrderId { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TipAmount { get; set; }
    public decimal FleetFeeAmount { get; set; }
    public decimal PenaltyAmount { get; set; }
    public decimal ChargebackAmount { get; set; }

    /// <summary>Net = Gross − Commission − Fleet Fee − Penalties − Chargebacks.</summary>
    public decimal NetAmount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>Webhook idempotency key — duplicate events must not cause double ledger entries.</summary>
    public string? IdempotencyKey { get; set; }

    public string? PspTransactionId { get; set; }
    public string? InvoiceId { get; set; }
    public bool IsReversed { get; set; } = false;
    public string? SagaCorrelationId { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────────
    public Wallet Wallet { get; set; } = null!;
    public ICollection<AuditLogEntry> AuditLogs { get; set; } = new List<AuditLogEntry>();
}

/// <summary>
/// Standard payout request to bank (UC-FIN-PAYOUT-01).
/// IBAN transfers routed via SARIE (Sun–Thu 08:00–16:00 AST).
/// Out-of-window transfers queued — never silently deferred.
/// </summary>
public class PayoutRequest : BaseEntity
{
    public Guid ActorId { get; set; }
    public Guid WalletId { get; set; }
    public Guid PayoutAccountId { get; set; }

    public decimal AmountRequested { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal NetTransferAmount { get; set; }

    public PayoutDestinationType DestinationType { get; set; }
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
    public SarieWindowStatus SarieWindowStatus { get; set; } = SarieWindowStatus.Open;

    public string? TransferReference { get; set; }
    public string? PspTransactionId { get; set; }

    /// <summary>Approver actor id — required above auto-approve threshold.</summary>
    public Guid? ApprovedByActorId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReasonCode { get; set; }

    /// <summary>Scheduled SARIE window when transfer is queued.</summary>
    public DateTime? ScheduledAt { get; set; }

    public int RetryCount { get; set; } = 0;
    public Guid? AuditLogEntryId { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────────
    public Wallet Wallet { get; set; } = null!;
    public PayoutAccount PayoutAccount { get; set; } = null!;
}
