using BjeekFinance.Domain.Enums;

namespace BjeekFinance.Domain.Entities;

/// <summary>
/// Central wallet entity. Every Driver and Delivery wallet MUST maintain
/// balance_pending and balance_available as separate sub-balances (SRS-FIN-001 §2).
/// User wallets maintain three credit sub-buckets (§5).
/// </summary>
public class Wallet : BaseEntity
{
    public ActorType ActorType { get; set; }
    public Guid ActorId { get; set; }

    /// <summary>ISO 4217 currency code. Default: SAR.</summary>
    public string Currency { get; set; } = "SAR";

    // ── Driver / Delivery balance split (ARCHITECTURE CRITICAL) ──────────────
    /// <summary>
    /// Cleared earnings eligible for Instant Pay or standard payout.
    /// Formula: Settled Digital Earnings − Outstanding Cash Commission Receivables − Active Holds.
    /// </summary>
    public decimal BalanceAvailable { get; set; } = 0;

    /// <summary>
    /// Earnings from trips completed within the last 15 minutes (digital)
    /// or 24 hours (disputed/flagged). NOT available for cashout.
    /// </summary>
    public decimal BalancePending { get; set; } = 0;

    /// <summary>Admin-locked or dunning-held amount.</summary>
    public decimal BalanceHold { get; set; } = 0;

    /// <summary>
    /// Outstanding commission owed on cash trips.
    /// Reduces AVAILABLE immediately upon cash trip completion.
    /// </summary>
    public decimal CashReceivable { get; set; } = 0;

    // ── User wallet credit sub-buckets (§5 — required from day one) ──────────
    /// <summary>Platform-issued refund credit. No expiry. First consumed.</summary>
    public decimal BalanceRefundCredit { get; set; } = 0;

    /// <summary>
    /// Promo/referral/loyalty credit. 30-day expiry (configurable).
    /// Does NOT apply to Premium rides or corporate-billed trips.
    /// </summary>
    public decimal BalancePromoCredit { get; set; } = 0;
    public DateTime? PromoCreditExpiresAt { get; set; }

    /// <summary>
    /// Goodwill / support / compensation credit. 90-day expiry.
    /// Monthly per-customer cap (default SAR 100/month).
    /// </summary>
    public decimal BalanceCourtesyCredit { get; set; } = 0;
    public DateTime? CourtesyCreditExpiresAt { get; set; }

    // ── Loyalty ───────────────────────────────────────────────────────────────
    public int LoyaltyPoints { get; set; } = 0;

    // ── KYC & payout linkage ──────────────────────────────────────────────────
    public KycStatus KycStatus { get; set; } = KycStatus.Unverified;
    public Guid? PayoutAccountId { get; set; }

    // ── Instant Pay configuration ─────────────────────────────────────────────
    public InstantPayTier InstantPayTier { get; set; } = InstantPayTier.TierA;

    /// <summary>Admin-controlled; revocable at any time independent of tier.</summary>
    public bool InstantPayEnabled { get; set; } = true;

    /// <summary>Daily manual cashout counter — resets at local city midnight.</summary>
    public int InstantPayDailyCount { get; set; } = 0;
    public DateTime? InstantPayDailyCountResetAt { get; set; }

    /// <summary>Auto-cashout threshold configured by driver (TIER_B min SAR 100, TIER_C min SAR 50).</summary>
    public decimal? AutoCashoutThreshold { get; set; }

    // ── Dunning ───────────────────────────────────────────────────────────────
    public bool IsInDunning { get; set; } = false;
    public DateTime? DunningStartedAt { get; set; }
    public DunningBucket? DunningBucket { get; set; }

    // ── Fraud score ───────────────────────────────────────────────────────────
    public int FraudScore { get; set; } = 0;
    public DateTime? LastFraudScoreDecayAt { get; set; }

    // ── City context ──────────────────────────────────────────────────────────
    public Guid? CityId { get; set; }

    // ── Navigation properties ─────────────────────────────────────────────────
    public PayoutAccount? PayoutAccount { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<PayoutRequest> PayoutRequests { get; set; } = new List<PayoutRequest>();
    public ICollection<InstantPayCashout> InstantPayCashouts { get; set; } = new List<InstantPayCashout>();

    // ── Computed helpers ──────────────────────────────────────────────────────
    /// <summary>
    /// Total user wallet balance respecting consumption order:
    /// RefundCredit → PromoCredit → CourtesyCredit → BalanceAvailable.
    /// </summary>
    public decimal TotalUserBalance =>
        BalanceRefundCredit + BalancePromoCredit + BalanceCourtesyCredit + BalanceAvailable;
}
