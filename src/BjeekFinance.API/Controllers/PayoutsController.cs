using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/payouts")]
[Authorize]
[Produces("application/json")]
public class PayoutsController : ControllerBase
{
    private readonly IPayoutService _payouts;

    public PayoutsController(IPayoutService payouts) => _payouts = payouts;

    /// <summary>
    /// UC-FIN-PAYOUT-01: Initiate a standard payout to bank/wallet.
    /// Checks KYC verified, AVAILABLE balance sufficient, dunning hold, SARIE window.
    /// Above auto-approve threshold → routes to Finance Admin approval queue.
    /// Out-of-window → actor notified with next SARIE window time (never silently deferred).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "DriverOrDelivery")]
    [ProducesResponseType(typeof(PayoutRequestDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Initiate([FromBody] InitiatePayoutRequest req, CancellationToken ct)
    {
        var result = await _payouts.InitiatePayoutAsync(req, ct);
        return Accepted(result);
    }

    /// <summary>Get a payout request by ID.</summary>
    [HttpGet("{payoutId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(PayoutRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid payoutId, CancellationToken ct)
    {
        var result = await _payouts.GetByIdAsync(payoutId, ct);
        return Ok(result);
    }

    /// <summary>Get payout history for an actor.</summary>
    [HttpGet("actor/{actorId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<PayoutRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByActor(Guid actorId, CancellationToken ct)
    {
        var result = await _payouts.GetByActorAsync(actorId, ct);
        return Ok(result);
    }

    /// <summary>Get all pending payout requests (Finance Admin approval queue).</summary>
    [HttpGet("pending")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<PayoutRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var result = await _payouts.GetPendingPayoutsAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Finance Admin: approve a pending payout.
    /// Above SAR 37,000 requires Super Admin — enforced via RBAC policy.
    /// </summary>
    [HttpPost("{payoutId:guid}/approve")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(PayoutRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid payoutId, CancellationToken ct)
    {
        var approverActorId = GetActorId();
        var result = await _payouts.ApprovePayoutAsync(payoutId, approverActorId, ct);
        return Ok(result);
    }

    /// <summary>Finance Admin: reject a pending payout with a reason code.</summary>
    [HttpPost("{payoutId:guid}/reject")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(PayoutRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid payoutId,
        [FromBody] RejectPayoutRequest req, CancellationToken ct)
    {
        var approverActorId = GetActorId();
        var result = await _payouts.RejectPayoutAsync(payoutId, approverActorId, req.ReasonCode, ct);
        return Ok(result);
    }

    /// <summary>
    /// Internal: process queued SARIE transfers.
    /// Triggered by scheduler when SARIE window opens (Sun–Thu 08:00 AST).
    /// </summary>
    [HttpPost("process-sarie-queue")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ProcessSarieQueue(CancellationToken ct)
    {
        await _payouts.ProcessSarieQueueAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// PSP/gateway webhook: confirm transfer completed.
    /// Releases balance_hold, records PSP transaction reference.
    /// </summary>
    [HttpPost("{payoutId:guid}/complete")]
    [AllowAnonymous] // Secured via HMAC signature validation (production: PSP webhook secret)
    [ProducesResponseType(typeof(PayoutRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid payoutId,
        [FromBody] CompletePayoutRequest req, CancellationToken ct)
    {
        var result = await _payouts.CompletePayoutAsync(payoutId, req.PspTransactionId, req.TransferReference, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retry a failed payout (EX2: exponential backoff, max 3 attempts).
    /// After 3 failures, hold released back to available, admin notified.
    /// </summary>
    [HttpPost("{payoutId:guid}/retry")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(PayoutRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Retry(Guid payoutId, CancellationToken ct)
    {
        var result = await _payouts.RetryPayoutAsync(payoutId, ct);
        return Ok(result);
    }

    private Guid GetActorId() =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}

public record RejectPayoutRequest(string ReasonCode);
public record CompletePayoutRequest(string PspTransactionId, string TransferReference);
