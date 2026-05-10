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

    private Guid GetAdminId() =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}

public record CashSettlementReviewRequest(string ResolutionNotes);
