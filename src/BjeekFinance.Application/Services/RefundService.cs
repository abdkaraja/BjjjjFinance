using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using BjeekFinance.Domain.Exceptions;
using System.Text.Json;

namespace BjeekFinance.Application.Services;

/// <summary>
/// UC-FIN-REFUND-01: Full refund — reverses entire payment.
/// UC-FIN-REFUND-02: Partial refund — reverses portion for specific items.
/// Card → gateway reversal; Wallet → BalanceRefundCredit instant credit.
/// All wallet writes atomic via Saga pattern.
/// </summary>
public class RefundService : IRefundService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public RefundService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<RefundDto> InitiateRefundAsync(InitiateRefundRequest req, CancellationToken ct = default)
    {
        var isPartial = req.RefundType == "PARTIAL";
        if (!isPartial && req.RefundType != "FULL")
            throw new ArgumentException($"RefundType must be FULL or PARTIAL. Got: {req.RefundType}.");

        // ── 1. Load original transaction ──────────────────────────────────────
        var originalTxn = await _uow.Transactions.GetByIdAsync(req.OriginalTransactionId, ct)
            ?? throw new KeyNotFoundException($"Transaction {req.OriginalTransactionId} not found.");

        if (originalTxn.IsReversed)
            throw new InvalidOperationException("Transaction has already been fully refunded.");

        // ── 2. Validate refund window (admin-configurable per service type) ──
        var serviceType = originalTxn.RideId.HasValue ? "ride" : "food_delivery";
        var refundWindowDays = await _uow.FinanceParameters.GetIntAsync(
            $"refund_window_{serviceType}_days", serviceType == "ride" ? 7 : 1, null, ct);
        var txnAge = DateTime.UtcNow - originalTxn.CreatedAt;
        if (txnAge.TotalDays > refundWindowDays)
            throw new RefundWindowExpiredException(serviceType, refundWindowDays);

        // ── 3. Calculate refund amount ────────────────────────────────────────
        var refundAmount = isPartial
            ? req.PartialAmount ?? throw new ArgumentException("PartialAmount is required for PARTIAL refund.")
            : originalTxn.GrossAmount;

        if (refundAmount <= 0)
            throw new ArgumentException("Refund amount must be positive.");
        if (refundAmount > originalTxn.GrossAmount)
            throw new ArgumentException($"Refund amount ({refundAmount} SAR) exceeds transaction gross ({originalTxn.GrossAmount} SAR).");

        // ── 4. Cumulative cap check (partial refunds only) ──────────────────
        if (isPartial)
        {
            var totalRefunded = await _uow.Refunds.GetTotalRefundedAmountAsync(req.OriginalTransactionId, ct);
            if (totalRefunded + refundAmount > originalTxn.GrossAmount)
                throw new InvalidOperationException(
                    $"Cumulative refunds ({totalRefunded} SAR + {refundAmount} SAR) would exceed transaction gross ({originalTxn.GrossAmount} SAR).");
        }

        // ── 5. Proportional commission reversal (UC-FIN-REFUND-02 BR2) ──────
        // Commission_Reversed = (RefundAmount / GrossAmount) × TotalCommission
        var commissionReversal = isPartial
            ? Math.Round(refundAmount / originalTxn.GrossAmount * originalTxn.CommissionAmount, 2)
            : originalTxn.CommissionAmount;

        var vatReversal = isPartial
            ? Math.Round(refundAmount / originalTxn.GrossAmount * originalTxn.VatAmount, 2)
            : originalTxn.VatAmount;

        // Driver net: the portion of NetAmount attributable to this refund
        var netDebit = isPartial
            ? Math.Round(refundAmount / originalTxn.GrossAmount * originalTxn.NetAmount, 2)
            : originalTxn.NetAmount;

        // ── 6. Determine destination method ──────────────────────────────────
        var destinationMethod = originalTxn.PaymentMethod switch
        {
            PaymentMethod.Card or PaymentMethod.PartialWalletCard => PayoutDestinationType.DebitCard,
            PaymentMethod.Wallet => PayoutDestinationType.SaudiIban,
            _ => PayoutDestinationType.SaudiIban
        };

        // ── 7. Load wallets ──────────────────────────────────────────────────
        var driverWallet = await _uow.Wallets.GetByIdWithLockAsync(originalTxn.WalletId, ct)
            ?? throw new KeyNotFoundException($"Wallet {originalTxn.WalletId} not found.");

        Wallet? userWallet = null;
        if (originalTxn.PaymentMethod == PaymentMethod.Wallet && req.UserActorId.HasValue)
        {
            userWallet = await _uow.Wallets.GetByActorAsync(req.UserActorId.Value, ActorType.User, ct);
        }

        var platformWallet = await _uow.Wallets.GetByActorAsync(Guid.Empty, ActorType.Platform, ct);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // ── 8. Credit user via original payment method ──────────────────
            if (originalTxn.PaymentMethod == PaymentMethod.Wallet && userWallet != null)
            {
                userWallet.BalanceRefundCredit += refundAmount;
                userWallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(userWallet);
            }
            else
            {
                // Card / PartialWalletCard → gateway reversal (T+3 bank settlement)
                // TODO: Initiate card gateway reversal via payment processor
            }

            // ── 9. Debit driver/merchant wallet (proportional net) ──────────
            if (driverWallet.BalanceAvailable >= netDebit)
            {
                driverWallet.BalanceAvailable -= netDebit;
            }
            else
            {
                var shortfall = netDebit - driverWallet.BalanceAvailable;
                driverWallet.BalanceAvailable = 0;
                driverWallet.CashReceivable += shortfall;

                if (!driverWallet.IsInDunning)
                {
                    driverWallet.IsInDunning = true;
                    driverWallet.DunningStartedAt = DateTime.UtcNow;
                    driverWallet.DunningBucket = Domain.Enums.DunningBucket.Notify;
                }
            }
            driverWallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(driverWallet);

            // ── 10. Platform wallet — commission reversal ───────────────────
            if (platformWallet != null)
            {
                platformWallet.BalanceAvailable -= commissionReversal;
                platformWallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(platformWallet);
            }

            // ── 11. Mark original transaction: full refunds set IsReversed,
            //      partial refunds only flag when cumulative reaches gross
            if (!isPartial)
            {
                originalTxn.IsReversed = true;
                originalTxn.UpdatedAt = DateTime.UtcNow;
                _uow.Transactions.Update(originalTxn);
            }
            else
            {
                // Check if cumulative refunds now equal full amount
                var cumulativeAfter = await _uow.Refunds.GetTotalRefundedAmountAsync(req.OriginalTransactionId, ct)
                    + refundAmount;
                if (Math.Abs(cumulativeAfter - originalTxn.GrossAmount) < 0.01m)
                {
                    originalTxn.IsReversed = true;
                    originalTxn.UpdatedAt = DateTime.UtcNow;
                    _uow.Transactions.Update(originalTxn);
                }
            }

            // ── 12. Create refund record ────────────────────────────────────
            var refund = new Refund
            {
                OriginalTransactionId = req.OriginalTransactionId,
                RefundType = req.RefundType,
                Amount = refundAmount,
                PartialAmount = isPartial ? refundAmount : null,
                ItemsRefunded = isPartial ? req.ItemsRefunded : null,
                CommissionReversalAmount = commissionReversal,
                VatReversalAmount = vatReversal,
                ReasonCode = req.ReasonCode,
                DestinationMethod = destinationMethod,
                Status = RefundStatus.Completed,
                ActorId = req.ActorId,
                ActorRole = req.ActorRole,
                UserWalletId = userWallet?.Id,
                WalletId = driverWallet.Id,
                PlatformWalletId = platformWallet?.Id ?? Guid.Empty,
                CompletedAt = DateTime.UtcNow
            };
            await _uow.Refunds.AddAsync(refund, ct);

            // ── 13. Immutable audit log ─────────────────────────────────────
            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Refund, isPartial ? "PARTIAL_REFUND_INITIATED" : "REFUND_INITIATED",
                req.ActorId, req.ActorRole,
                refund.Id, "REFUND",
                JsonSerializer.Serialize(new
                {
                    originalTxnId = req.OriginalTransactionId,
                    refundType = req.RefundType,
                    refundAmount,
                    commissionReversal,
                    vatReversal,
                    netDebit,
                    itemsRefunded = req.ItemsRefunded
                }),
                JsonSerializer.Serialize(new
                {
                    refundId = refund.Id,
                    destinationMethod,
                    status = refund.Status,
                    driverAvailableAfter = driverWallet.BalanceAvailable
                }),
                driverWallet.CityId, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return MapToDto(refund);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<RefundDto> GetRefundAsync(Guid refundId, CancellationToken ct = default)
    {
        var refund = await _uow.Refunds.GetByIdAsync(refundId, ct)
            ?? throw new KeyNotFoundException($"Refund {refundId} not found.");
        return MapToDto(refund);
    }

    public async Task<RefundDto?> GetRefundByTransactionAsync(Guid transactionId, CancellationToken ct = default)
    {
        var refunds = await _uow.Refunds.GetByOriginalTransactionAsync(transactionId, ct);
        return refunds.FirstOrDefault() is Refund r ? MapToDto(r) : null;
    }

    public async Task<IEnumerable<RefundDto>> GetRefundsByActorAsync(Guid actorId, CancellationToken ct = default)
    {
        var refunds = await _uow.Refunds.GetByActorAsync(actorId, ct);
        return refunds.Select(MapToDto);
    }

    private static RefundDto MapToDto(Refund r) => new(
        r.Id, r.OriginalTransactionId, r.RefundType, r.Amount,
        r.PartialAmount, r.ItemsRefunded,
        r.CommissionReversalAmount, r.VatReversalAmount, r.ReasonCode,
        r.DestinationMethod, r.Status, r.ActorId, r.ActorRole,
        r.PspReversalReference, r.CreatedAt, r.CompletedAt);
}
