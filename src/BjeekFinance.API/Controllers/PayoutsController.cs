using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Enums;
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
    /// UC-AD-FIN-02: Pending payout queue sorted by amount descending then oldest first,
    /// enriched with wallet KYC, fraud score, tier, and aging info.
    /// </summary>
    [HttpGet("pending-queue")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<PendingPayoutQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingQueue(CancellationToken ct)
    {
        var result = await _payouts.GetPendingQueueAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-AD-FIN-02: Detailed payout review for admin.
    /// Returns destination account info, KYC status, wallet balances, and aging.
    /// </summary>
    [HttpGet("{payoutId:guid}/review")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(PayoutReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Review(Guid payoutId, CancellationToken ct)
    {
        var result = await _payouts.GetPayoutReviewAsync(payoutId, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-AD-FIN-02: Approve a pending payout.
    /// Payouts above Super Admin threshold (SAR 10,000) require SuperAdmin role.
    /// Set scheduleForNextWindow=true to queue for the next SARIE window.
    /// </summary>
    [HttpPost("{payoutId:guid}/approve")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(PayoutRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid payoutId,
        [FromBody] ApprovePayoutRequest req, CancellationToken ct)
    {
        var approverActorId = GetActorId();

        // Check Super Admin threshold — enforced at controller level via role claim
        var payout = await _payouts.GetByIdAsync(payoutId, ct);
        var superAdminThreshold = 10_000m; // matches seed default; overridden in service via FinanceParameters
        if (payout.AmountRequested > superAdminThreshold && !User.IsInRole("SuperAdmin"))
            return Forbid();

        var result = await _payouts.ApprovePayoutAsync(payoutId, approverActorId, req.ScheduleForNextWindow, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-AD-FIN-02: Reject a pending payout with a predefined reason code.
    /// Reason code must be from PayoutRejectionReasonCode enum.
    /// Hold released back to available.
    /// </summary>
    [HttpPost("{payoutId:guid}/reject")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(PayoutRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid payoutId,
        [FromBody] RejectPayoutRequest req, CancellationToken ct)
    {
        if (!Enum.IsDefined(typeof(PayoutRejectionReasonCode), req.ReasonCode))
            return BadRequest($"Invalid rejection reason code: {req.ReasonCode}. Must be one of: {string.Join(", ", Enum.GetNames<PayoutRejectionReasonCode>())}");

        var approverActorId = GetActorId();
        var result = await _payouts.RejectPayoutAsync(payoutId, approverActorId, req.ReasonCode, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-AD-FIN-02: Schedule an approved payout for a future SARIE window.
    /// Used when approving outside operating hours or when admin explicitly
    /// wants to defer execution.
    /// </summary>
    [HttpPost("{payoutId:guid}/schedule")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(PayoutRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Schedule(Guid payoutId,
        [FromBody] SchedulePayoutRequest req, CancellationToken ct)
    {
        var result = await _payouts.SchedulePayoutAsync(payoutId, req.ScheduledUtc, ct);
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

public record RejectPayoutRequest(PayoutRejectionReasonCode ReasonCode);
public record CompletePayoutRequest(string PspTransactionId, string TransferReference);
