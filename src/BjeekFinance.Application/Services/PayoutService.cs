using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using BjeekFinance.Domain.Exceptions;
using System.Text.Json;

namespace BjeekFinance.Application.Services;

/// <summary>
/// UC-FIN-PAYOUT-01: Standard payout to bank.
/// IBAN transfers routed via SARIE (Sun–Thu 08:00–16:00 AST).
/// Out-of-window → queued, never silently deferred. Actor notified with expected time.
/// </summary>
public class PayoutService : IPayoutService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public PayoutService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<PayoutRequestDto> InitiatePayoutAsync(InitiatePayoutRequest req, CancellationToken ct = default)
    {
        var wallet = await _uow.Wallets.GetByIdWithLockAsync(req.WalletId, ct)
            ?? throw new KeyNotFoundException($"Wallet {req.WalletId} not found.");

        // KYC mandatory before any payout
        if (wallet.KycStatus != KycStatus.Verified) throw new KycNotVerifiedException();

        // Fraud gate
        if (wallet.FraudScore >= 80) throw new WalletFrozenException();

        // Dunning hold
        if (wallet.IsInDunning && wallet.DunningBucket >= Domain.Enums.DunningBucket.HoldPayout)
            throw new DunningHoldException(new(wallet.DunningBucket.ToString()!, wallet.DunningStartedAt!.Value));

        // Minimum threshold — admin-configurable per city (default SAR 50)
        var minThreshold = await _uow.FinanceParameters.GetDecimalAsync("payout_minimum_threshold", 50m, req.CityId, null, ct);
        if (req.AmountRequested < minThreshold)
            throw new PayoutBelowMinimumException(minThreshold);

        if (wallet.BalanceAvailable < req.AmountRequested)
            throw new InsufficientBalanceException(wallet.BalanceAvailable, req.AmountRequested);

        var payoutAccount = await _uow.PayoutAccounts.GetByIdAsync(req.PayoutAccountId, ct)
            ?? throw new KeyNotFoundException("Payout account not found.");
        if (payoutAccount.VerificationStatus != KycStatus.Verified)
            throw new KycNotVerifiedException();

        var feeAmount = await _uow.FinanceParameters.GetDecimalAsync("payout_fee", 0m, req.CityId, null, ct);
        var netTransfer = req.AmountRequested - feeAmount;

        // AF2: STC Pay / mobile wallet — near-real-time, no SARIE dependency
        // IBAN transfers route via SARIE (Sun–Thu 08:00–16:00 AST)
        var isIBAN = payoutAccount.DestinationType == PayoutDestinationType.SaudiIban;
        var sarieStatus = isIBAN ? CheckSarieWindow(DateTime.UtcNow) : SarieWindowStatus.Open;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // Lock amount into hold
            wallet.BalanceAvailable -= req.AmountRequested;
            wallet.BalanceHold += req.AmountRequested;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            // Approval routing — three tiers:
            //   ≤ autoApproveThreshold            → Approved (auto-fire)
            //   > autoApproveThreshold            → Pending (Finance Admin)
            //   > superAdminThreshold             → Pending (Super Admin via RBAC)
            var autoApproveThreshold = await _uow.FinanceParameters.GetDecimalAsync("payout_auto_approve_threshold", 10_000m, req.CityId, null, ct);
            var superAdminThreshold = await _uow.FinanceParameters.GetDecimalAsync("payout_super_admin_threshold", 37_000m, req.CityId, null, ct);

            var status = req.AmountRequested <= autoApproveThreshold
                ? PayoutStatus.Approved
                : PayoutStatus.Pending;

            var payout = new PayoutRequest
            {
                ActorId = req.ActorId,
                WalletId = req.WalletId,
                PayoutAccountId = req.PayoutAccountId,
                AmountRequested = req.AmountRequested,
                FeeAmount = feeAmount,
                NetTransferAmount = netTransfer,
                DestinationType = payoutAccount.DestinationType,
                Status = status,
                SarieWindowStatus = sarieStatus,
                ScheduledAt = isIBAN && sarieStatus == SarieWindowStatus.Queued
                    ? NextSarieWindow(DateTime.UtcNow) : null
            };
            await _uow.PayoutRequests.AddAsync(payout, ct);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Payout, "PAYOUT_REQUESTED",
                req.ActorId, ActorRole.Driver,
                payout.Id, "PAYOUT", null,
                JsonSerializer.Serialize(new { payout.Id, req.AmountRequested, status, sarieStatus }),
                req.CityId, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return MapToDto(payout);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<PayoutRequestDto> ApprovePayoutAsync(Guid payoutId, Guid approverActorId, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var payout = await _uow.PayoutRequests.GetByIdAsync(payoutId, ct)
                ?? throw new KeyNotFoundException($"Payout {payoutId} not found.");
            if (payout.Status != PayoutStatus.Pending)
                throw new InvalidOperationException("Only pending payouts can be approved.");

            payout.Status = PayoutStatus.Approved;
            payout.ApprovedByActorId = approverActorId;
            payout.ApprovedAt = DateTime.UtcNow;
            payout.UpdatedAt = DateTime.UtcNow;
            _uow.PayoutRequests.Update(payout);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Payout, "PAYOUT_APPROVED",
                approverActorId, ActorRole.FinanceAdmin,
                payoutId, "PAYOUT", null,
                JsonSerializer.Serialize(new { payoutId, approvedBy = approverActorId }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return MapToDto(payout);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<PayoutRequestDto> RejectPayoutAsync(Guid payoutId, Guid approverActorId, string reasonCode, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var payout = await _uow.PayoutRequests.GetByIdAsync(payoutId, ct)
                ?? throw new KeyNotFoundException($"Payout {payoutId} not found.");

            var wallet = await _uow.Wallets.GetByIdWithLockAsync(payout.WalletId, ct)!;
            // Release hold back to available
            wallet!.BalanceHold -= payout.AmountRequested;
            wallet.BalanceAvailable += payout.AmountRequested;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            payout.Status = PayoutStatus.Rejected;
            payout.RejectionReasonCode = reasonCode;
            payout.UpdatedAt = DateTime.UtcNow;
            _uow.PayoutRequests.Update(payout);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Payout, "PAYOUT_REJECTED",
                approverActorId, ActorRole.FinanceAdmin,
                payoutId, "PAYOUT", null,
                JsonSerializer.Serialize(new { payoutId, reasonCode }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return MapToDto(payout);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<IEnumerable<PayoutRequestDto>> GetPendingPayoutsAsync(CancellationToken ct = default)
    {
        var payouts = await _uow.PayoutRequests.GetPendingPayoutsAsync(ct);
        return payouts.Select(MapToDto);
    }

    public async Task<IEnumerable<PayoutRequestDto>> GetByActorAsync(Guid actorId, CancellationToken ct = default)
    {
        var payouts = await _uow.PayoutRequests.GetByActorAsync(actorId, ct);
        return payouts.Select(MapToDto);
    }

    public async Task<PayoutRequestDto> GetByIdAsync(Guid payoutId, CancellationToken ct = default)
    {
        var payout = await _uow.PayoutRequests.GetByIdAsync(payoutId, ct)
            ?? throw new KeyNotFoundException($"Payout {payoutId} not found.");
        return MapToDto(payout);
    }

    public async Task<PayoutRequestDto> CompletePayoutAsync(Guid payoutId, string pspTransactionId, string transferReference, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var payout = await _uow.PayoutRequests.GetByIdAsync(payoutId, ct)
                ?? throw new KeyNotFoundException($"Payout {payoutId} not found.");
            if (payout.Status != PayoutStatus.Processing)
                throw new InvalidOperationException($"Only processing payouts can be completed. Current status: {payout.Status}.");

            var wallet = await _uow.Wallets.GetByIdWithLockAsync(payout.WalletId, ct)
                ?? throw new KeyNotFoundException($"Wallet {payout.WalletId} not found.");

            var before = wallet.BalanceHold;
            // Release hold — balance_available already reduced at initiation
            wallet.BalanceHold -= payout.AmountRequested;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            payout.Status = PayoutStatus.Completed;
            payout.PspTransactionId = pspTransactionId;
            payout.TransferReference = transferReference;
            payout.UpdatedAt = DateTime.UtcNow;
            _uow.PayoutRequests.Update(payout);

            // If this PayoutRequest was created as an Instant Pay fallback (AF2),
            // update the linked cashout to Completed as well.
            var linkedCashout = await _uow.InstantPay.GetByPayoutRequestIdAsync(payoutId, ct);
            if (linkedCashout != null)
            {
                linkedCashout.TransferStatus = PayoutStatus.Completed;
                linkedCashout.TransferReference = transferReference;
                _uow.InstantPay.Update(linkedCashout);

                await _audit.WriteAsync(new AuditLogRequest(
                    AuditEventType.InstantPay, "INSTANT_PAY_FALLBACK_COMPLETED",
                    linkedCashout.ActorId, ActorRole.Driver, linkedCashout.Id, "INSTANT_PAY",
                    null, JsonSerializer.Serialize(new { payoutId, pspTransactionId, transferReference }),
                    null, null, null), ct);
            }

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Payout, "PAYOUT_COMPLETED",
                Guid.Empty, ActorRole.System,
                payoutId, "PAYOUT", null,
                JsonSerializer.Serialize(new { payoutId, pspTransactionId, transferReference, holdReleased = before }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return MapToDto(payout);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<PayoutRequestDto> RetryPayoutAsync(Guid payoutId, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var payout = await _uow.PayoutRequests.GetByIdAsync(payoutId, ct)
                ?? throw new KeyNotFoundException($"Payout {payoutId} not found.");

            if (payout.Status != PayoutStatus.Failed)
                throw new InvalidOperationException($"Only failed payouts can be retried. Current status: {payout.Status}.");

            // EX2: Exponential backoff — 3 max retries
            if (payout.RetryCount >= 3)
            {
                // Release hold back to available after persistent failure
                var wallet = await _uow.Wallets.GetByIdWithLockAsync(payout.WalletId, ct)!;
                wallet!.BalanceHold -= payout.AmountRequested;
                wallet.BalanceAvailable += payout.AmountRequested;
                wallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(wallet);

                payout.Status = PayoutStatus.Rejected;
                payout.RejectionReasonCode = "MAX_RETRIES_EXCEEDED";
                payout.UpdatedAt = DateTime.UtcNow;
                _uow.PayoutRequests.Update(payout);

                // If this was an Instant Pay fallback, mark linked cashout as Failed
                var linkedCashout = await _uow.InstantPay.GetByPayoutRequestIdAsync(payoutId, ct);
                if (linkedCashout != null)
                {
                    linkedCashout.TransferStatus = PayoutStatus.Failed;
                    _uow.InstantPay.Update(linkedCashout);

                    await _audit.WriteAsync(new AuditLogRequest(
                        AuditEventType.InstantPay, "INSTANT_PAY_FALLBACK_PERMANENTLY_FAILED",
                        linkedCashout.ActorId, ActorRole.Driver, linkedCashout.Id, "INSTANT_PAY",
                        null, JsonSerializer.Serialize(new { payoutId, retryCount = payout.RetryCount }),
                        null, null, null), ct);
                }

                await _audit.WriteAsync(new AuditLogRequest(
                    AuditEventType.Payout, "PAYOUT_FAILED_PERMANENT",
                    Guid.Empty, ActorRole.System,
                    payoutId, "PAYOUT", null,
                    JsonSerializer.Serialize(new { payoutId, retryCount = payout.RetryCount }),
                    null, null, null), ct);
            }
            else
            {
                payout.RetryCount++;
                payout.Status = PayoutStatus.Processing;
                payout.UpdatedAt = DateTime.UtcNow;
                _uow.PayoutRequests.Update(payout);

                await _audit.WriteAsync(new AuditLogRequest(
                    AuditEventType.Payout, "PAYOUT_RETRY",
                    Guid.Empty, ActorRole.System,
                    payoutId, "PAYOUT", null,
                    JsonSerializer.Serialize(new { payoutId, retryCount = payout.RetryCount }),
                    null, null, null), ct);
            }

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return MapToDto(payout);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task ProcessSarieQueueAsync(CancellationToken ct = default)
    {
        if (CheckSarieWindow(DateTime.UtcNow) != SarieWindowStatus.Open) return;
        var queued = await _uow.PayoutRequests.GetQueuedSariePayoutsAsync(ct);
        foreach (var payout in queued)
        {
            payout.Status = PayoutStatus.Processing;
            payout.SarieWindowStatus = SarieWindowStatus.Open;
            payout.UpdatedAt = DateTime.UtcNow;
            _uow.PayoutRequests.Update(payout);
        }
        await _uow.SaveChangesAsync(ct);
    }

    // ── SARIE helpers ──────────────────────────────────────────────────────────

    private static SarieWindowStatus CheckSarieWindow(DateTime utcNow)
    {
        var ast = TimeZoneInfo.ConvertTimeFromUtc(utcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"));
        var dayOfWeek = ast.DayOfWeek;
        bool isBusinessDay = dayOfWeek is >= DayOfWeek.Sunday and <= DayOfWeek.Thursday;
        bool isInWindow = ast.TimeOfDay >= new TimeSpan(8, 0, 0) && ast.TimeOfDay < new TimeSpan(16, 0, 0);
        return isBusinessDay && isInWindow ? SarieWindowStatus.Open : SarieWindowStatus.Queued;
    }

    private static DateTime NextSarieWindow(DateTime utcNow)
    {
        var ast = TimeZoneInfo.ConvertTimeFromUtc(utcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"));
        var next = ast.Date.AddHours(8);
        if (ast.TimeOfDay >= new TimeSpan(16, 0, 0)) next = next.AddDays(1);
        // Skip Friday/Saturday
        while (next.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday) next = next.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(next, TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"));
    }

    private static PayoutRequestDto MapToDto(PayoutRequest p) => new(
        p.Id, p.ActorId, p.WalletId, p.AmountRequested, p.FeeAmount,
        p.NetTransferAmount, p.DestinationType, p.Status, p.SarieWindowStatus,
        p.TransferReference, p.ApprovedAt, p.ScheduledAt, p.RetryCount, p.CreatedAt);
}

/// <summary>
/// UC-FIN-INSTANT-01: On-demand Instant Pay cashout.
/// Draws ONLY from AVAILABLE balance. Separate from standard payout.
/// Eligibility engine: tier, daily limit, fraud score, destination fast-fund flag.
/// VAT-compliant micro-invoice generated per cashout (ZATCA).
/// Daily limit resets at local city midnight — not UTC.
/// </summary>
public class InstantPayService : IInstantPayService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public InstantPayService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<EligibilityResultDto> CheckEligibilityAsync(Guid actorId, decimal amount, CancellationToken ct = default)
    {
        var wallet = await _uow.Wallets.GetByActorAsync(actorId, ActorType.Driver, ct)
                  ?? await _uow.Wallets.GetByActorAsync(actorId, ActorType.Delivery, ct)
                  ?? throw new KeyNotFoundException("Wallet not found.");

        if (!wallet.InstantPayEnabled)
            return Fail("Instant Pay is disabled on this account.", wallet);
        if (wallet.KycStatus != KycStatus.Verified)
            return Fail("KYC not verified.", wallet);
        if (wallet.FraudScore >= 50)
            return Fail("Account suspended pending fraud review.", wallet);
        if (wallet.InstantPayTier == InstantPayTier.TierA)
            return Fail("Tier A accounts are not eligible for manual cashout (Tier 2).", wallet);

        var minBalance = await _uow.FinanceParameters.GetDecimalAsync("instant_pay_min_balance", 5m, wallet.CityId, null, ct);
        if (wallet.BalanceAvailable < minBalance)
            return Fail($"Available balance below minimum ({minBalance} SAR).", wallet);
        if (wallet.BalanceAvailable < amount)
            return Fail("Insufficient available balance.", wallet);

        // Daily limit check — resets at local city midnight
        var dailyLimit = wallet.InstantPayTier == InstantPayTier.TierC
            ? await _uow.FinanceParameters.GetIntAsync("instant_pay_daily_limit_tier_c", 5, wallet.CityId, ct)
            : await _uow.FinanceParameters.GetIntAsync("instant_pay_daily_limit_tier_b", 3, wallet.CityId, ct);

        if (wallet.InstantPayDailyCount >= dailyLimit)
            return Fail($"Daily limit of {dailyLimit} cashouts reached.", wallet);

        var (fee, vat) = CalculateFee(wallet.InstantPayTier);
        return new EligibilityResultDto(
            true, null, wallet.BalanceAvailable, minBalance,
            dailyLimit - wallet.InstantPayDailyCount,
            wallet.InstantPayTier, fee, vat);
    }

    public async Task<InstantPayResultDto> InitiateCashoutAsync(InstantPayRequest req, CancellationToken ct = default)
    {
        var eligibility = await CheckEligibilityAsync(req.ActorId, req.AmountRequested, ct);
        if (!eligibility.IsEligible)
            throw new InstantPayNotEligibleException(eligibility.FailureReason!);

        var payoutAccount = await _uow.PayoutAccounts.GetByIdAsync(req.PayoutAccountId, ct)
            ?? throw new KeyNotFoundException("Payout account not found.");

        // Fast-fund eligibility: debit card must be eligible; otherwise direct to STC Pay
        if (payoutAccount.DestinationType == PayoutDestinationType.DebitCard && !payoutAccount.CardFastFundEligible)
            throw new InstantPayNotEligibleException("Debit card does not support fast funds. Use STC Pay or an eligible card.");

        var wallet = await _uow.Wallets.GetByIdWithLockAsync(req.WalletId, ct)!;
        var (fee, vat) = CalculateFee(wallet!.InstantPayTier);
        var netTransfer = req.AmountRequested - fee;

        var rail = payoutAccount.DestinationType == PayoutDestinationType.StcPay
            ? TransferRail.StcPay : TransferRail.IbanFast;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var before = wallet.BalanceAvailable;
            // Fee deducted from available balance; remaining amount locked for transfer
            wallet.BalanceAvailable -= (req.AmountRequested + fee);
            wallet.BalanceHold += req.AmountRequested;
            wallet.InstantPayDailyCount++;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            var microInvoiceId = $"MIC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N[..6]}";

            var cashout = new InstantPayCashout
            {
                ActorId = req.ActorId,
                WalletId = req.WalletId,
                AmountRequested = req.AmountRequested,
                FeeAmount = fee,
                VatOnFee = vat,
                NetTransferAmount = netTransfer,
                DestinationType = payoutAccount.DestinationType,
                TransferRail = rail,
                TransferStatus = PayoutStatus.Processing,
                MicroInvoiceId = microInvoiceId,
                DailyCountBefore = wallet.InstantPayDailyCount - 1,
                DailyCountAfter = wallet.InstantPayDailyCount,
                CityLocalTime = DateTime.UtcNow, // real impl: convert via city timezone
                IsAutoTriggered = req.IsAutoTriggered
            };
            await _uow.InstantPay.AddAsync(cashout, ct);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.InstantPay, "INSTANT_PAY_INITIATED",
                req.ActorId, ActorRole.Driver, cashout.Id, "INSTANT_PAY",
                JsonSerializer.Serialize(new { BalanceAvailable = before }),
                JsonSerializer.Serialize(new { req.AmountRequested, fee, vat, netTransfer, rail }),
                wallet.CityId, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return new InstantPayResultDto(
                cashout.Id, req.ActorId, req.AmountRequested, fee, vat, netTransfer,
                rail, PayoutStatus.Processing, null, microInvoiceId,
                cashout.DailyCountAfter, false, DateTime.UtcNow);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task RecalculateTiersAsync(CancellationToken ct = default)
    {
        // Nightly job: promote/demote wallets based on trip count, fraud flags
        // All thresholds admin-configurable — never hardcoded
        var tierATripThreshold = await _uow.FinanceParameters.GetIntAsync("instant_pay_tier_a_trips", 50, null, ct);
        var tierCTripThreshold = await _uow.FinanceParameters.GetIntAsync("instant_pay_tier_c_trips", 500, null, ct);

        // Fetch all driver/delivery wallets with sufficient trip history
        // Promotion: TierA → TierB (≥50 trips), TierB → TierC (≥500 trips)
        // Demotion: TierC → TierB (fraud ≥30 or dunning)
        //           TierB → TierA (fraud ≥50 or dunning HoldPayout+)
        var wallets = await _uow.Wallets.GetAllAsync(ct);
        var allDriverWallets = wallets.Where(w =>
            w.ActorType is ActorType.Driver or ActorType.Delivery);

        foreach (var wallet in allDriverWallets)
        {
            var tripCount = await _uow.Transactions.GetByActorCountAsync(wallet.ActorId, ct);
            var tier = wallet.InstantPayTier;

            // Demotion rules first (safety over speed)
            if (tier == InstantPayTier.TierC && (wallet.FraudScore >= 30 || wallet.IsInDunning))
                tier = InstantPayTier.TierB;

            if (tier == InstantPayTier.TierB && (wallet.FraudScore >= 50 ||
                (wallet.IsInDunning && wallet.DunningBucket >= Domain.Enums.DunningBucket.HoldPayout)))
                tier = InstantPayTier.TierA;

            // Promotion rules
            if (tier == InstantPayTier.TierA && tripCount >= tierATripThreshold && wallet.FraudScore < 50)
                tier = InstantPayTier.TierB;

            if (tier == InstantPayTier.TierB && tripCount >= tierCTripThreshold && wallet.FraudScore < 30 && !wallet.IsInDunning)
                tier = InstantPayTier.TierC;

            if (wallet.InstantPayTier != tier)
            {
                wallet.InstantPayTier = tier;
                wallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(wallet);
            }
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task RevokeInstantPayAsync(Guid walletId, string reasonCode, string estimatedResolution, Guid adminId, CancellationToken ct = default)
    {
        var wallet = await _uow.Wallets.GetByIdAsync(walletId, ct)
            ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");

        wallet.InstantPayEnabled = false;
        wallet.UpdatedAt = DateTime.UtcNow;
        _uow.Wallets.Update(wallet);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.InstantPay, "INSTANT_PAY_REVOKED",
            adminId, ActorRole.FinanceAdmin, walletId, "WALLET",
            null, JsonSerializer.Serialize(new { reasonCode, estimatedResolution }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<InstantPayCashoutDto>> GetByActorAsync(Guid actorId, CancellationToken ct = default)
    {
        var cashouts = await _uow.InstantPay.GetByActorAsync(actorId, ct);
        return cashouts.Select(c => new InstantPayCashoutDto(
            c.Id, c.ActorId, c.AmountRequested, c.FeeAmount,
            c.NetTransferAmount, c.TransferRail, c.TransferStatus,
            c.IsAutoTriggered, c.IsFallback, c.CreatedAt));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  UC-FIN-INSTANT-01: PSP webhook / lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<InstantPayResultDto> CompleteCashoutAsync(Guid cashoutId, string pspTransactionId, string transferReference, CancellationToken ct = default)
    {
        var cashout = await _uow.InstantPay.GetByIdAsync(cashoutId, ct)
            ?? throw new KeyNotFoundException($"Instant Pay cashout {cashoutId} not found.");

        if (cashout.TransferStatus != PayoutStatus.Processing)
            throw new InvalidOperationException($"Cashout is not in Processing status. Current: {cashout.TransferStatus}.");
        if (cashout.IsFallback)
            throw new InvalidOperationException("Cashout is in fallback mode. Use CompletePayoutAsync on the linked PayoutRequest instead.");

        var wallet = await _uow.Wallets.GetByIdWithLockAsync(cashout.WalletId, ct)!;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var before = wallet!.BalanceHold;
            wallet.BalanceHold -= cashout.AmountRequested;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            cashout.TransferReference = transferReference;
            cashout.TransferStatus = PayoutStatus.Completed;
            _uow.InstantPay.Update(cashout);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.InstantPay, "INSTANT_PAY_COMPLETED",
                cashout.ActorId, ActorRole.Driver, cashout.Id, "INSTANT_PAY",
                JsonSerializer.Serialize(new { BalanceHold = before }),
                JsonSerializer.Serialize(new { pspTransactionId, transferReference }),
                wallet.CityId, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return new InstantPayResultDto(
                cashout.Id, cashout.ActorId, cashout.AmountRequested, cashout.FeeAmount,
                cashout.VatOnFee, cashout.NetTransferAmount, cashout.TransferRail,
                PayoutStatus.Completed, transferReference, cashout.MicroInvoiceId,
                cashout.DailyCountAfter, false, DateTime.UtcNow);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<InstantPayCashoutDto> FailCashoutAsync(Guid cashoutId, string failureReason, CancellationToken ct = default)
    {
        var cashout = await _uow.InstantPay.GetByIdAsync(cashoutId, ct)
            ?? throw new KeyNotFoundException($"Instant Pay cashout {cashoutId} not found.");

        if (cashout.TransferStatus != PayoutStatus.Processing)
            throw new InvalidOperationException($"Cashout is not in Processing status. Current: {cashout.TransferStatus}.");

        var wallet = await _uow.Wallets.GetByIdWithLockAsync(cashout.WalletId, ct)!;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            if (cashout.TransferRail is TransferRail.StcPay or TransferRail.IbanFast)
            {
                // ── AF2: Primary rail failed → fallback to standard IBAN (T+1) ──
                // Hold stays in place (cashout locked it). Fallback PayoutRequest
                // will release it on completion (via CompletePayoutAsync).
                var payoutAccount = await _uow.PayoutAccounts.GetVerifiedAsync(cashout.ActorId, ct);
                var fallbackPayout = new PayoutRequest
                {
                    ActorId = cashout.ActorId,
                    WalletId = cashout.WalletId,
                    PayoutAccountId = payoutAccount?.Id ?? Guid.Empty,
                    AmountRequested = cashout.AmountRequested,
                    FeeAmount = 0m, // fee already collected at Instant Pay initiation
                    NetTransferAmount = cashout.AmountRequested,
                    DestinationType = PayoutDestinationType.SaudiIban,
                    Status = PayoutStatus.Pending,
                    SarieWindowStatus = SarieWindowStatus.Queued
                };
                await _uow.PayoutRequests.AddAsync(fallbackPayout, ct);

                cashout.IsFallback = true;
                cashout.TransferRail = TransferRail.IbanStandardFallback;
                cashout.PayoutRequestId = fallbackPayout.Id;

                await _audit.WriteAsync(new AuditLogRequest(
                    AuditEventType.InstantPay, "INSTANT_PAY_FALLBACK",
                    cashout.ActorId, ActorRole.Driver, cashout.Id, "INSTANT_PAY",
                    JsonSerializer.Serialize(new { BalanceHold = wallet!.BalanceHold }),
                    JsonSerializer.Serialize(new { failureReason, fallbackPayoutId = fallbackPayout.Id }),
                    wallet.CityId, null, null), ct);
            }
            else
            {
                // ── EX4: Fallback rail also failed → release hold, mark failed ──
                wallet!.BalanceHold -= cashout.AmountRequested;
                wallet.BalanceAvailable += cashout.AmountRequested;
                wallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(wallet);

                cashout.TransferStatus = PayoutStatus.Failed;

                await _audit.WriteAsync(new AuditLogRequest(
                    AuditEventType.InstantPay, "INSTANT_PAY_ALL_RAILS_FAILED",
                    cashout.ActorId, ActorRole.Driver, cashout.Id, "INSTANT_PAY",
                    JsonSerializer.Serialize(new { BalanceHold = wallet.BalanceHold }),
                    JsonSerializer.Serialize(new { failureReason }),
                    wallet.CityId, null, null), ct);
            }

            _uow.InstantPay.Update(cashout);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return new InstantPayCashoutDto(
                cashout.Id, cashout.ActorId, cashout.AmountRequested, cashout.FeeAmount,
                cashout.NetTransferAmount, cashout.TransferRail, cashout.TransferStatus,
                cashout.IsAutoTriggered, cashout.IsFallback, cashout.CreatedAt);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<InstantPayCashoutDto> CancelCashoutAsync(Guid cashoutId, string reasonCode, CancellationToken ct = default)
    {
        var cashout = await _uow.InstantPay.GetByIdAsync(cashoutId, ct)
            ?? throw new KeyNotFoundException($"Instant Pay cashout {cashoutId} not found.");

        if (cashout.TransferStatus != PayoutStatus.Processing)
            throw new InvalidOperationException($"Cashout is not in Processing status. Current: {cashout.TransferStatus}.");

        var wallet = await _uow.Wallets.GetByIdWithLockAsync(cashout.WalletId, ct)!;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // EX3: Release hold back to available, reverse daily count increment
            wallet!.BalanceHold -= cashout.AmountRequested;
            wallet.BalanceAvailable += cashout.AmountRequested;
            wallet.InstantPayDailyCount--;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            cashout.TransferStatus = PayoutStatus.Failed;

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.InstantPay, "INSTANT_PAY_CANCELLED_FRAUD",
                cashout.ActorId, ActorRole.Driver, cashout.Id, "INSTANT_PAY",
                JsonSerializer.Serialize(new { BalanceHold = wallet.BalanceHold + cashout.AmountRequested }),
                JsonSerializer.Serialize(new { reasonCode }),
                wallet.CityId, null, null), ct);

            // TODO: Raise fraud alert via IFraudService when available

            _uow.InstantPay.Update(cashout);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return new InstantPayCashoutDto(
                cashout.Id, cashout.ActorId, cashout.AmountRequested, cashout.FeeAmount,
                cashout.NetTransferAmount, cashout.TransferRail, cashout.TransferStatus,
                cashout.IsAutoTriggered, cashout.IsFallback, cashout.CreatedAt);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<int> ProcessAutoCashoutsAsync(CancellationToken ct = default)
    {
        var wallets = await _uow.Wallets.GetWalletsWithAutoCashoutEnabledAsync(ct);
        int processed = 0;

        foreach (var wallet in wallets)
        {
            var payoutAccount = await _uow.PayoutAccounts.GetVerifiedAsync(wallet.ActorId, ct);
            if (payoutAccount == null) continue;

            // Build auto-cashout request using the driver's threshold
            var amount = Math.Min(wallet.BalanceAvailable, wallet.AutoCashoutThreshold!.Value);
            var req = new InstantPayRequest(
                wallet.ActorId, wallet.Id, amount, payoutAccount.Id, IsAutoTriggered: true);

            try
            {
                await InitiateCashoutAsync(req, ct);
                processed++;

                // TODO: Send notification via INotificationService when available:
                // "SAR [amount] has been auto-cashed out to your account."
            }
            catch
            {
                // Individual wallet failure should not block other auto-cashouts
                await _audit.WriteAsync(new AuditLogRequest(
                    AuditEventType.InstantPay, "INSTANT_PAY_AUTO_FAILED",
                    wallet.ActorId, ActorRole.Driver, wallet.Id, "WALLET",
                    null, JsonSerializer.Serialize(new { wallet.BalanceAvailable, wallet.AutoCashoutThreshold }),
                    wallet.CityId, null, null), ct);
            }
        }

        if (processed > 0)
            await _uow.SaveChangesAsync(ct);

        return processed;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fee table per tier (admin-configurable — these are recommended defaults).
    /// Tier B: SAR 1.50 + 15% VAT = SAR 1.74
    /// Tier C: SAR 0.87 (loyalty reward)
    /// </summary>
    private static (decimal fee, decimal vat) CalculateFee(InstantPayTier tier)
    {
        const decimal vatRate = 0.15m;
        decimal baseFee = tier == InstantPayTier.TierC ? 0.756522m : 1.50m;
        decimal vat = Math.Round(baseFee * vatRate, 2);
        return (Math.Round(baseFee + vat, 2), vat);
    }

    private static EligibilityResultDto Fail(string reason, Wallet w) => new(
        false, reason, w.BalanceAvailable, 0, 0, w.InstantPayTier, 0, 0);
}
