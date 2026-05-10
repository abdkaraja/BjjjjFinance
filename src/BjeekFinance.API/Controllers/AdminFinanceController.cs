using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/admin/finance")]
[Authorize(Policy = "FinanceAdmin")]
[Produces("application/json")]
public class AdminFinanceController : ControllerBase
{
    private readonly IAdminFinanceService _admin;
    private readonly IVatReportService _vat;

    public AdminFinanceController(IAdminFinanceService admin, IVatReportService vat)
    {
        _admin = admin;
        _vat = vat;
    }

    // ── Dunning ────────────────────────────────────────────────────────────────

    /// <summary>Get dunning status for a wallet.</summary>
    [HttpGet("dunning/{walletId:guid}")]
    [ProducesResponseType(typeof(DunningStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDunningStatus(Guid walletId, CancellationToken ct)
    {
        var result = await _admin.GetDunningStatusAsync(walletId, ct);
        return Ok(result);
    }

    /// <summary>Get all wallets currently in dunning.</summary>
    [HttpGet("dunning")]
    [ProducesResponseType(typeof(IEnumerable<DunningStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllDunning(CancellationToken ct)
    {
        var result = await _admin.GetAllDunningWalletsAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Trigger nightly dunning classification batch.
    /// Normally invoked by scheduler at 02:00 local time — exposed for manual override.
    /// </summary>
    [HttpPost("dunning/run-batch")]
    [Authorize(Policy = "FinanceManager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RunDunningBatch(CancellationToken ct)
    {
        await _admin.RunDunningBatchAsync(ct);
        return NoContent();
    }

    // ── Write-Offs ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Initiate a debt write-off.
    /// &lt; SAR 18,500: Finance Manager self-approves.
    /// ≥ SAR 18,500: VP Finance co-approval required.
    /// ReasonCode = Other requires Notes ≥ 100 characters.
    /// </summary>
    [HttpPost("write-offs")]
    [Authorize(Policy = "FinanceManager")]
    [ProducesResponseType(typeof(WriteOffResultDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateWriteOff([FromBody] WriteOffRequest req, CancellationToken ct)
    {
        var result = await _admin.InitiateWriteOffAsync(req, ct);
        return Accepted(result);
    }

    /// <summary>VP Finance: approve a write-off ≥ SAR 18,500.</summary>
    [HttpPost("write-offs/{writeOffId:guid}/approve")]
    [Authorize(Policy = "VpFinance")]
    [ProducesResponseType(typeof(WriteOffResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveWriteOff(Guid writeOffId, CancellationToken ct)
    {
        var approverActorId = GetActorId();
        var result = await _admin.ApproveWriteOffAsync(writeOffId, approverActorId, ct);
        return Ok(result);
    }

    // ── Bulk Adjustments ───────────────────────────────────────────────────────

    /// <summary>
    /// Execute bulk wallet adjustments.
    /// &gt; SAR 50,000 total requires Super Admin — enforced via RBAC.
    /// Full audit log written atomically with adjustments.
    /// </summary>
    [HttpPost("bulk-adjustments")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(BulkAdjustmentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkAdjust([FromBody] BulkAdjustmentRequest req, CancellationToken ct)
    {
        var result = await _admin.ExecuteBulkAdjustmentAsync(req, ct);
        return Ok(result);
    }

    // ── Audit Logs ─────────────────────────────────────────────────────────────

    /// <summary>Query audit logs for a subject entity.</summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(IEnumerable<AuditLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid subjectId,
        [FromQuery] string subjectType,
        CancellationToken ct)
    {
        var result = await _admin.GetAuditLogsAsync(subjectId, subjectType, ct);
        return Ok(result);
    }

    /// <summary>Query audit logs by event type and date range.</summary>
    [HttpGet("audit-logs/by-event")]
    [ProducesResponseType(typeof(IEnumerable<AuditLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogsByEvent(
        [FromQuery] AuditEventType eventType,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        var result = await _admin.GetAuditLogsByEventTypeAsync(eventType, from, to, ct);
        return Ok(result);
    }

    // ── Reconciliation ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generate financial reconciliation report.
    /// Target: &lt; 30 seconds for 30-day range.
    /// </summary>
    [HttpGet("reconciliation")]
    [Authorize(Policy = "FinanceManager")]
    [ProducesResponseType(typeof(ReconciliationReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReconciliation(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        var result = await _admin.GenerateReconciliationReportAsync(from, to, ct);
        return Ok(result);
    }

    // ── Wallet Export ───────────────────────────────────────────────────────────

    /// <summary>
    /// UC-AD-FIN-01: Export wallet data as CSV for selected actor type and city.
    /// </summary>
    [HttpGet("wallets/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportWallets(
        [FromQuery] ActorType? actorType,
        [FromQuery] Guid? cityId,
        CancellationToken ct)
    {
        var csv = await _admin.ExportWalletsCsvAsync(actorType, cityId, ct);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"wallets-export-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ── UC-AD-FIN-04: VAT Reports ───────────────────────────────────────────────

    /// <summary>
    /// UC-AD-FIN-04: Generate a ZATCA-compliant VAT report for the given period.
    /// Optional merchant actor and service type filters.
    /// Instant Pay fee VAT reported separately as platform service revenue.
    /// Missing tax config (VAT = 0 on taxable gross) flagged in report.
    /// CSV export with all ZATCA-required fields.
    /// </summary>
    [HttpPost("vat-reports")]
    [Authorize(Policy = "FinanceManager")]
    [ProducesResponseType(typeof(VatReportDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> GenerateVatReport(
        [FromQuery] DateTime periodStart,
        [FromQuery] DateTime periodEnd,
        [FromQuery] Guid? merchantActorId,
        [FromQuery] string? serviceType,
        CancellationToken ct)
    {
        var adminId = GetActorId();
        var result = await _vat.GenerateVatReportAsync(periodStart, periodEnd, merchantActorId, serviceType, ct);
        return CreatedAtAction(nameof(GetVatReports), new { periodStart, periodEnd }, result);
    }

    /// <summary>List previously generated VAT reports by period and optional merchant.</summary>
    [HttpGet("vat-reports")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<VatReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVatReports(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? merchantActorId,
        CancellationToken ct)
    {
        var result = await _vat.GetVatReportsAsync(from, to, merchantActorId, ct);
        return Ok(result);
    }

    /// <summary>Download VAT report CSV by report ID.</summary>
    [HttpGet("vat-reports/{reportId:guid}/csv")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadVatReportCsv(Guid reportId, CancellationToken ct)
    {
        var csv = await _vat.GetVatReportCsvAsync(reportId, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"vat-report-{reportId:N[..8]}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ── UC-AD-FIN-06: Bulk Platform Reconciliation ──────────────────────────────

    /// <summary>
    /// UC-AD-FIN-06: Generate bulk platform reconciliation report.
    /// Verifies: Total Gross Collected = Driver Payouts + Merchant Payouts + Platform Revenue + Outstanding Receivables + Holds.
    /// Imbalance > SAR 1 triggers alert. Tamper check on audit logs.
    /// CSV export in QuickBooks/Xero-compatible format.
    /// </summary>
    [HttpPost("bulk-reconciliation")]
    [Authorize(Policy = "FinanceManager")]
    [ProducesResponseType(typeof(BulkReconciliationReportDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> GenerateBulkReconciliation(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? cityId,
        [FromQuery] string? serviceType,
        CancellationToken ct)
    {
        var adminId = GetActorId();
        var result = await _admin.GenerateBulkReconciliationReportAsync(from, to, cityId, serviceType, adminId, ct);
        return CreatedAtAction(nameof(GetBulkReconciliationReports), new { from, to }, result);
    }

    /// <summary>List previously generated bulk reconciliation reports by period.</summary>
    [HttpGet("bulk-reconciliation")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<BulkReconciliationReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBulkReconciliationReports(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? cityId,
        [FromQuery] string? serviceType,
        CancellationToken ct)
    {
        var result = await _admin.GetBulkReconciliationReportsAsync(from, to, cityId, serviceType, ct);
        return Ok(result);
    }

    /// <summary>Download bulk reconciliation report CSV by report ID.</summary>
    [HttpGet("bulk-reconciliation/{reportId:guid}/csv")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadBulkReconciliationCsv(Guid reportId, CancellationToken ct)
    {
        var csv = await _admin.GetBulkReconciliationReportCsvAsync(reportId, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"bulk-reconciliation-{reportId:N[..8]}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ── UC-AD-FIN-07: Finance Parameters ────────────────────────────────────────

    /// <summary>
    /// Get all finance parameters.
    /// All financial thresholds are admin-configurable and versioned — never hardcoded.
    /// </summary>
    [HttpGet("parameters")]
    [ProducesResponseType(typeof(IEnumerable<FinanceParameterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetParameters(CancellationToken ct)
    {
        var result = await _admin.GetAllParametersAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-AD-FIN-07: Get parameters grouped by category.
    /// </summary>
    [HttpGet("parameters/by-category")]
    [ProducesResponseType(typeof(IEnumerable<ParameterCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetParametersByCategory(CancellationToken ct)
    {
        var result = await _admin.GetParametersByCategoryAsync(ct);
        return Ok(result);
    }

    /// <summary>Get a specific finance parameter with optional city, service-type, actor-type, and tier scope.</summary>
    [HttpGet("parameters/{key}")]
    [ProducesResponseType(typeof(FinanceParameterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParameter(
        string key,
        [FromQuery] Guid? cityId,
        [FromQuery] string? serviceType,
        [FromQuery] ActorType? actorType,
        [FromQuery] string? tier,
        CancellationToken ct)
    {
        var result = await _admin.GetParameterAsync(key, cityId, serviceType, actorType, tier, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-AD-FIN-07: Get version history for a parameter key.
    /// </summary>
    [HttpGet("parameters/{key}/history")]
    [ProducesResponseType(typeof(IEnumerable<FinanceParameterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetParameterHistory(string key, CancellationToken ct)
    {
        var result = await _admin.GetParameterHistoryAsync(key, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-AD-FIN-07: Update a finance parameter.
    /// Previous value retained as versioned history.
    /// Finance Admin level required; change immutably logged.
    /// Commission rate changes above ±5% require Super Admin approval.
    /// Supports scoping by City, ServiceType, ActorType, and Instant Pay Tier.
    /// Effective date can be future-dated (scheduled activation via EffectiveFrom).
    /// </summary>
    [HttpPut("parameters")]
    [ProducesResponseType(typeof(FinanceParameterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateParameter([FromBody] UpdateParameterRequest req, CancellationToken ct)
    {
        var result = await _admin.UpdateParameterAsync(req, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-AD-FIN-07: Rollback a parameter to its previous version — Super Admin only.
    /// </summary>
    [HttpPost("parameters/{parameterId:guid}/rollback")]
    [Authorize(Policy = "SuperAdmin")]
    [ProducesResponseType(typeof(FinanceParameterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RollbackParameter(Guid parameterId, CancellationToken ct)
    {
        var adminId = GetActorId();
        var request = new RollbackParameterRequest(parameterId, adminId);
        var result = await _admin.RollbackParameterAsync(request, ct);
        return Ok(result);
    }

    // ── Chargebacks ────────────────────────────────────────────────────────────

    /// <summary>
    /// Process a chargeback/dispute reversal.
    /// Reduces PENDING first if still unsettled, otherwise reduces AVAILABLE.
    /// If AVAILABLE insufficient, remainder becomes dunning receivable.
    /// </summary>
    [HttpPost("chargebacks")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcessChargeback([FromBody] ChargebackRequest req, CancellationToken ct)
    {
        var walletService = HttpContext.RequestServices.GetRequiredService<IWalletService>();
        await walletService.ProcessChargebackAsync(req.WalletId, req.Amount, req.TransactionId, ct);
        return NoContent();
    }

    private Guid GetActorId() =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}

public record ChargebackRequest(Guid WalletId, decimal Amount, Guid TransactionId);
