using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/instant-pay")]
[Authorize]
[Produces("application/json")]
public class InstantPayController : ControllerBase
{
    private readonly IInstantPayService _instantPay;

    public InstantPayController(IInstantPayService instantPay) => _instantPay = instantPay;

    /// <summary>
    /// Check real-time Instant Pay eligibility for a given actor and amount.
    /// Returns tier, fee, daily count remaining, and failure reason if ineligible.
    /// </summary>
    [HttpGet("eligibility")]
    [Authorize(Policy = "DriverOrDelivery")]
    [ProducesResponseType(typeof(EligibilityResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckEligibility(
        [FromQuery] Guid actorId,
        [FromQuery] decimal amount,
        CancellationToken ct)
    {
        var result = await _instantPay.CheckEligibilityAsync(actorId, amount, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-INSTANT-01: Initiate on-demand Instant Pay cashout.
    /// Draws ONLY from AVAILABLE balance — never PENDING.
    /// Separate eligibility engine; no admin approval gate.
    /// ZATCA-compliant micro-invoice generated per cashout.
    /// Daily limit resets at local city midnight.
    /// </summary>
    [HttpPost("cashouts")]
    [Authorize(Policy = "DriverOrDelivery")]
    [ProducesResponseType(typeof(InstantPayResultDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Cashout([FromBody] InstantPayRequest req, CancellationToken ct)
    {
        var result = await _instantPay.InitiateCashoutAsync(req, ct);
        return Accepted(result);
    }

    /// <summary>Get Instant Pay cashout history for an actor.</summary>
    [HttpGet("cashouts/actor/{actorId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<InstantPayCashoutDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByActor(Guid actorId, CancellationToken ct)
    {
        var result = await _instantPay.GetByActorAsync(actorId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Finance Admin: revoke Instant Pay for an actor.
    /// Reason code and estimated resolution timeline required.
    /// Actor notified asynchronously.
    /// </summary>
    [HttpPost("{walletId:guid}/revoke")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid walletId,
        [FromBody] RevokeInstantPayRequest req, CancellationToken ct)
    {
        var adminId = GetActorId();
        await _instantPay.RevokeInstantPayAsync(walletId, req.ReasonCode, req.EstimatedResolution, adminId, ct);
        return NoContent();
    }

    /// <summary>
    /// Internal: nightly tier recalculation job.
    /// Promotes/demotes wallets based on trip count, rating, fraud flags.
    /// All thresholds admin-configurable.
    /// </summary>
    [HttpPost("recalculate-tiers")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecalculateTiers(CancellationToken ct)
    {
        await _instantPay.RecalculateTiersAsync(ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  UC-FIN-INSTANT-01: PSP webhooks & lifecycle
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PSP/gateway webhook: confirms Instant Pay transfer was successful.
    /// Releases balance_hold, records PSP reference.
    /// </summary>
    [HttpPost("cashouts/{cashoutId:guid}/complete")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InstantPayResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteCashout(Guid cashoutId,
        [FromBody] InstantPayWebhookRequest req, CancellationToken ct)
    {
        var result = await _instantPay.CompleteCashoutAsync(cashoutId, req.PspTransactionId, req.TransferReference, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-INSTANT-01 AF2: Primary transfer rail failed.
    /// Falls back to standard IBAN transfer within T+1 business days.
    /// If already fallback (EX4), releases hold and marks failed.
    /// </summary>
    [HttpPost("cashouts/{cashoutId:guid}/fail")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InstantPayCashoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> FailCashout(Guid cashoutId,
        [FromBody] InstantPayFailureRequest req, CancellationToken ct)
    {
        var result = await _instantPay.FailCashoutAsync(cashoutId, req.FailureReason, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-INSTANT-01 EX3: Account flagged during transfer.
    /// Cancels cashout, releases hold back to available, raises fraud alert.
    /// </summary>
    [HttpPost("cashouts/{cashoutId:guid}/cancel")]
    [Authorize(Policy = "FraudOfficer")]
    [ProducesResponseType(typeof(InstantPayCashoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelCashout(Guid cashoutId,
        [FromBody] InstantPayCancelRequest req, CancellationToken ct)
    {
        var result = await _instantPay.CancelCashoutAsync(cashoutId, req.ReasonCode, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-INSTANT-01 AF3: Trigger auto-cashout for all eligible wallets.
    /// Scans wallets with AutoCashoutThreshold and available balance ≥ threshold.
    /// </summary>
    [HttpPost("auto-cashouts/process")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessAutoCashouts(CancellationToken ct)
    {
        var count = await _instantPay.ProcessAutoCashoutsAsync(ct);
        return Ok(new { processedCount = count });
    }

    private Guid GetActorId() =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}

public record RevokeInstantPayRequest(string ReasonCode, string EstimatedResolution);
public record InstantPayWebhookRequest(string PspTransactionId, string TransferReference);
public record InstantPayFailureRequest(string FailureReason);
public record InstantPayCancelRequest(string ReasonCode);
