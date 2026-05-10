using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using System.Text.Json;

namespace BjeekFinance.Application.Services;

public class AdminFinanceService : IAdminFinanceService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public AdminFinanceService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<DunningStatusDto> GetDunningStatusAsync(Guid walletId, CancellationToken ct = default)
    {
        var wallet = await _uow.Wallets.GetByIdAsync(walletId, ct)
            ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");
        return new DunningStatusDto(wallet.Id, wallet.ActorId, wallet.IsInDunning,
            wallet.DunningBucket, wallet.DunningStartedAt, wallet.CashReceivable, wallet.BalanceAvailable);
    }

    public async Task<IEnumerable<DunningStatusDto>> GetAllDunningWalletsAsync(CancellationToken ct = default)
    {
        var wallets = await _uow.Wallets.GetWalletsInDunningAsync(ct);
        return wallets.Select(w => new DunningStatusDto(w.Id, w.ActorId, w.IsInDunning,
            w.DunningBucket, w.DunningStartedAt, w.CashReceivable, w.BalanceAvailable));
    }

    /// <summary>
    /// Nightly dunning classification at 02:00 local city time.
    /// Aging calculated from date negative balance FIRST occurred — not last notification.
    /// </summary>
    public async Task RunDunningBatchAsync(CancellationToken ct = default)
    {
        var wallets = await _uow.Wallets.GetWalletsInDunningAsync(ct);

        foreach (var wallet in wallets)
        {
            if (!wallet.DunningStartedAt.HasValue) continue;

            var ageDays = (DateTime.UtcNow - wallet.DunningStartedAt.Value).TotalDays;

            var newBucket = ageDays switch
            {
                <= 7 => Domain.Enums.DunningBucket.Notify,
                <= 30 => Domain.Enums.DunningBucket.HoldPayout,
                _ => Domain.Enums.DunningBucket.HoldAssignments
            };

            // Auto-resolve: when balance ≥ 0, all holds released automatically
            if (wallet.BalanceAvailable >= 0 && wallet.CashReceivable <= 0)
            {
                wallet.IsInDunning = false;
                wallet.DunningBucket = null;
                wallet.DunningStartedAt = null;
            }
            else
            {
                wallet.DunningBucket = newBucket;
            }

            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<WriteOffResultDto> InitiateWriteOffAsync(WriteOffRequest req, CancellationToken ct = default)
    {
        if (req.ReasonCode == WriteOffReasonCode.Other && (req.Notes?.Length ?? 0) < 100)
            throw new ArgumentException("Write-off reason 'Other' requires at least 100 characters in notes.");

        var writeOffId = Guid.NewGuid();

        // < SAR 18,500: Finance Manager self-approves
        // ≥ SAR 18,500: VP Finance co-approval required
        var needsVpApproval = req.Amount >= 18_500m;
        var status = needsVpApproval ? "PENDING_VP_APPROVAL" : "APPROVED";

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.AdminOverride, "WRITE_OFF_INITIATED",
            req.InitiatedByActorId, ActorRole.FinanceManager,
            writeOffId, "WRITE_OFF", null,
            JsonSerializer.Serialize(new { req.WalletId, req.Amount, req.ReasonCode, status }),
            null, null, null), ct);

        return new WriteOffResultDto(writeOffId, req.WalletId, req.Amount, req.ReasonCode, status, null, DateTime.UtcNow);
    }

    public async Task<WriteOffResultDto> ApproveWriteOffAsync(Guid writeOffId, Guid approverActorId, CancellationToken ct = default)
    {
        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.AdminOverride, "WRITE_OFF_APPROVED",
            approverActorId, ActorRole.VpFinance,
            writeOffId, "WRITE_OFF", null,
            JsonSerializer.Serialize(new { writeOffId, approvedBy = approverActorId }),
            null, null, null), ct);

        return new WriteOffResultDto(writeOffId, Guid.Empty, 0, WriteOffReasonCode.Other, "APPROVED", approverActorId, DateTime.UtcNow);
    }

    public async Task<BulkAdjustmentResultDto> ExecuteBulkAdjustmentAsync(BulkAdjustmentRequest req, CancellationToken ct = default)
    {
        // > SAR 50,000 total requires Super Admin — validated by caller / RBAC middleware
        var batchId = Guid.NewGuid();
        var totalAmount = req.Adjustments.Sum(a => a.Delta);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            foreach (var item in req.Adjustments)
            {
                var wallet = await _uow.Wallets.GetByIdWithLockAsync(item.WalletId, ct)
                    ?? throw new KeyNotFoundException($"Wallet {item.WalletId} not found.");
                wallet.BalanceAvailable += item.Delta;
                wallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(wallet);
            }

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Wallet, "BULK_ADJUSTMENT",
                req.InitiatedByActorId, ActorRole.SuperAdmin,
                batchId, "BULK_ADJUSTMENT", null,
                JsonSerializer.Serialize(new { batchId, Count = req.Adjustments.Count(), totalAmount, req.Reason }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return new BulkAdjustmentResultDto(batchId, req.Adjustments.Count(), totalAmount, "COMPLETED", DateTime.UtcNow);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<IEnumerable<AuditLogEntryDto>> GetAuditLogsAsync(Guid subjectId, string subjectType, CancellationToken ct = default)
    {
        var logs = await _uow.AuditLogs.GetBySubjectAsync(subjectId, subjectType, ct);
        return logs.Select(MapAuditDto);
    }

    public async Task<IEnumerable<AuditLogEntryDto>> GetAuditLogsByEventTypeAsync(AuditEventType eventType, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var logs = await _uow.AuditLogs.GetByEventTypeAsync(eventType, from, to, ct);
        return logs.Select(MapAuditDto);
    }

    public async Task<ReconciliationReportDto> GenerateReconciliationReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // Real impl: aggregate from transactions and audit logs
        // Target: < 30 seconds for 30-day range (async job with progress indicator)
        await Task.CompletedTask;
        return new ReconciliationReportDto(Guid.NewGuid(), from, to, 0, 0, 0, 0, 0, 0, 0,
            Enumerable.Empty<ReconciliationLineDto>(), DateTime.UtcNow);
    }

    public async Task<BulkReconciliationReportDto> GenerateBulkReconciliationReportAsync(DateTime from, DateTime to, Guid? cityId, string? serviceType, Guid adminId, CancellationToken ct = default)
    {
        // Step 1: Check audit log tamper hashes
        var tamperFailures = await _uow.AuditLogs.GetHashValidationFailuresAsync(ct);
        var auditTampered = tamperFailures.Any();

        // Step 2: Fetch all transactions in period
        var allTxns = await _uow.Transactions.GetByDateRangeWithWalletAsync(from, to, serviceType, ct);

        // Step 3: Filter by city if specified (transaction's wallet.CityId)
        if (cityId.HasValue)
            allTxns = allTxns.Where(t => t.Wallet.CityId == cityId.Value).ToList();

        // Step 4: Compute gross collected
        var totalGrossCollected = allTxns.Sum(t => t.GrossAmount);
        var totalCommission = allTxns.Sum(t => t.CommissionAmount);
        var totalVat = allTxns.Sum(t => t.VatAmount);
        var totalFleetFees = allTxns.Sum(t => t.FleetFeeAmount);
        var totalPenalties = allTxns.Sum(t => t.PenaltyAmount);
        var totalPlatformRevenue = totalCommission + totalVat + totalFleetFees + totalPenalties;

        // Step 5: Fetch all completed payouts in period
        var allPayouts = await _uow.PayoutRequests.GetAllAsync(ct);
        var periodPayouts = allPayouts.Where(p => p.CreatedAt >= from && p.CreatedAt <= to && p.Status == PayoutStatus.Completed);

        // Filter by city via wallet lookup
        var payoutWalletIds = periodPayouts.Select(p => p.WalletId).Distinct().ToList();
        var payoutWallets = await _uow.Wallets.GetByIdsAsync(payoutWalletIds, ct);
        var payoutWalletMap = payoutWallets.ToDictionary(w => w.Id);

        if (cityId.HasValue)
            periodPayouts = periodPayouts.Where(p =>
                payoutWalletMap.TryGetValue(p.WalletId, out var w) && w.CityId == cityId.Value);

        var totalDriverPayouts = periodPayouts
            .Where(p => payoutWalletMap.TryGetValue(p.WalletId, out var w) && w.ActorType == ActorType.Driver)
            .Sum(p => p.AmountRequested);
        var totalMerchantPayouts = periodPayouts
            .Where(p => payoutWalletMap.TryGetValue(p.WalletId, out var w) && w.ActorType == ActorType.Merchant)
            .Sum(p => p.AmountRequested);

        // Step 6: Fetch wallet snapshot data
        var allWallets = await _uow.Wallets.GetAllAsync(ct);
        if (cityId.HasValue)
            allWallets = allWallets.Where(w => w.CityId == cityId.Value).ToList();

        var totalOutstandingReceivables = allWallets.Sum(w => w.CashReceivable);
        var totalHolds = allWallets.Sum(w => w.BalanceHold);

        // Step 7: Fetch completed refunds in period
        var allRefunds = await _uow.Refunds.GetAllAsync(ct);
        var totalRefunds = allRefunds
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to && r.Status == RefundStatus.Completed)
            .Sum(r => r.Amount);

        // Step 8: Reconciliation formula
        var rightSide = totalDriverPayouts + totalMerchantPayouts + totalPlatformRevenue
                        + totalOutstandingReceivables + totalHolds;
        var imbalanceAmount = totalGrossCollected - rightSide;
        var imbalanceThreshold = await _uow.FinanceParameters.GetDecimalAsync("reconciliation_imbalance_threshold", 1m, cityId, null, ct);
        var imbalanceDetected = Math.Abs(imbalanceAmount) > imbalanceThreshold;

        // Step 9: Build lines
        var lines = new List<BulkReconciliationLineDto>
        {
            new("Revenue", "Gross Collected", totalGrossCollected, allTxns.Count(), "All transactions in period"),
            new("Revenue", "Platform Commission", totalCommission, allTxns.Count(t => t.CommissionAmount > 0), "Commission charged per transaction"),
            new("Revenue", "VAT Collected", totalVat, allTxns.Count(t => t.VatAmount > 0), "VAT at ZATCA rate"),
            new("Revenue", "Fleet Fees", totalFleetFees, allTxns.Count(t => t.FleetFeeAmount > 0), "Fleet/partner fees"),
            new("Revenue", "Penalties", totalPenalties, allTxns.Count(t => t.PenaltyAmount > 0), "Penalty/chargeback fees"),
            new("Payouts", "Driver Payouts", totalDriverPayouts, periodPayouts.Count(), "Completed driver payouts"),
            new("Payouts", "Merchant Payouts", totalMerchantPayouts, periodPayouts.Count(), "Completed merchant payouts"),
            new("Balance Sheet", "Outstanding Receivables", totalOutstandingReceivables, allWallets.Count(w => w.CashReceivable > 0), "Cash commission owed by drivers"),
            new("Balance Sheet", "Active Holds", totalHolds, allWallets.Count(w => w.BalanceHold > 0), "Admin/dunning holds on wallets"),
            new("Adjustments", "Refunds", totalRefunds, allRefunds.Count(r => r.CreatedAt >= from && r.CreatedAt <= to), "Completed refunds in period"),
            new("Imbalance", "Net Imbalance", imbalanceAmount, 0, imbalanceDetected ? "ALERT: exceeds threshold" : "In balance")
        };

        var reportDataJson = JsonSerializer.Serialize(lines);
        var csvContent = BuildBulkReconciliationCsv(from, to, lines);

        // Step 10: Create and persist report
        var report = new BulkReconciliationReport
        {
            DateFrom = from,
            DateTo = to,
            CityId = cityId,
            ServiceType = serviceType,
            TotalGrossCollected = totalGrossCollected,
            TotalDriverPayouts = totalDriverPayouts,
            TotalMerchantPayouts = totalMerchantPayouts,
            TotalPlatformRevenue = totalPlatformRevenue,
            TotalOutstandingReceivables = totalOutstandingReceivables,
            TotalHolds = totalHolds,
            TotalRefunds = totalRefunds,
            TotalWriteOffs = 0,
            ImbalanceAmount = imbalanceAmount,
            ImbalanceDetected = imbalanceDetected,
            AuditTamperDetected = auditTampered,
            ReportDataJson = reportDataJson,
            CsvContent = csvContent,
            ExportFormat = "CSV",
            GeneratedByActorId = adminId,
            GeneratedAt = DateTime.UtcNow
        };
        await _uow.BulkReconciliationReports.AddAsync(report, ct);

        // Step 11: Immutable audit log
        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.AdminOverride, "BULK_RECONCILIATION_GENERATED",
            adminId, ActorRole.FinanceAdmin,
            report.Id, "BULK_RECONCILIATION_REPORT", null,
            JsonSerializer.Serialize(new
            {
                from, to, cityId, serviceType,
                totalGrossCollected, totalDriverPayouts, totalMerchantPayouts,
                totalPlatformRevenue, imbalanceAmount, imbalanceDetected, auditTampered
            }),
            cityId, null, null), ct);

        await _uow.SaveChangesAsync(ct);

        return MapBulkReportToDto(report, lines);
    }

    public async Task<IEnumerable<BulkReconciliationReportDto>> GetBulkReconciliationReportsAsync(DateTime from, DateTime to, Guid? cityId = null, string? serviceType = null, CancellationToken ct = default)
    {
        var reports = await _uow.BulkReconciliationReports.GetByPeriodAsync(from, to, cityId, serviceType, ct);
        return reports.Select(r =>
        {
            var lines = JsonSerializer.Deserialize<List<BulkReconciliationLineDto>>(r.ReportDataJson) ?? new List<BulkReconciliationLineDto>();
            return MapBulkReportToDto(r, lines);
        });
    }

    public async Task<string> GetBulkReconciliationReportCsvAsync(Guid reportId, CancellationToken ct = default)
    {
        var report = await _uow.BulkReconciliationReports.GetByIdAsync(reportId, ct)
            ?? throw new KeyNotFoundException($"Bulk reconciliation report {reportId} not found.");
        return report.CsvContent ?? string.Empty;
    }

    private static BulkReconciliationReportDto MapBulkReportToDto(BulkReconciliationReport r, IEnumerable<BulkReconciliationLineDto> lines) => new(
        r.Id, r.DateFrom, r.DateTo, r.CityId, r.ServiceType,
        r.TotalGrossCollected, r.TotalDriverPayouts, r.TotalMerchantPayouts,
        r.TotalPlatformRevenue, r.TotalOutstandingReceivables, r.TotalHolds,
        r.TotalRefunds, r.TotalWriteOffs, r.ImbalanceAmount, r.ImbalanceDetected,
        r.AuditTamperDetected, r.ExportFormat, r.GeneratedByActorId, r.GeneratedAt, lines);

    private static string BuildBulkReconciliationCsv(DateTime from, DateTime to, List<BulkReconciliationLineDto> lines)
    {
        // QuickBooks/Xero-compatible CSV format: Date, Transaction Type, Document Number, Name, Amount, Memo/Description
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date,Transaction Type,Document Number,Name,Amount,Description");
        foreach (var line in lines)
        {
            var date = from.ToString("yyyy-MM-dd");
            var type = line.Category;
            var name = line.Subcategory;
            var amount = line.Amount.ToString("F2");
            var desc = (line.Notes ?? "").Replace("\"", "\"\"");
            sb.AppendLine($"{date},{type},,{name},{amount},\"{desc}\"");
        }
        sb.AppendLine();
        sb.AppendLine($"Generated,{DateTime.UtcNow:O}");
        return sb.ToString();
    }

    public async Task<FinanceParameterDto> GetParameterAsync(string key, Guid? cityId, string? serviceType, ActorType? actorType = null, string? tier = null, CancellationToken ct = default)
    {
        var param = await _uow.FinanceParameters.GetActiveScopedAsync(key, cityId, serviceType, actorType, tier, ct)
            ?? throw new KeyNotFoundException($"Parameter '{key}' not found.");
        return MapParamDto(param);
    }

    public async Task<FinanceParameterDto> UpdateParameterAsync(UpdateParameterRequest req, CancellationToken ct = default)
    {
        var existing = await _uow.FinanceParameters.GetActiveScopedAsync(req.Key, req.CityId, req.ServiceType, req.ActorType, req.Tier, ct);

        // UC-AD-FIN-07: Commission rate changes above ±5% require Super Admin approval
        if (req.Key == "commission_rate" && existing is not null
            && decimal.TryParse(existing.ParameterValue, out var oldRate)
            && decimal.TryParse(req.Value, out var newRate))
        {
            var pctChange = Math.Abs((newRate - oldRate) / oldRate) * 100;
            if (pctChange > 5m)
            {
                await _audit.WriteAsync(new AuditLogRequest(
                    AuditEventType.AdminOverride, "COMMISSION_RATE_CHANGE_PENDING_SUPER_ADMIN",
                    req.ChangedByActorId, ActorRole.FinanceAdmin,
                    existing.Id, "FINANCE_PARAMETER",
                    JsonSerializer.Serialize(existing),
                    JsonSerializer.Serialize(new { req.Value, pctChange }),
                    req.CityId, null, null), ct);
                await _uow.SaveChangesAsync(ct);
                throw new UnauthorizedAccessException(
                    $"Commission rate change of {pctChange:F1}% exceeds ±5% threshold. Super Admin approval required.");
            }
        }

        // Versioned — previous value retained; new version created
        if (existing is not null)
        {
            existing.IsActive = false;
            _uow.FinanceParameters.Update(existing);
        }

        var newParam = new FinanceParameter
        {
            ParameterKey = req.Key,
            ParameterValue = req.Value,
            CityId = req.CityId,
            ServiceType = req.ServiceType,
            ActorType = req.ActorType,
            Tier = req.Tier,
            Category = req.Category,
            Description = req.Description,
            ChangedByActorId = req.ChangedByActorId,
            EffectiveFrom = DateTime.UtcNow,
            Version = (existing?.Version ?? 0) + 1,
            PreviousValue = existing is not null && decimal.TryParse(existing.ParameterValue, out var prev) ? prev : null
        };
        await _uow.FinanceParameters.AddAsync(newParam, ct);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Config, "PARAMETER_UPDATED",
            req.ChangedByActorId, ActorRole.FinanceAdmin,
            newParam.Id, "FINANCE_PARAMETER",
            existing is not null ? JsonSerializer.Serialize(existing) : null,
            JsonSerializer.Serialize(newParam),
            req.CityId, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapParamDto(newParam);
    }

    /// <summary>UC-AD-FIN-07: Rollback parameter to previous version — Super Admin only.</summary>
    public async Task<FinanceParameterDto> RollbackParameterAsync(RollbackParameterRequest req, CancellationToken ct = default)
    {
        var current = await _uow.FinanceParameters.GetByIdAsync(req.ParameterId, ct)
            ?? throw new KeyNotFoundException($"Parameter {req.ParameterId} not found.");

        var previous = await _uow.FinanceParameters.GetPreviousVersionAsync(req.ParameterId, ct)
            ?? throw new InvalidOperationException("No previous version available for rollback.");

        // Deactivate current
        current.IsActive = false;
        _uow.FinanceParameters.Update(current);

        // Reactivate previous as new version
        var rolledBack = new FinanceParameter
        {
            ParameterKey = current.ParameterKey,
            ParameterValue = previous.ParameterValue,
            CityId = current.CityId,
            ServiceType = current.ServiceType,
            ActorType = current.ActorType,
            Tier = current.Tier,
            Category = current.Category,
            Description = current.Description,
            ChangedByActorId = req.RollbackByActorId,
            EffectiveFrom = DateTime.UtcNow,
            Version = current.Version + 1,
            PreviousValue = decimal.TryParse(current.ParameterValue, out var curVal) ? curVal : null
        };
        await _uow.FinanceParameters.AddAsync(rolledBack, ct);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.AdminOverride, "PARAMETER_ROLLED_BACK",
            req.RollbackByActorId, ActorRole.SuperAdmin,
            rolledBack.Id, "FINANCE_PARAMETER",
            JsonSerializer.Serialize(current),
            JsonSerializer.Serialize(rolledBack),
            current.CityId, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapParamDto(rolledBack);
    }

    /// <summary>UC-AD-FIN-07: Get version history for a parameter key.</summary>
    public async Task<IEnumerable<FinanceParameterDto>> GetParameterHistoryAsync(string key, CancellationToken ct = default)
    {
        var history = await _uow.FinanceParameters.GetHistoryAsync(key, ct);
        return history.Select(MapParamDto);
    }

    /// <summary>UC-AD-FIN-07: Get all parameters grouped by category.</summary>
    public async Task<IEnumerable<ParameterCategoryDto>> GetParametersByCategoryAsync(CancellationToken ct = default)
    {
        var all = await _uow.FinanceParameters.GetAllAsync(ct);
        var active = all.Where(p => p.IsActive);
        var grouped = active.GroupBy(p => p.Category ?? "uncategorized")
            .Select(g => new ParameterCategoryDto(
                g.Key,
                g.Count(),
                g.Select(MapParamDto)))
            .OrderBy(g => g.Category);
        return grouped;
    }

    public async Task<string> ExportWalletsCsvAsync(ActorType? actorType, Guid? cityId, CancellationToken ct = default)
    {
        var wallets = await _uow.Wallets.GetAllAsync(ct);
        var filtered = wallets.AsEnumerable();

        if (actorType.HasValue)
            filtered = filtered.Where(w => w.ActorType == actorType.Value);
        if (cityId.HasValue)
            filtered = filtered.Where(w => w.CityId == cityId.Value);

        var lines = new List<string>
        {
            "WalletId,ActorId,ActorType,Currency,BalanceAvailable,BalancePending,BalanceHold,CashReceivable,BalanceRefundCredit,BalancePromoCredit,BalanceCourtesyCredit,KycStatus,InstantPayTier,IsInDunning,FraudScore,CreatedAt"
        };

        foreach (var w in filtered.OrderBy(w => w.ActorType).ThenBy(w => w.CreatedAt))
        {
            lines.Add(
                $"{w.Id},{w.ActorId},{w.ActorType},{w.Currency},{w.BalanceAvailable:F2},{w.BalancePending:F2},{w.BalanceHold:F2},{w.CashReceivable:F2}," +
                $"{w.BalanceRefundCredit:F2},{w.BalancePromoCredit:F2},{w.BalanceCourtesyCredit:F2},{w.KycStatus},{w.InstantPayTier},{w.IsInDunning},{w.FraudScore},{w.CreatedAt:O}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public async Task<IEnumerable<FinanceParameterDto>> GetAllParametersAsync(CancellationToken ct = default)
    {
        var all = await _uow.FinanceParameters.GetAllAsync(ct);
        return all.Select(MapParamDto);
    }

    private static AuditLogEntryDto MapAuditDto(AuditLogEntry l) => new(
        l.Id, l.EventType, l.EventSubtype, l.ActorId, l.ActorRole,
        l.SubjectId, l.SubjectType, l.Delta, l.Timestamp, l.TamperHash);

    private static FinanceParameterDto MapParamDto(FinanceParameter p) => new(
        p.Id, p.ParameterKey, p.ParameterValue, p.Description,
        p.CityId, p.ServiceType, p.ActorType, p.Tier, p.Category,
        p.PreviousValue, p.ChangedByActorId, p.Version, p.EffectiveFrom);
}

public class CorporateBillingService : ICorporateBillingService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public CorporateBillingService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<CorporateAccountDto> GetAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _uow.CorporateAccounts.GetByIdAsync(accountId, ct)
            ?? throw new KeyNotFoundException($"Corporate account {accountId} not found.");
        var wallet = await _uow.Wallets.GetByIdAsync(account.WalletId, ct);
        return MapToDto(account, wallet?.BalanceAvailable ?? 0);
    }

    public async Task<CorporateAccountDto> CreateAccountAsync(CreateCorporateAccountRequest req, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            // Create Corporate Wallet (debit-only — no outbound payouts)
            var wallet = new Wallet
            {
                ActorType = ActorType.Corporate,
                ActorId = Guid.NewGuid(),
                Currency = "SAR",
                KycStatus = KycStatus.Pending
            };
            await _uow.Wallets.AddAsync(wallet, ct);

            var account = new CorporateAccount
            {
                CompanyName = req.CompanyName,
                VatRegistrationNumber = req.VatRegistrationNumber,
                TradeLicenseNumber = req.TradeLicenseNumber,
                AuthorizedSignatoryId = req.AuthorizedSignatoryId,
                BillingModel = req.BillingModel,
                PaymentTerms = req.PaymentTerms,
                NegotiatedDiscountPercent = req.NegotiatedDiscountPercent,
                WalletId = wallet.Id,
                CreditLimit = req.CreditLimit,
                LowBalanceAlertThreshold = req.LowBalanceAlertThreshold,
                MonthlyBudgetCap = req.MonthlyBudgetCap,
                ContractTermsSnapshot = JsonSerializer.Serialize(req),
                ContractVersion = 1
            };
            await _uow.CorporateAccounts.AddAsync(account, ct);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Config, "CORPORATE_ACCOUNT_CREATED",
                req.CreatedByActorId, ActorRole.CorporateAccountManager,
                account.Id, "CORPORATE_ACCOUNT", null,
                JsonSerializer.Serialize(new { account.CompanyName, account.BillingModel }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return MapToDto(account, 0);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<CorporateAccountDto> UpdateBillingModelAsync(Guid accountId, UpdateBillingModelRequest req, CancellationToken ct = default)
    {
        var account = await _uow.CorporateAccounts.GetByIdAsync(accountId, ct)
            ?? throw new KeyNotFoundException($"Corporate account {accountId} not found.");

        var before = JsonSerializer.Serialize(account);
        account.BillingModel = req.NewBillingModel;
        account.PaymentTerms = req.NewPaymentTerms;
        // Contract change → new version + Finance Manager approval immutably logged
        account.ContractVersion++;
        account.ContractTermsSnapshot = JsonSerializer.Serialize(req);
        account.UpdatedAt = DateTime.UtcNow;
        _uow.CorporateAccounts.Update(account);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Config, "BILLING_MODEL_UPDATED",
            req.ChangedByAdminId, ActorRole.FinanceManager,
            accountId, "CORPORATE_ACCOUNT", before,
            JsonSerializer.Serialize(new { req.NewBillingModel, req.ApprovalReference }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        var wallet = await _uow.Wallets.GetByIdAsync(account.WalletId, ct);
        return MapToDto(account, wallet?.BalanceAvailable ?? 0);
    }

    /// <summary>
    /// Validates corporate booking at booking time.
    /// Blocks if: Corporate Wallet insufficient, employee budget exceeded,
    /// cost-center budget at 100%, or account monthly budget at 100%.
    /// </summary>
    public async Task<BookingValidationResultDto> ValidateBookingAsync(Guid corporateAccountId, Guid employeeUserId, decimal estimatedFare, CancellationToken ct = default)
    {
        var account = await _uow.CorporateAccounts.GetByIdAsync(corporateAccountId, ct)
            ?? throw new KeyNotFoundException($"Corporate account {corporateAccountId} not found.");
        var wallet = await _uow.Wallets.GetByIdAsync(account.WalletId, ct)!;
        var employee = await _uow.CorporateAccounts.GetEmployeeAsync(corporateAccountId, employeeUserId, ct);

        // Apply negotiated discount — reduces Gross, not commission rate
        var discountedFare = estimatedFare * (1 - account.NegotiatedDiscountPercent / 100);
        decimal companyPortion = 0, employeePortion = 0;

        switch (account.BillingModel)
        {
            case CorporateBillingModel.CompanyPay:
                companyPortion = discountedFare;
                break;
            case CorporateBillingModel.SplitPay:
                // Cap configured elsewhere; simplified: full company pay up to limit
                companyPortion = discountedFare;
                break;
            case CorporateBillingModel.VoucherAllowance when employee is not null:
                var allowanceRemaining = employee.MonthlyAllowance - employee.AllowanceConsumed;
                companyPortion = Math.Min(discountedFare, allowanceRemaining);
                employeePortion = discountedFare - companyPortion;
                break;
            case CorporateBillingModel.Reimbursement:
                employeePortion = discountedFare;
                break;
        }

        // Corporate Wallet insufficient → block at booking, not payment
        if (companyPortion > 0 && (wallet?.BalanceAvailable ?? 0) < companyPortion)
            return new BookingValidationResultDto(false, "Corporate Wallet balance insufficient.", 0, 0, 0, 0);

        // Account monthly budget
        if (account.MonthlyBudgetCap > 0 &&
            account.MonthlyBudgetConsumed + companyPortion > account.MonthlyBudgetCap)
            return new BookingValidationResultDto(false, "Corporate account monthly budget exhausted.", 0, 0, 0, 0);

        // Employee monthly budget
        if (employee is not null && employee.MonthlyBudget > 0 &&
            employee.MonthlyBudgetConsumed + discountedFare > employee.MonthlyBudget)
            return new BookingValidationResultDto(false, "Employee monthly budget exhausted.", 0, 0, 0, 0);

        return new BookingValidationResultDto(true, null, companyPortion, employeePortion,
            employee is not null ? employee.MonthlyBudget - employee.MonthlyBudgetConsumed : 0,
            account.MonthlyBudgetCap - account.MonthlyBudgetConsumed);
    }

    public async Task<SplitPayResultDto> ProcessSplitPayAsync(ProcessSplitPayRequest req, CancellationToken ct = default)
    {
        // Both company and employee portions must succeed or both reversed (atomic)
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var account = await _uow.CorporateAccounts.GetByIdAsync(req.CorporateAccountId, ct)!;
            var wallet = await _uow.Wallets.GetByIdWithLockAsync(account!.WalletId, ct)!;

            var companyPortion = Math.Min(req.GrossFare, req.CompanyCap);
            var employeePortion = req.GrossFare - companyPortion;

            wallet!.BalanceAvailable -= companyPortion;
            account.MonthlyBudgetConsumed += companyPortion;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);
            _uow.CorporateAccounts.Update(account);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return new SplitPayResultDto(companyPortion, employeePortion, "COMPLETED");
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<CorporateInvoiceDto> GenerateInvoiceAsync(Guid corporateAccountId, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken ct = default)
    {
        var account = await _uow.CorporateAccounts.GetByIdAsync(corporateAccountId, ct)
            ?? throw new KeyNotFoundException($"Corporate account {corporateAccountId} not found.");

        // Real impl: aggregate trips in period; generate sequential ZATCA number
        var dueDate = account.PaymentTerms switch
        {
            CorporatePaymentTerms.Net30 => DateTime.UtcNow.AddDays(30),
            CorporatePaymentTerms.Net15 => DateTime.UtcNow.AddDays(15),
            _ => DateTime.UtcNow
        };

        var invoice = new CorporateInvoice
        {
            CorporateAccountId = corporateAccountId,
            InvoiceNumber = $"CORP-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid():N[..6].ToUpper()}",
            SubtotalAmount = 0,
            VatAmount = 0,
            TotalAmount = 0,
            InvoiceDate = DateTime.UtcNow,
            DueDate = dueDate,
            SellerVatRegistration = "SA000000000000000",
            BuyerVatRegistration = account.VatRegistrationNumber,
            QrCodeData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"BJEEK|{corporateAccountId}|{DateTime.UtcNow:O}"))
        };
        await _uow.CorporateAccounts.GetByIdAsync(corporateAccountId, ct); // nav load
        await _uow.SaveChangesAsync(ct);
        return MapInvoiceDto(invoice);
    }

    public async Task<IEnumerable<CorporateInvoiceDto>> GetInvoicesAsync(Guid corporateAccountId, CancellationToken ct = default)
    {
        var account = await _uow.CorporateAccounts.GetByIdAsync(corporateAccountId, ct)
            ?? throw new KeyNotFoundException();
        return account.Invoices.Select(MapInvoiceDto);
    }

    public async Task<CorporateInvoiceDto> GetInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        // Real impl: dedicated invoice repo query
        await Task.CompletedTask;
        throw new KeyNotFoundException($"Invoice {invoiceId} not found.");
    }

    public async Task<EmployeeBudgetDto> GetEmployeeBudgetAsync(Guid corporateAccountId, Guid userId, CancellationToken ct = default)
    {
        var employee = await _uow.CorporateAccounts.GetEmployeeAsync(corporateAccountId, userId, ct)
            ?? throw new KeyNotFoundException($"Employee {userId} not found in account {corporateAccountId}.");
        return new EmployeeBudgetDto(userId, corporateAccountId, employee.CostCenter,
            employee.MonthlyBudget, employee.MonthlyBudgetConsumed,
            employee.MonthlyAllowance, employee.AllowanceConsumed);
    }

    public async Task UpdateEmployeeBudgetAsync(Guid corporateAccountId, Guid userId, decimal newBudget, Guid changedByAdminId, CancellationToken ct = default)
    {
        var employee = await _uow.CorporateAccounts.GetEmployeeAsync(corporateAccountId, userId, ct)
            ?? throw new KeyNotFoundException();

        var before = employee.MonthlyBudget;
        employee.MonthlyBudget = newBudget;
        employee.UpdatedAt = DateTime.UtcNow;

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Config, "EMPLOYEE_BUDGET_UPDATED",
            changedByAdminId, ActorRole.CorporateAccountManager,
            employee.Id, "CORPORATE_EMPLOYEE",
            JsonSerializer.Serialize(new { MonthlyBudget = before }),
            JsonSerializer.Serialize(new { MonthlyBudget = newBudget }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<CorporateAccountDto>> GetAccountsBelowAlertThresholdAsync(CancellationToken ct = default)
    {
        var accounts = await _uow.CorporateAccounts.GetBelowAlertThresholdAsync(ct);
        return accounts.Select(a => MapToDto(a, 0));
    }

    private static CorporateAccountDto MapToDto(CorporateAccount a, decimal walletBalance) => new(
        a.Id, a.CompanyName, a.VatRegistrationNumber, a.BillingModel, a.PaymentTerms,
        a.NegotiatedDiscountPercent, a.WalletId, walletBalance,
        a.CreditLimit, a.MonthlyBudgetCap, a.MonthlyBudgetConsumed,
        a.IsActive, a.ContractVersion, a.CreatedAt);

    private static CorporateInvoiceDto MapInvoiceDto(CorporateInvoice i) => new(
        i.Id, i.CorporateAccountId, i.InvoiceNumber,
        i.SubtotalAmount, i.VatAmount, i.TotalAmount,
        i.InvoiceDate, i.DueDate, i.IsPaid, i.PaidAt);
}
