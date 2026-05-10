using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/admin/finance")]
[Authorize(Policy = "FinanceAdmin")]
[Produces("application/json")]
public class AdminFinanceController : ControllerBase
{
    private readonly IAdminFinanceService _admin;

    public AdminFinanceController(IAdminFinanceService admin) => _admin = admin;

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

    // ── Finance Parameters ─────────────────────────────────────────────────────

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

    /// <summary>Get a specific finance parameter with optional city and service-type scope.</summary>
    [HttpGet("parameters/{key}")]
    [ProducesResponseType(typeof(FinanceParameterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParameter(
        string key,
        [FromQuery] Guid? cityId,
        [FromQuery] string? serviceType,
        CancellationToken ct)
    {
        var result = await _admin.GetParameterAsync(key, cityId, serviceType, ct);
        return Ok(result);
    }

    /// <summary>
    /// Update a finance parameter.
    /// Previous value retained as versioned history.
    /// Finance Admin level required; change immutably logged.
    /// </summary>
    [HttpPut("parameters")]
    [ProducesResponseType(typeof(FinanceParameterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateParameter([FromBody] UpdateParameterRequest req, CancellationToken ct)
    {
        var result = await _admin.UpdateParameterAsync(req, ct);
        return Ok(result);
    }

    private Guid GetActorId() =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}
