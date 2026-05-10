using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Domain.Entities;

/// <summary>
/// UC-FIN-CASH-01: Cash ride/order settlement record.
/// Tracks expected vs reported cash per driver per settlement period,
/// variance computation, and auto-adjustment vs manual review routing.
/// </summary>
public class CashSettlement : BaseEntity
{
    public Guid DriverId { get; set; }
    public Guid WalletId { get; set; }

    /// <summary>System-computed expected cash total from completed cash trips.</summary>
    public decimal ExpectedCashTotal { get; set; }

    /// <summary>Driver-reported cash total at settlement time.</summary>
    public decimal ReportedCashTotal { get; set; }

    /// <summary>ReportedCashTotal − ExpectedCashTotal (negative = shortfall).</summary>
    public decimal VarianceAmount { get; set; }

    public decimal CommissionReceivableAmount { get; set; }

    public CashSettlementStatus Status { get; set; } = CashSettlementStatus.Pending;

    /// <summary>|Variance| > SAR 3 → true, requires Finance Admin review.</summary>
    public bool VarianceFlag { get; set; }

    /// <summary>JSON array of trip IDs included in this settlement.</summary>
    public string? TripIdsJson { get; set; }

    public string? Notes { get; set; }
    public Guid? ReviewedByActorId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? AuditLogEntryId { get; set; }
    public DateTime? CompletedAt { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────────
    public Wallet Wallet { get; set; } = null!;
}
