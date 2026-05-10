using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using System.Text;
using System.Text.Json;

namespace BjeekFinance.Application.Services;

/// <summary>
/// UC-AD-FIN-04: ZATCA-compliant VAT report generation.
/// Aggregates platform rides, orders, Instant Pay fee VAT.
/// Merchant-specific and service-type filters available.
/// Missing tax config (VAT=0 on taxable transaction) flagged.
/// CSV export with all ZATCA-required fields. Report stored immutably.
/// </summary>
public class VatReportService : IVatReportService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public VatReportService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<VatReportDto> GenerateVatReportAsync(DateTime periodStart, DateTime periodEnd, Guid? merchantActorId = null, string? serviceType = null, CancellationToken ct = default)
    {
        // ── 1. Fetch transactions with wallet data ─────────────────────────────
        var transactions = await _uow.Transactions.GetByDateRangeWithWalletAsync(periodStart, periodEnd, serviceType, ct);

        // ── 2. Fetch Instant Pay cashouts for fee VAT ──────────────────────────
        var cashouts = await _uow.InstantPay.GetByDateRangeAsync(periodStart, periodEnd, ct);

        // ── 3. Build report lines ──────────────────────────────────────────────
        var lines = new List<VatReportLineDto>();
        var flaggedCount = 0;

        foreach (var txn in transactions)
        {
            // Skip if merchant filter is active and doesn't match
            if (merchantActorId.HasValue && txn.Wallet.ActorId != merchantActorId.Value)
                continue;

            // Derive service type
            var svcType = txn.RideId.HasValue ? "ride"
                : txn.OrderId.HasValue ? "delivery"
                : "other";

            // Determine vat rate: if VatAmount > 0, compute rate; otherwise 0
            var vatRate = txn.GrossAmount > 0
                ? Math.Round(txn.VatAmount / (txn.GrossAmount - txn.VatAmount) * 100, 2)
                : 0m;

            // Flag transactions with VAT = 0 but Gross > 0 (missing tax config)
            var missingConfig = txn.VatAmount == 0 && txn.GrossAmount > 0;
            if (missingConfig) flaggedCount++;

            lines.Add(new VatReportLineDto(
                txn.InvoiceId ?? $"MISSING-{txn.Id:N}",
                txn.CreatedAt,
                txn.Wallet.ActorId,
                txn.Wallet.ActorType.ToString(),
                txn.GrossAmount,
                vatRate,
                txn.VatAmount,
                txn.NetAmount,
                svcType,
                txn.Wallet.ActorType == ActorType.Merchant ? txn.Wallet.ActorId.ToString() : null,
                missingConfig
            ));
        }

        // ── 4. Add Instant Pay fee VAT lines ──────────────────────────────────
        decimal instantPayFeeVatTotal = 0;
        foreach (var co in cashouts.Where(c => c.VatOnFee > 0))
        {
            // Instant Pay fee VAT is platform liability — separate lines
            instantPayFeeVatTotal += co.VatOnFee;

            lines.Add(new VatReportLineDto(
                co.MicroInvoiceId ?? $"MIC-{co.Id:N}",
                co.CreatedAt,
                co.ActorId,
                ActorType.Driver.ToString(),
                co.FeeAmount + co.VatOnFee, // gross = fee + vat
                15m,
                co.VatOnFee,
                co.FeeAmount,
                "instant_pay_fee",
                null,
                false
            ));
        }

        // ── 5. Compute totals ─────────────────────────────────────────────────
        var totalGross = lines.Sum(l => l.GrossAmount);
        var totalVat = lines.Sum(l => l.VatAmount);
        var totalNet = lines.Sum(l => l.NetAmount);

        // ── 6. Build CSV ──────────────────────────────────────────────────────
        var csv = new StringBuilder();
        csv.AppendLine("invoice_id,transaction_date,actor_id,actor_type,gross_amount,vat_rate_pct,vat_amount,net_amount,service_type,merchant_id,missing_tax_config");
        foreach (var l in lines)
        {
            csv.AppendLine(
                $"{l.InvoiceId},{l.TransactionDate:O},{l.ActorId},{l.ActorType}," +
                $"{l.GrossAmount:F2},{l.VatRate:F2},{l.VatAmount:F2},{l.NetAmount:F2}," +
                $"{l.ServiceType},{l.MerchantId ?? ""},{l.MissingTaxConfig}");
        }

        // Add summary section
        csv.AppendLine();
        csv.AppendLine("ZATCA VAT REPORT SUMMARY");
        csv.AppendLine($"Period,{periodStart:O},{periodEnd:O}");
        csv.AppendLine($"Merchant Filter,{merchantActorId?.ToString() ?? "ALL"}");
        csv.AppendLine($"Service Type,{serviceType ?? "ALL"}");
        csv.AppendLine($"Total Gross,{totalGross:F2}");
        csv.AppendLine($"Total VAT,{totalVat:F2}");
        csv.AppendLine($"Total Net,{totalNet:F2}");
        csv.AppendLine($"Instant Pay Fee VAT (Platform Liability),{instantPayFeeVatTotal:F2}");
        csv.AppendLine($"Flagged Missing Tax Config,{flaggedCount}");
        csv.AppendLine($"Generated At,{DateTime.UtcNow:O}");

        // ── 7. Store report ───────────────────────────────────────────────────
        var report = new VatReport
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            MerchantActorId = merchantActorId,
            ServiceType = serviceType,
            TotalGross = totalGross,
            TotalVat = totalVat,
            TotalNet = totalNet,
            InstantPayFeeVat = instantPayFeeVatTotal,
            FlaggedMissingConfigCount = flaggedCount,
            ReportDataJson = JsonSerializer.Serialize(lines),
            CsvContent = csv.ToString(),
            ExportFormat = "CSV",
            GeneratedByActorId = Guid.Empty // Set by controller from auth context
        };
        await _uow.VatReports.AddAsync(report, ct);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Vat, "VAT_REPORT_GENERATED",
            Guid.Empty, ActorRole.FinanceAdmin,
            report.Id, "VAT_REPORT", null,
            JsonSerializer.Serialize(new
            {
                periodStart, periodEnd, merchantActorId, serviceType,
                totalGross, totalVat, totalNet, instantPayFeeVatTotal,
                lineCount = lines.Count, flaggedCount
            }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapToDto(report);
    }

    public async Task<IEnumerable<VatReportDto>> GetVatReportsAsync(DateTime from, DateTime to, Guid? merchantActorId = null, CancellationToken ct = default)
    {
        var reports = await _uow.VatReports.GetByPeriodAsync(from, to, merchantActorId, ct);
        return reports.Select(MapToDto);
    }

    public async Task<string> GetVatReportCsvAsync(Guid reportId, CancellationToken ct = default)
    {
        var report = await _uow.VatReports.GetByIdAsync(reportId, ct)
            ?? throw new KeyNotFoundException($"VAT report {reportId} not found.");
        return report.CsvContent ?? "No CSV content available.";
    }

    private static VatReportDto MapToDto(VatReport r) => new(
        r.Id, r.PeriodStart, r.PeriodEnd, r.MerchantActorId, r.ServiceType,
        r.TotalGross, r.TotalVat, r.TotalNet, r.InstantPayFeeVat,
        r.FlaggedMissingConfigCount, r.ExportFormat,
        r.GeneratedByActorId, r.GeneratedAt);
}
