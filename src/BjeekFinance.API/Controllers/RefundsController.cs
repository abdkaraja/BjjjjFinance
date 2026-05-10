using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
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
    /// UC-FIN-REFUND-01: Initiate full refund.
    /// Reverses completed payment — commission, VAT, and net amount.
    /// Card payments → gateway reversal (T+3 settlement).
    /// Wallet payments → instant BalanceRefundCredit.
    /// Driver/merchant debited for net; platform adjusted for commission reversal.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> InitiateRefund([FromBody] InitiateRefundRequest req, CancellationToken ct)
    {
        var result = await _refunds.InitiateRefundAsync(req, ct);
        return CreatedAtAction(nameof(GetRefund), new { refundId = result.RefundId }, result);
    }

    /// <summary>Get refund by ID.</summary>
    [HttpGet("{refundId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRefund(Guid refundId, CancellationToken ct)
    {
        var result = await _refunds.GetRefundAsync(refundId, ct);
        return Ok(result);
    }

    /// <summary>Get refund for a specific transaction.</summary>
    [HttpGet("by-transaction/{transactionId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
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
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<RefundDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByActor(Guid actorId, CancellationToken ct)
    {
        var result = await _refunds.GetRefundsByActorAsync(actorId, ct);
        return Ok(result);
    }
}
