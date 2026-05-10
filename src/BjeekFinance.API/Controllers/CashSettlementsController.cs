using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/cash-settlements")]
[Authorize]
[Produces("application/json")]
public class CashSettlementsController : ControllerBase
{
    private readonly ICashSettlementService _cash;

    public CashSettlementsController(ICashSettlementService cash) => _cash = cash;

    /// <summary>
    /// UC-FIN-CASH-01: Driver submits daily cash settlement.
    /// Variance ≤ SAR 3 → auto-adjusts ledger and clears cash_receivable.
    /// Variance > SAR 3 → flagged for Finance Admin review.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "DriverOrDelivery")]
    [ProducesResponseType(typeof(CashSettlementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitSettlement([FromBody] SubmitCashSettlementRequest req, CancellationToken ct)
    {
        var result = await _cash.SubmitSettlementAsync(req, ct);
        return CreatedAtAction(nameof(GetSettlement), new { settlementId = result.SettlementId }, result);
    }

    /// <summary>
    /// Finance Admin reviews and resolves a flagged settlement.
    /// Clears cash_receivable, adjusts AVAILABLE for variance, sets status to Completed.
    /// </summary>
    [HttpPost("{settlementId:guid}/review")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(CashSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewSettlement(Guid settlementId,
        [FromBody] CashSettlementReviewRequest req, CancellationToken ct)
    {
        var adminId = GetAdminId();
        var result = await _cash.ReviewSettlementAsync(settlementId, adminId, req.ResolutionNotes, ct);
        return Ok(result);
    }

    /// <summary>Get a cash settlement by ID.</summary>
    [HttpGet("{settlementId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(CashSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSettlement(Guid settlementId, CancellationToken ct)
    {
        var result = await _cash.GetSettlementAsync(settlementId, ct);
        return Ok(result);
    }

    /// <summary>Get all cash settlements for a driver.</summary>
    [HttpGet("by-driver/{driverId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<CashSettlementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDriver(Guid driverId, CancellationToken ct)
    {
        var result = await _cash.GetByDriverAsync(driverId, ct);
        return Ok(result);
    }

    /// <summary>Get all settlements flagged for Finance Admin review.</summary>
    [HttpGet("flagged")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<CashSettlementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFlagged(CancellationToken ct)
    {
        var result = await _cash.GetFlaggedForReviewAsync(ct);
        return Ok(result);
    }

    // ── UC-AD-FIN-03: Reconciliation Dashboard ─────────────────────────────────

    /// <summary>
    /// UC-AD-FIN-03: Reconciliation dashboard with variance severity buckets.
    /// Green: ≤ SAR 3 (auto-adjusted)
    /// Yellow: SAR 3–20 (pending review)
    /// Red: > SAR 20 (flagged, shared with fraud)
    /// </summary>
    [HttpGet("reconciliation/dashboard")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(ReconciliationDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? cityId,
        CancellationToken ct)
    {
        var result = await _cash.GetDashboardAsync(from, to, cityId, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-AD-FIN-03: Escalate a flagged settlement to the Fraud Detection team.
    /// Variances > SAR 20 are automatically eligible for fraud escalation.
    /// </summary>
    [HttpPost("{settlementId:guid}/escalate")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(CashSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Escalate(Guid settlementId,
        [FromBody] EscalateSettlementRequest req, CancellationToken ct)
    {
        var adminId = GetAdminId();
        var result = await _cash.EscalateToFraudAsync(settlementId, adminId, req.Notes, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-AD-FIN-03: Generate a reconciliation report for audit.
    /// Report stored with timestamp. CSV available via /reports/{reportId}/csv.
    /// Variances > SAR 20 automatically flagged to fraud service (UC-AD-FIN-05).
    /// </summary>
    [HttpPost("reconciliation/reports")]
    [Authorize(Policy = "FinanceManager")]
    [ProducesResponseType(typeof(CashReconciliationReportDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> GenerateReport(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? cityId,
        CancellationToken ct)
    {
        var adminId = GetAdminId();
        var result = await _cash.GenerateReportAsync(from, to, cityId, adminId, ct);
        return CreatedAtAction(nameof(GetReports), new { from, to, cityId }, result);
    }

    /// <summary>List previously generated reconciliation reports.</summary>
    [HttpGet("reconciliation/reports")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<CashReconciliationReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReports(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? cityId,
        CancellationToken ct)
    {
        var result = await _cash.GetReportsAsync(from, to, cityId, ct);
        return Ok(result);
    }

    private Guid GetAdminId() =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}

public record CashSettlementReviewRequest(string ResolutionNotes);
