using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using System.Text.Json;

namespace BjeekFinance.Application.Services;

/// <summary>
/// UC-FIN-CASH-01: Cash ride/order settlement.
/// Records expected cash from COD trips, accepts driver-reported cash,
/// computes variance, auto-adjusts ≤ SAR 3, flags > SAR 3 for admin review.
/// On completion: cash_receivable cleared, AVAILABLE adjusted.
/// </summary>
public class CashSettlementService : ICashSettlementService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public CashSettlementService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<CashSettlementDto> SubmitSettlementAsync(SubmitCashSettlementRequest req, CancellationToken ct = default)
    {
        var varianceThreshold = await _uow.FinanceParameters.GetDecimalAsync("cash_settlement_variance_threshold", 3m, null, null, ct);

        // ── 1. Validate no pending settlement exists ──────────────────────────
        var existing = await _uow.CashSettlements.GetPendingByDriverAsync(req.DriverId, ct);
        if (existing is not null)
            throw new InvalidOperationException(
                $"Driver {req.DriverId} already has a pending settlement ({existing.Id}). Complete or cancel it first.");

        // ── 2. Load wallet ────────────────────────────────────────────────────
        var wallet = await _uow.Wallets.GetByIdWithLockAsync(req.WalletId, ct)
            ?? throw new KeyNotFoundException($"Wallet {req.WalletId} not found.");

        var variance = req.ReportedCashTotal - req.ExpectedCashTotal;
        var isFlagged = Math.Abs(variance) > varianceThreshold;

        var status = isFlagged
            ? CashSettlementStatus.FlaggedForReview
            : CashSettlementStatus.Submitted;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // ── 3. If variance ≤ SAR 3: auto-adjust ledger ───────────────────
            if (!isFlagged)
            {
                // Clear cash_receivable (expected commission already deducted from AVAILABLE)
                var receivableBefore = wallet.CashReceivable;
                wallet.CashReceivable -= req.CommissionReceivableAmount;
                if (wallet.CashReceivable < 0) wallet.CashReceivable = 0;

                // Record the actual cash collected difference in AVAILABLE
                // If variance is negative (shortfall), driver owes more → reduce AVAILABLE
                // If variance is positive (surplus), driver gets credit → increase AVAILABLE
                wallet.BalanceAvailable += variance;
                wallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(wallet);

                status = CashSettlementStatus.AutoAdjusted;
            }
            else
            {
                // Flagged: hold the cash_receivable as-is pending admin review
                // No wallet changes until admin resolves
                wallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(wallet);
            }

            // ── 4. Create settlement record ───────────────────────────────────
            var settlement = new CashSettlement
            {
                DriverId = req.DriverId,
                WalletId = req.WalletId,
                ExpectedCashTotal = req.ExpectedCashTotal,
                ReportedCashTotal = req.ReportedCashTotal,
                VarianceAmount = variance,
                CommissionReceivableAmount = req.CommissionReceivableAmount,
                Status = status,
                VarianceFlag = isFlagged,
                TripIdsJson = req.TripIdsJson,
                Notes = req.Notes,
                CompletedAt = isFlagged ? null : DateTime.UtcNow
            };
            await _uow.CashSettlements.AddAsync(settlement, ct);

            // ── 5. Audit log ──────────────────────────────────────────────────
            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.CashSettlement,
                isFlagged ? "CASH_SETTLEMENT_FLAGGED" : "CASH_SETTLEMENT_AUTO_ADJUSTED",
                req.DriverId, ActorRole.Driver,
                settlement.Id, "CASH_SETTLEMENT",
                JsonSerializer.Serialize(new
                {
                    expected = req.ExpectedCashTotal,
                    reported = req.ReportedCashTotal,
                    variance,
                    receivableBefore = wallet.CashReceivable + req.CommissionReceivableAmount
                }),
                JsonSerializer.Serialize(new
                {
                    settlementId = settlement.Id,
                    status = status.ToString(),
                    varianceFlag = isFlagged,
                    cashReceivableAfter = wallet.CashReceivable
                }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return MapToDto(settlement);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<CashSettlementDto> ReviewSettlementAsync(Guid settlementId, Guid adminId, string resolutionNotes, CancellationToken ct = default)
    {
        var settlement = await _uow.CashSettlements.GetByIdAsync(settlementId, ct)
            ?? throw new KeyNotFoundException($"Cash settlement {settlementId} not found.");

        if (settlement.Status != CashSettlementStatus.FlaggedForReview)
            throw new InvalidOperationException($"Settlement is not flagged for review. Current status: {settlement.Status}.");

        var wallet = await _uow.Wallets.GetByIdWithLockAsync(settlement.WalletId, ct)!;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // Admin resolves: clear cash_receivable and adjust AVAILABLE for variance
            wallet!.CashReceivable -= settlement.CommissionReceivableAmount;
            if (wallet.CashReceivable < 0) wallet.CashReceivable = 0;

            wallet.BalanceAvailable += settlement.VarianceAmount;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);

            settlement.Status = CashSettlementStatus.Completed;
            settlement.ReviewedByActorId = adminId;
            settlement.ReviewedAt = DateTime.UtcNow;
            settlement.CompletedAt = DateTime.UtcNow;
            settlement.Notes = resolutionNotes;
            _uow.CashSettlements.Update(settlement);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.CashSettlement, "CASH_SETTLEMENT_REVIEWED",
                adminId, ActorRole.FinanceAdmin,
                settlement.Id, "CASH_SETTLEMENT",
                null,
                JsonSerializer.Serialize(new
                {
                    settlementId,
                    variance = settlement.VarianceAmount,
                    resolutionNotes,
                    cashReceivableAfter = wallet.CashReceivable
                }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return MapToDto(settlement);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<CashSettlementDto> GetSettlementAsync(Guid settlementId, CancellationToken ct = default)
    {
        var settlement = await _uow.CashSettlements.GetByIdAsync(settlementId, ct)
            ?? throw new KeyNotFoundException($"Cash settlement {settlementId} not found.");
        return MapToDto(settlement);
    }

    public async Task<IEnumerable<CashSettlementDto>> GetByDriverAsync(Guid driverId, CancellationToken ct = default)
    {
        var settlements = await _uow.CashSettlements.GetByDriverAsync(driverId, ct);
        return settlements.Select(MapToDto);
    }

    public async Task<IEnumerable<CashSettlementDto>> GetFlaggedForReviewAsync(CancellationToken ct = default)
    {
        var settlements = await _uow.CashSettlements.GetFlaggedForReviewAsync(ct);
        return settlements.Select(MapToDto);
    }

    // ── UC-AD-FIN-03: Reconciliation Dashboard ─────────────────────────────────

    public async Task<ReconciliationDashboardDto> GetDashboardAsync(DateTime from, DateTime to, Guid? cityId = null, CancellationToken ct = default)
    {
        var settlements = await _uow.CashSettlements.GetByDateRangeAsync(from, to, cityId, ct);
        var list = settlements.ToList();

        var green = list.Where(s => Math.Abs(s.VarianceAmount) <= 3m && s.Status == CashSettlementStatus.AutoAdjusted);
        var yellow = list.Where(s => Math.Abs(s.VarianceAmount) > 3m && Math.Abs(s.VarianceAmount) <= 20m && s.VarianceFlag);
        var red = list.Where(s => Math.Abs(s.VarianceAmount) > 20m && s.VarianceFlag);

        var lines = list.Select(s =>
        {
            var absVar = Math.Abs(s.VarianceAmount);
            var severity = absVar <= 3m ? "green" : absVar <= 20m ? "yellow" : "red";
            return new ReconciliationSettlementLineDto(
                s.Id, s.DriverId, s.ExpectedCashTotal, s.ReportedCashTotal,
                s.VarianceAmount, s.Status, s.VarianceFlag, severity,
                s.Notes, s.EscalatedToFraudAt.HasValue, s.CreatedAt);
        });

        // Ledger reconciliation check: compare sum of ExpectedCashTotal vs LedgerTotal
        var allLedgerMatch = list.All(s => s.LedgerMatch);
        var totalExpected = list.Sum(s => s.ExpectedCashTotal);
        var totalReported = list.Sum(s => s.ReportedCashTotal);

        return new ReconciliationDashboardDto(
            from, to, cityId,
            list.Count,
            green.Count(), yellow.Count(), red.Count(),
            list.Sum(s => s.VarianceAmount),
            totalExpected, totalReported,
            allLedgerMatch, lines);
    }

    public async Task<CashSettlementDto> EscalateToFraudAsync(Guid settlementId, Guid adminId, string notes, CancellationToken ct = default)
    {
        var settlement = await _uow.CashSettlements.GetByIdAsync(settlementId, ct)
            ?? throw new KeyNotFoundException($"Cash settlement {settlementId} not found.");

        if (!settlement.VarianceFlag)
            throw new InvalidOperationException("Only flagged settlements can be escalated to fraud.");

        if (settlement.EscalatedToFraudAt.HasValue)
            throw new InvalidOperationException("Settlement already escalated to fraud.");

        settlement.EscalatedToFraudAt = DateTime.UtcNow;
        settlement.FraudCaseId = $"FR-{settlement.Id:N}".ToUpperInvariant(); // UC-AD-FIN-05 placeholder
        settlement.Notes = string.IsNullOrEmpty(settlement.Notes)
            ? notes
            : $"{settlement.Notes}; {notes}";
        _uow.CashSettlements.Update(settlement);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Fraud, "CASH_SETTLEMENT_ESCALATED",
            adminId, ActorRole.FinanceAdmin,
            settlementId, "CASH_SETTLEMENT", null,
            JsonSerializer.Serialize(new
            {
                settlementId,
                variance = settlement.VarianceAmount,
                fraudCaseId = settlement.FraudCaseId,
                notes
            }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapToDto(settlement);
    }

    public async Task<CashReconciliationReportDto> GenerateReportAsync(DateTime from, DateTime to, Guid? cityId, Guid adminId, CancellationToken ct = default)
    {
        var settlements = await _uow.CashSettlements.GetByDateRangeAsync(from, to, cityId, ct);
        var list = settlements.ToList();

        var totalExpected = list.Sum(s => s.ExpectedCashTotal);
        var totalReported = list.Sum(s => s.ReportedCashTotal);
        var totalVariance = list.Sum(s => s.VarianceAmount);
        var autoAdjusted = list.Count(s => s.Status == CashSettlementStatus.AutoAdjusted);
        var flagged = list.Count(s => s.VarianceFlag);
        var escalated = list.Count(s => s.EscalatedToFraudAt.HasValue);
        var allLedgerMatch = list.All(s => s.LedgerMatch);

        // Build CSV content
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("SettlementId,DriverId,ExpectedCash,ReportedCash,Variance,Status,VarianceFlag,Severity,Notes,CreatedAt");
        foreach (var s in list)
        {
            var absVar = Math.Abs(s.VarianceAmount);
            var severity = absVar <= 3m ? "green" : absVar <= 20m ? "yellow" : "red";
            csv.AppendLine($"{s.Id},{s.DriverId},{s.ExpectedCashTotal},{s.ReportedCashTotal},{s.VarianceAmount},{s.Status},{s.VarianceFlag},{severity},\"{s.Notes?.Replace("\"", "\"\"")}\",{s.CreatedAt:O}");
        }

        // Add summary rows
        csv.AppendLine();
        csv.AppendLine($"Total Expected Cash,{totalExpected}");
        csv.AppendLine($"Total Reported Cash,{totalReported}");
        csv.AppendLine($"Total Variance,{totalVariance}");
        csv.AppendLine($"Auto-Adjusted Count,{autoAdjusted}");
        csv.AppendLine($"Flagged Count,{flagged}");
        csv.AppendLine($"Escalated Count,{escalated}");
        csv.AppendLine($"Ledger Reconciled,{allLedgerMatch}");

        var report = new ReconciliationReport
        {
            DateFrom = from,
            DateTo = to,
            CityId = cityId,
            TotalExpectedCash = totalExpected,
            TotalReportedCash = totalReported,
            TotalVariance = totalVariance,
            AutoAdjustedCount = autoAdjusted,
            FlaggedCount = flagged,
            EscalatedCount = escalated,
            LedgerReconciled = allLedgerMatch,
            ReportDataJson = System.Text.Json.JsonSerializer.Serialize(list.Select(MapToDto)),
            CsvContent = csv.ToString(),
            GeneratedByActorId = adminId
        };
        await _uow.ReconciliationReports.AddAsync(report, ct);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.CashSettlement, "RECONCILIATION_REPORT_GENERATED",
            adminId, ActorRole.FinanceAdmin,
            report.Id, "RECONCILIATION_REPORT", null,
            JsonSerializer.Serialize(new
            {
                from, to, cityId,
                totalExpected, totalReported, totalVariance,
                autoAdjusted, flagged, escalated, allLedgerMatch
            }),
            cityId, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapReportToDto(report);
    }

    public async Task<IEnumerable<CashReconciliationReportDto>> GetReportsAsync(DateTime from, DateTime to, Guid? cityId = null, CancellationToken ct = default)
    {
        var reports = await _uow.ReconciliationReports.GetByDateRangeAsync(from, to, cityId, ct);
        return reports.Select(MapReportToDto);
    }

    private static CashSettlementDto MapToDto(CashSettlement s) => new(
        s.Id, s.DriverId, s.WalletId,
        s.ExpectedCashTotal, s.ReportedCashTotal, s.VarianceAmount,
        s.CommissionReceivableAmount, s.Status, s.VarianceFlag,
        s.TripIdsJson, s.Notes, s.ReviewedByActorId,
        s.ReviewedAt, s.CompletedAt, s.CreatedAt);

    private static CashReconciliationReportDto MapReportToDto(ReconciliationReport r) => new(
        r.Id, r.DateFrom, r.DateTo, r.CityId,
        r.TotalExpectedCash, r.TotalReportedCash, r.TotalVariance,
        r.AutoAdjustedCount, r.FlaggedCount, r.EscalatedCount,
        r.LedgerReconciled, r.GeneratedByActorId, r.GeneratedAt);
}
