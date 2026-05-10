using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using BjeekFinance.Domain.Exceptions;
using System.Text.Json;

namespace BjeekFinance.Application.Services;

public class WalletService : IWalletService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public WalletService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<WalletDto> GetWalletAsync(Guid actorId, ActorType actorType, CancellationToken ct = default)
    {
        var wallet = await _uow.Wallets.GetByActorAsync(actorId, actorType, ct)
            ?? throw new KeyNotFoundException($"Wallet not found for actor {actorId} ({actorType}).");
        return MapToDto(wallet);
    }

    public async Task<WalletDto> GetWalletByIdAsync(Guid walletId, CancellationToken ct = default)
    {
        var wallet = await _uow.Wallets.GetByIdAsync(walletId, ct)
            ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");
        return MapToDto(wallet);
    }

    public async Task CreditAsync(Guid walletId, decimal amount, string subtype, Guid initiatorId, ActorRole initiatorRole, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var wallet = await _uow.Wallets.GetByIdWithLockAsync(walletId, ct)
                ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");

            var before = SerializeState(wallet);
            wallet.BalanceAvailable += amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Wallet, subtype, initiatorId, initiatorRole,
                walletId, "WALLET", before, SerializeState(wallet), null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DebitAsync(Guid walletId, decimal amount, string subtype, Guid initiatorId, ActorRole initiatorRole, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var wallet = await _uow.Wallets.GetByIdWithLockAsync(walletId, ct)
                ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");

            // Fraud gate: critical risk = all operations suspended
            if (wallet.FraudScore >= 80)
                throw new WalletFrozenException();

            var before = SerializeState(wallet);

            if (wallet.ActorType == ActorType.User)
                DebitUserWalletBuckets(wallet, amount);
            else
            {
                if (wallet.BalanceAvailable < amount)
                    throw new InsufficientBalanceException(wallet.BalanceAvailable, amount);
                wallet.BalanceAvailable -= amount;
            }

            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Wallet, subtype, initiatorId, initiatorRole,
                walletId, "WALLET", before, SerializeState(wallet), null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task HoldAsync(Guid walletId, decimal amount, string reason, Guid initiatorId, ActorRole initiatorRole, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var wallet = await _uow.Wallets.GetByIdWithLockAsync(walletId, ct)
                ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");

            if (wallet.BalanceAvailable < amount)
                throw new InsufficientBalanceException(wallet.BalanceAvailable, amount);

            var before = SerializeState(wallet);
            wallet.BalanceAvailable -= amount;
            wallet.BalanceHold += amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Wallet, "WALLET_HOLD", initiatorId, initiatorRole,
                walletId, "WALLET", before, SerializeState(wallet), null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task ReleaseHoldAsync(Guid walletId, decimal amount, string reason, Guid initiatorId, ActorRole initiatorRole, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var wallet = await _uow.Wallets.GetByIdWithLockAsync(walletId, ct)
                ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");

            var releaseAmount = Math.Min(amount, wallet.BalanceHold);
            var before = SerializeState(wallet);
            wallet.BalanceHold -= releaseAmount;
            wallet.BalanceAvailable += releaseAmount;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Wallet, "WALLET_RELEASE_HOLD", initiatorId, initiatorRole,
                walletId, "WALLET", before, SerializeState(wallet), null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task AdminBalanceCorrectionAsync(Guid walletId, decimal correctionAmount, string reason, Guid adminId, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var wallet = await _uow.Wallets.GetByIdWithLockAsync(walletId, ct)
                ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");

            var before = SerializeState(wallet);
            wallet.BalanceAvailable += correctionAmount;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            // Finance Admin only — immutable audit log required
            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.AdminOverride, "BALANCE_CORRECTION", adminId, ActorRole.FinanceAdmin,
                walletId, "WALLET", before, SerializeState(wallet), null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task SettlePendingEarningsAsync(Guid walletId, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var wallet = await _uow.Wallets.GetByIdWithLockAsync(walletId, ct)
                ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");

            if (wallet.BalancePending <= 0)
                return;

            var before = SerializeState(wallet);
            var settledAmount = wallet.BalancePending;
            wallet.BalancePending = 0;
            wallet.BalanceAvailable += settledAmount;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Wallet, "PENDING_SETTLED", Guid.Empty, ActorRole.System,
                walletId, "WALLET", before, SerializeState(wallet), null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task RecordCashCommissionReceivableAsync(Guid walletId, decimal commissionAmount, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var wallet = await _uow.Wallets.GetByIdWithLockAsync(walletId, ct)
                ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");

            var before = SerializeState(wallet);
            // Cash commission receivable reduces AVAILABLE immediately —
            // prevents Instant Pay from withdrawing cash covering unsettled commission.
            wallet.CashReceivable += commissionAmount;
            wallet.BalanceAvailable -= commissionAmount;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.CashSettlement, "CASH_COMMISSION_RECEIVABLE_RECORDED",
                Guid.Empty, ActorRole.System, walletId, "WALLET",
                before, SerializeState(wallet), null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<IEnumerable<WalletSummaryDto>> GetWalletsByActorTypeAsync(ActorType actorType, CancellationToken ct = default)
    {
        var wallets = await _uow.Wallets.GetByInstantPayTierAsync(InstantPayTier.TierA, ct);
        return wallets.Select(w => new WalletSummaryDto(
            w.Id, w.ActorId, w.ActorType, w.BalanceAvailable,
            w.BalancePending, w.KycStatus, w.IsInDunning, w.FraudScore));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Debit User Wallet respecting credit consumption order:
    /// 1. RefundCredit (no expiry, highest priority)
    /// 2. PromoCredit (expiry risk)
    /// 3. CourtesyCredit
    /// 4. Remainder from BalanceAvailable
    /// </summary>
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

    private static string SerializeState(object entity) =>
        JsonSerializer.Serialize(entity, new JsonSerializerOptions { WriteIndented = false });

    private static WalletDto MapToDto(Wallet w) => new(
        w.Id, w.ActorType, w.ActorId, w.Currency,
        w.BalanceAvailable, w.BalancePending, w.BalanceHold, w.CashReceivable,
        w.BalanceRefundCredit, w.BalancePromoCredit, w.BalanceCourtesyCredit,
        w.LoyaltyPoints, w.KycStatus, w.InstantPayTier, w.InstantPayEnabled,
        w.IsInDunning, w.DunningBucket, w.FraudScore, w.CreatedAt, w.UpdatedAt);
}
