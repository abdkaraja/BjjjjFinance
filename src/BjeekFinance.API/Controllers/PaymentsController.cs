using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentCollectionService _payments;

    public PaymentsController(IPaymentCollectionService payments) => _payments = payments;

    /// <summary>
    /// UC-FIN-COLLECT-01: Collect payment and atomically distribute proceeds
    /// across all relevant wallets using a Saga pattern.
    /// Idempotency key required — duplicate webhook events will be silently deduplicated.
    /// </summary>
    [HttpPost("collect")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(CollectPaymentResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Collect([FromBody] CollectPaymentRequest req, CancellationToken ct)
    {
        var result = await _payments.CollectPaymentAsync(req, ct);
        return CreatedAtAction(nameof(GetTransaction), new { transactionId = result.TransactionId }, result);
    }

    /// <summary>
    /// UC-FIN-TIP-01: Add tip for driver post-ride.
    /// Tips are 100% to driver — not subject to platform commission.
    /// Tip window duration is admin-configurable (default 2 hours).
    /// </summary>
    [HttpPost("tips")]
    [Authorize(Policy = "User")]
    [ProducesResponseType(typeof(TipResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddTip([FromBody] AddTipRequest req, CancellationToken ct)
    {
        var result = await _payments.AddTipAsync(req, ct);
        return Created(string.Empty, result);
    }

    /// <summary>Get a transaction by ID.</summary>
    [HttpGet("transactions/{transactionId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransaction(Guid transactionId, CancellationToken ct)
    {
        var result = await _payments.GetTransactionAsync(transactionId, ct);
        return Ok(result);
    }

    /// <summary>Get transactions for a wallet within an optional date range.</summary>
    [HttpGet("transactions")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<TransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByWallet(
        [FromQuery] Guid walletId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var result = await _payments.GetTransactionsByWalletAsync(walletId, from, to, ct);
        return Ok(result);
    }
}
