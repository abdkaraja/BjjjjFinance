using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Domain.Entities;

/// <summary>
/// On-demand Instant Pay cashout (UC-FIN-INSTANT-01).
/// Separate code path from standard payout — no admin approval required.
/// Draws ONLY from AVAILABLE balance — never PENDING.
/// </summary>
public class InstantPayCashout : BaseEntity
{
    public Guid ActorId { get; set; }
    public Guid WalletId { get; set; }

    public decimal AmountRequested { get; set; }
    public decimal FeeAmount { get; set; }

    /// <summary>VAT component of fee recorded separately for ZATCA compliance.</summary>
    public decimal VatOnFee { get; set; }

    public decimal NetTransferAmount { get; set; }
    public PayoutDestinationType DestinationType { get; set; }
    public TransferRail TransferRail { get; set; }
    public PayoutStatus TransferStatus { get; set; } = PayoutStatus.Pending;

    public string? TransferReference { get; set; }

    /// <summary>ZATCA-compliant micro-invoice generated per cashout.</summary>
    public string? MicroInvoiceId { get; set; }

    public int DailyCountBefore { get; set; }
    public int DailyCountAfter { get; set; }

    /// <summary>City-local timestamp — daily limit resets at local midnight, not UTC.</summary>
    public DateTime CityLocalTime { get; set; }

    /// <summary>Whether this was triggered by auto-cashout threshold.</summary>
    public bool IsAutoTriggered { get; set; } = false;

    /// <summary>Whether this fell back from Tier 2 to standard payout.</summary>
    public bool IsFallback { get; set; } = false;

    /// <summary>
    /// Links to the standard PayoutRequest created during AF2 fallback.
    /// When non-null, CompletePayoutAsync on the linked PayoutRequest releases the hold.
    /// </summary>
    public Guid? PayoutRequestId { get; set; }

    public Guid? AuditLogEntryId { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────────
    public Wallet Wallet { get; set; } = null!;
    public PayoutRequest? PayoutRequest { get; set; }
}

/// <summary>
/// Immutable audit log entry (SRS-FIN-001 Compliance §1).
/// WORM storage — no delete or update operations permitted.
/// SHA-256 tamper hash per entry. Written synchronously before response returned.
/// </summary>
public class AuditLogEntry : BaseEntity
{
    public AuditEventType EventType { get; set; }
    public string EventSubtype { get; set; } = string.Empty; // e.g. PAYOUT_APPROVED
    public Guid ActorId { get; set; }
    public ActorRole ActorRole { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectType { get; set; } = string.Empty; // WALLET | PAYOUT | etc.

    /// <summary>Full entity state before action (JSON).</summary>
    public string? BeforeState { get; set; }

    /// <summary>Full entity state after action (JSON).</summary>
    public string? AfterState { get; set; }

    /// <summary>Computed difference (JSON).</summary>
    public string? Delta { get; set; }

    public Guid? CityId { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceId { get; set; }

    /// <summary>UTC timestamp with millisecond precision.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>City-local timestamp (timezone-aware).</summary>
    public DateTime? LocalTimestamp { get; set; }

    /// <summary>SHA-256 hash of full log entry for tamper detection.</summary>
    public string TamperHash { get; set; } = string.Empty;

    public Guid? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
}

/// <summary>
/// Corporate account entity for B2B billing (SRS-FIN-001 §19).
/// Corporate Wallet is debit-only — no outbound payouts.
/// </summary>
public class CorporateAccount : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string VatRegistrationNumber { get; set; } = string.Empty;
    public string TradeLicenseNumber { get; set; } = string.Empty;
    public string AuthorizedSignatoryId { get; set; } = string.Empty;

    public CorporateBillingModel BillingModel { get; set; }
    public CorporatePaymentTerms PaymentTerms { get; set; } = CorporatePaymentTerms.Net30;

    /// <summary>
    /// Negotiated discount % applied before commission calculation.
    /// Discount reduces Gross — not the Commission rate.
    /// </summary>
    public decimal NegotiatedDiscountPercent { get; set; } = 0;

    public Guid WalletId { get; set; }

    /// <summary>Pre-payment balance (Prepaid model) or credit limit (Postpaid).</summary>
    public decimal CreditLimit { get; set; } = 0;

    /// <summary>Low-balance alert threshold — admin configurable.</summary>
    public decimal LowBalanceAlertThreshold { get; set; } = 0;

    /// <summary>Monthly budget cap — block at 100% or require Finance Officer approval.</summary>
    public decimal MonthlyBudgetCap { get; set; } = 0;
    public decimal MonthlyBudgetConsumed { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Contract terms snapshot stored immutably at activation.
    /// Changes require new contract version + Finance Manager approval.
    /// </summary>
    public string? ContractTermsSnapshot { get; set; } // JSON

    public int ContractVersion { get; set; } = 1;

    // ── Navigation ─────────────────────────────────────────────────────────────
    public Wallet Wallet { get; set; } = null!;
    public ICollection<CorporateEmployee> Employees { get; set; } = new List<CorporateEmployee>();
    public ICollection<CorporateInvoice> Invoices { get; set; } = new List<CorporateInvoice>();
}

/// <summary>Employee within a corporate account with per-employee budget.</summary>
public class CorporateEmployee : BaseEntity
{
    public Guid CorporateAccountId { get; set; }
    public Guid UserId { get; set; }
    public string CostCenter { get; set; } = string.Empty;

    /// <summary>Per-employee monthly budget — blocked at booking time if exceeded.</summary>
    public decimal MonthlyBudget { get; set; } = 0;
    public decimal MonthlyBudgetConsumed { get; set; } = 0;

    /// <summary>Monthly SAR allowance (Voucher/Allowance model). Resets 1st of month. Does NOT roll over.</summary>
    public decimal MonthlyAllowance { get; set; } = 0;
    public decimal AllowanceConsumed { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    // ── Navigation ─────────────────────────────────────────────────────────────
    public CorporateAccount CorporateAccount { get; set; } = null!;
}

/// <summary>
/// ZATCA-compliant corporate invoice (UC-FIN-CORP-INVOICE-01).
/// Immutable once generated — errors require a credit note.
/// Sequential invoice number, QR code, seller and buyer VAT registration.
/// </summary>
public class CorporateInvoice : BaseEntity
{
    public Guid CorporateAccountId { get; set; }

    /// <summary>Sequential ZATCA invoice number.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public decimal SubtotalAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }

    public string SellerVatRegistration { get; set; } = string.Empty;
    public string BuyerVatRegistration { get; set; } = string.Empty;

    /// <summary>ZATCA Phase 2 QR code data (Base64).</summary>
    public string? QrCodeData { get; set; }

    public bool IsPaid { get; set; } = false;
    public DateTime? PaidAt { get; set; }

    public CorporateAccount CorporateAccount { get; set; } = null!;
}

/// <summary>
/// Finance parameter configuration — all thresholds admin-configurable and versioned.
/// No financial parameter may be hardcoded in the application layer (SRS-FIN-001 §AR-7).
/// </summary>
public class FinanceParameter : BaseEntity
{
    public string ParameterKey { get; set; } = string.Empty;
    public string ParameterValue { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>City or country scope. Null = global default.</summary>
    public Guid? CityId { get; set; }

    public string? ServiceType { get; set; } // ride | food | grocery | carpool

    public decimal? PreviousValue { get; set; }
    public Guid ChangedByActorId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
}
