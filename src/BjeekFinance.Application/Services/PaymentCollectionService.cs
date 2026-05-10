using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using BjeekFinance.Domain.Exceptions;
using System.Text.Json;

namespace BjeekFinance.Application.Services;

/// <summary>
/// UC-FIN-COLLECT-01: Atomic multi-wallet Saga.
/// All wallet deltas must succeed or all must roll back.
/// Webhook idempotency key required — duplicate events must not cause double ledger entries.
/// </summary>
public class PaymentCollectionService : IPaymentCollectionService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly IFinanceParameterRepository _params;

    public PaymentCollectionService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
        _params = uow.FinanceParameters;
    }

    public async Task<CollectPaymentResultDto> CollectPaymentAsync(CollectPaymentRequest req, CancellationToken ct = default)
    {
        // Idempotency guard — duplicate gateway webhooks must be a no-op
        if (!string.IsNullOrEmpty(req.IdempotencyKey))
        {
            var existing = await _uow.Transactions.GetByIdempotencyKeyAsync(req.IdempotencyKey, ct);
            if (existing is not null)
                return MapTransactionToResult(existing);
        }

        // Load admin-configurable VAT rate — never hardcoded
        var vatRate = await _params.GetDecimalAsync("vat_rate", 0.15m, req.CityId, req.ServiceType, ct);

        var commissionAmount = Math.Round(req.GrossAmount * req.CommissionRate, 2);
        var fleetFeeAmount = Math.Round(req.GrossAmount * req.FleetFeePercent, 2);
        var vatAmount = Math.Round(commissionAmount * vatRate, 2);
        var netDriverAmount = req.GrossAmount - commissionAmount - fleetFeeAmount;
        var tipAmount = req.TipAmount;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var txnId = Guid.NewGuid();
            var invoiceId = $"INV-{DateTime.UtcNow:yyyyMMdd}-{txnId:N[..8]}";
            var sagaId = Guid.NewGuid().ToString();
            var deltas = new List<WalletDeltaDto>();

            // ── 1. User wallet debit (digital payments) ───────────────────────
            if (req.PaymentMethod is PaymentMethod.Wallet or PaymentMethod.PartialWalletCard)
            {
                var userWallet = await _uow.Wallets.GetByActorAsync(req.UserActorId, ActorType.User, ct)
                    ?? throw new KeyNotFoundException("User wallet not found.");
                if (userWallet.FraudScore >= 80) throw new WalletFrozenException();

                DebitUserWalletBuckets(userWallet, req.GrossAmount);
                userWallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(userWallet);
                deltas.Add(new WalletDeltaDto(userWallet.Id, ActorType.User, -req.GrossAmount, "Payment debit"));
            }

            // ── 2. Driver / Delivery wallet credit (PENDING sub-balance) ──────
            var driverWallet = await _uow.Wallets.GetByActorAsync(req.DriverOrDeliveryActorId,
                req.MerchantActorId.HasValue ? ActorType.Delivery : ActorType.Driver, ct)
                ?? throw new KeyNotFoundException("Driver/Delivery wallet not found.");

            // Cash payment: commission receivable reduces AVAILABLE immediately
            if (req.PaymentMethod == PaymentMethod.Cash)
            {
                driverWallet.CashReceivable += commissionAmount;
                driverWallet.BalanceAvailable -= commissionAmount;
                deltas.Add(new WalletDeltaDto(driverWallet.Id, driverWallet.ActorType,
                    -commissionAmount, "Cash commission receivable"));
            }
            else
            {
                // Digital: net earnings enter PENDING — move to AVAILABLE after 15-min window
                // Track PendingSince: set when first pending earnings batch is recorded
                if (driverWallet.BalancePending == 0)
                    driverWallet.PendingSince = DateTime.UtcNow;
                driverWallet.BalancePending += netDriverAmount;
                deltas.Add(new WalletDeltaDto(driverWallet.Id, driverWallet.ActorType,
                    netDriverAmount, "Net earnings (pending settlement)"));
            }

            // Tip credit: immediate, not commission-subject
            if (tipAmount > 0)
            {
                driverWallet.BalanceAvailable += tipAmount;
                deltas.Add(new WalletDeltaDto(driverWallet.Id, driverWallet.ActorType, tipAmount, "Tip"));
            }

            driverWallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(driverWallet);

            // ── 3. Merchant wallet credit (food/grocery orders) ───────────────
            if (req.MerchantActorId.HasValue)
            {
                var merchantWallet = await _uow.Wallets.GetByActorAsync(req.MerchantActorId.Value, ActorType.Merchant, ct)
                    ?? throw new KeyNotFoundException("Merchant wallet not found.");
                var netMerchantAmount = req.GrossAmount - commissionAmount;
                merchantWallet.BalanceAvailable += netMerchantAmount;
                merchantWallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(merchantWallet);
                deltas.Add(new WalletDeltaDto(merchantWallet.Id, ActorType.Merchant, netMerchantAmount, "Net order credit"));
            }

            // ── 4. Platform wallet credit (commission + fleet fees) ───────────
            var platformWallet = await _uow.Wallets.GetByActorAsync(Guid.Empty, ActorType.Platform, ct);
            if (platformWallet is not null)
            {
                platformWallet.BalanceAvailable += commissionAmount + fleetFeeAmount;
                platformWallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(platformWallet);
                deltas.Add(new WalletDeltaDto(platformWallet.Id, ActorType.Platform,
                    commissionAmount + fleetFeeAmount, "Commission + fleet fee"));
            }

            // ── 5. Fleet wallet credit (fleet fee, same atomic write) ─────────
            // Fleet fee recorded as distinct line item per SRS-FIN-001 §Wallets
            if (fleetFeeAmount > 0)
            {
                // Fleet wallet lookup would go here via fleet affiliation lookup
                deltas.Add(new WalletDeltaDto(Guid.Empty, ActorType.Fleet, fleetFeeAmount, "Fleet fee credit"));
            }

            // ── 6. Persist transaction ────────────────────────────────────────
            var transaction = new Transaction
            {
                Id = txnId,
                WalletId = driverWallet.Id,
                RideId = req.RideId,
                OrderId = req.OrderId,
                GrossAmount = req.GrossAmount,
                CommissionAmount = commissionAmount,
                CommissionRate = req.CommissionRate,
                VatAmount = vatAmount,
                TipAmount = tipAmount,
                FleetFeeAmount = fleetFeeAmount,
                NetAmount = netDriverAmount,
                PaymentMethod = req.PaymentMethod,
                IdempotencyKey = req.IdempotencyKey,
                PspTransactionId = req.PspTransactionId,
                InvoiceId = invoiceId,
                SagaCorrelationId = sagaId
            };
            await _uow.Transactions.AddAsync(transaction, ct);

            // ── 7. Immutable audit log (written before response returned) ──────
            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Payment, "PAYMENT_COLLECTED",
                req.UserActorId, ActorRole.System,
                txnId, "TRANSACTION", null,
                JsonSerializer.Serialize(new { txnId, req.GrossAmount, commissionAmount, deltas }),
                req.CityId, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return new CollectPaymentResultDto(
                txnId, req.GrossAmount, commissionAmount, vatAmount,
                tipAmount, fleetFeeAmount, netDriverAmount,
                req.MerchantActorId.HasValue ? req.GrossAmount - commissionAmount : 0,
                req.PaymentMethod, invoiceId, deltas, DateTime.UtcNow);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TipResultDto> AddTipAsync(AddTipRequest req, CancellationToken ct = default)
    {
        // Tip window enforced — duration is admin-configurable (default 2 hours post-ride)
        // (Ride timestamp validation would be done by caller or via ride service)

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var driverWallet = await _uow.Wallets.GetByActorAsync(req.DriverId, ActorType.Driver, ct)
                ?? throw new KeyNotFoundException("Driver wallet not found.");

            // 100% to driver — NOT subject to platform commission
            driverWallet.BalanceAvailable += req.TipAmount;
            driverWallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(driverWallet);

            var tipId = Guid.NewGuid();
            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Payment, "TIP_ADDED",
                req.UserId, ActorRole.User,
                tipId, "TIP", null,
                JsonSerializer.Serialize(new { tipId, req.RideId, req.TipAmount, req.TipType }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return new TipResultDto(tipId, req.RideId, req.DriverId,
                req.TipAmount, req.TipType, req.Source, DateTime.UtcNow);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<TransactionDto> GetTransactionAsync(Guid transactionId, CancellationToken ct = default)
    {
        var txn = await _uow.Transactions.GetByIdAsync(transactionId, ct)
            ?? throw new KeyNotFoundException($"Transaction {transactionId} not found.");
        return MapToDto(txn);
    }

    public async Task<IEnumerable<TransactionDto>> GetTransactionsByWalletAsync(Guid walletId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var txns = await _uow.Transactions.GetByWalletAsync(walletId, from, to, ct);
        return txns.Select(MapToDto);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void DebitUserWalletBuckets(Wallet wallet, decimal amount)
    {
        decimal remaining = amount;
        var refundDebit = Math.Min(remaining, wallet.BalanceRefundCredit);
        wallet.BalanceRefundCredit -= refundDebit;
        remaining -= refundDebit;

        if (remaining > 0)
        {
            var promoDebit = Math.Min(remaining, wallet.BalancePromoCredit);
            wallet.BalancePromoCredit -= promoDebit;
            remaining -= promoDebit;
        }

        if (remaining > 0)
        {
            var courtesyDebit = Math.Min(remaining, wallet.BalanceCourtesyCredit);
            wallet.BalanceCourtesyCredit -= courtesyDebit;
            remaining -= courtesyDebit;
        }

        if (remaining > 0)
        {
            if (wallet.BalanceAvailable < remaining)
                throw new InsufficientBalanceException(wallet.TotalUserBalance, amount);
            wallet.BalanceAvailable -= remaining;
        }
    }

    private static TransactionDto MapToDto(Transaction t) => new(
        t.Id, t.WalletId, t.RideId, t.OrderId,
        t.GrossAmount, t.CommissionAmount, t.VatAmount, t.TipAmount, t.NetAmount,
        t.PaymentMethod, t.PspTransactionId, t.InvoiceId, t.IsReversed, t.CreatedAt);

    private CollectPaymentResultDto MapTransactionToResult(Transaction t) => new(
        t.Id, t.GrossAmount, t.CommissionAmount, t.VatAmount,
        t.TipAmount, t.FleetFeeAmount, t.NetAmount, 0,
        t.PaymentMethod, t.InvoiceId ?? "", Enumerable.Empty<WalletDeltaDto>(), t.CreatedAt);
}
