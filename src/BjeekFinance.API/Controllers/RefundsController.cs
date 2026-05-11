using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/refunds")]
[Authorize]
[Produces("application/json")]
public class RefundsController : ControllerBase
{
    private readonly IRefundService _refunds;

    public RefundsController(IRefundService refunds) => _refunds = refunds;

    /// <summary>
    /// UC-FIN-REFUND-ENGINE-01 Step 1: Support Agent submits a refund request.
    /// System pre-fills available data, validates, runs auto-approval rule engine,
    /// and routes to the correct approval tier if manual review is needed.
    /// </summary>
    [HttpPost("request")]
    [Authorize(Policy = "SupportAgent")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitRefundRequest([FromBody] InitiateRefundRequest req, CancellationToken ct)
    {
        var result = await _refunds.SubmitRefundRequestAsync(req, ct);
        return CreatedAtAction(nameof(GetRefund), new { refundId = result.RefundId }, result);
    }

    /// <summary>
    /// UC-FIN-REFUND-ENGINE-01: Get pre-filled refund form data for a transaction.
    /// Returns trip/order details, fare amount, previous refunds, available-for-refund balance.
    /// </summary>
    [HttpGet("pre-fill/{transactionId:guid}")]
    [Authorize(Policy = "SupportAgent")]
    [ProducesResponseType(typeof(RefundRequestPreFillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreFill(Guid transactionId, CancellationToken ct)
    {
        var result = await _refunds.GetRefundPreFillAsync(transactionId, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-REFUND-ENGINE-01: Get pending approval queue for approver dashboard.
    /// Returns all refunds awaiting approval, sorted by tier priority then oldest first.
    /// </summary>
    [HttpGet("pending-queue")]
    [Authorize(Policy = "FinanceOfficer")]
    [ProducesResponseType(typeof(IEnumerable<RefundApprovalQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingQueue(CancellationToken ct)
    {
        var result = await _refunds.GetPendingApprovalQueueAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-REFUND-ENGINE-01: Get detailed refund review for approver modal.
    /// Shows request details (left panel) + customer profile (right panel) + AI recommendation.
    /// </summary>
    [HttpGet("{refundId:guid}/review")]
    [Authorize(Policy = "FinanceOfficer")]
    [ProducesResponseType(typeof(RefundReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRefundReview(Guid refundId, CancellationToken ct)
    {
        var result = await _refunds.GetRefundReviewAsync(refundId, ct);
        return Ok(result);
    }

    /// <summary>Get refund by ID.</summary>
    [HttpGet("{refundId:guid}")]
    [Authorize(Policy = "SupportAgent")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRefund(Guid refundId, CancellationToken ct)
    {
        var result = await _refunds.GetRefundAsync(refundId, ct);
        return Ok(result);
    }

    /// <summary>Get refund for a specific transaction.</summary>
    [HttpGet("by-transaction/{transactionId:guid}")]
    [Authorize(Policy = "SupportAgent")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByTransaction(Guid transactionId, CancellationToken ct)
    {
        var result = await _refunds.GetRefundByTransactionAsync(transactionId, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>Get all refunds initiated by an actor.</summary>
    [HttpGet("by-actor/{actorId:guid}")]
    [Authorize(Policy = "SupportAgent")]
    [ProducesResponseType(typeof(IEnumerable<RefundDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByActor(Guid actorId, CancellationToken ct)
    {
        var result = await _refunds.GetRefundsByActorAsync(actorId, ct);
        return Ok(result);
    }

    /// <summary>Get all refunds with a specific status.</summary>
    [HttpGet("by-status/{status}")]
    [Authorize(Policy = "SupportAgent")]
    [ProducesResponseType(typeof(IEnumerable<RefundDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStatus(RefundStatus status, CancellationToken ct)
    {
        var result = await _refunds.GetRefundsByStatusAsync(status, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-REFUND-ENGINE-01 Step 8a: Approve a refund.
    /// Approver can approve at requested amount or an adjusted amount.
    /// On approval: wallet credited or card reversal initiated, customer notified,
    /// driver warned if checkbox selected, audit log created.
    /// </summary>
    [HttpPost("{refundId:guid}/approve")]
    [Authorize(Policy = "FinanceOfficer")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveRefund(Guid refundId, [FromBody] ApproveRefundRequest req, CancellationToken ct)
    {
        var result = await _refunds.ApproveRefundAsync(refundId, req, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-REFUND-ENGINE-01 Step 8b: Deny a refund with reason code.
    /// Customer notified, request closed, audit log created.
    /// </summary>
    [HttpPost("{refundId:guid}/deny")]
    [Authorize(Policy = "FinanceOfficer")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DenyRefund(Guid refundId, [FromBody] DenyRefundRequest req, CancellationToken ct)
    {
        var result = await _refunds.DenyRefundAsync(refundId, req, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-REFUND-ENGINE-01 AF3: Approver requests more info from the initiating agent.
    /// SLA paused until agent responds.
    /// </summary>
    [HttpPost("{refundId:guid}/request-more-info")]
    [Authorize(Policy = "FinanceOfficer")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestMoreInfo(Guid refundId, [FromBody] RequestMoreInfoRefundRequest req, CancellationToken ct)
    {
        var result = await _refunds.RequestMoreInfoAsync(refundId, req, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-REFUND-ENGINE-01 AF3: Support Agent responds to a more-info request.
    /// SLA resumes. Request re-enters approval queue.
    /// </summary>
    [HttpPost("{refundId:guid}/respond-more-info")]
    [Authorize(Policy = "SupportAgent")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RespondMoreInfo(Guid refundId, [FromBody] RespondMoreInfoRefundRequest req, CancellationToken ct)
    {
        var result = await _refunds.RespondToMoreInfoAsync(refundId, req, ct);
        return Ok(result);
    }

    /// <summary>
    /// UC-FIN-REFUND-ENGINE-01 EX1: Retry a failed wallet credit for a processing refund.
    /// Finance Admin only. Max 3 retries before admin intervention.
    /// </summary>
    [HttpPost("{refundId:guid}/retry-credit")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetryWalletCredit(Guid refundId, CancellationToken ct)
    {
        var result = await _refunds.RetryWalletCreditAsync(refundId, ct);
        return Ok(result);
    }
}
