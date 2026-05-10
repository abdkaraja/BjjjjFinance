using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/wallets")]
[Authorize]
[Produces("application/json")]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _wallets;

    public WalletsController(IWalletService wallets) => _wallets = wallets;

    /// <summary>Get wallet by ID.</summary>
    [HttpGet("{walletId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(WalletDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid walletId, CancellationToken ct)
    {
        var result = await _wallets.GetWalletByIdAsync(walletId, ct);
        return Ok(result);
    }

    /// <summary>Get wallet for a specific actor.</summary>
    [HttpGet("actor/{actorId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(WalletDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByActor(Guid actorId, [FromQuery] ActorType actorType, CancellationToken ct)
    {
        var result = await _wallets.GetWalletAsync(actorId, actorType, ct);
        return Ok(result);
    }

    /// <summary>Get all wallets of a given actor type.</summary>
    [HttpGet]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<WalletSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] ActorType actorType, CancellationToken ct)
    {
        var result = await _wallets.GetWalletsByActorTypeAsync(actorType, ct);
        return Ok(result);
    }

    /// <summary>
    /// Finance Admin: manually correct wallet balance.
    /// Immutable audit log written synchronously before response.
    /// </summary>
    [HttpPost("{walletId:guid}/corrections")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminCorrection(Guid walletId,
        [FromBody] AdminCorrectionRequest req, CancellationToken ct)
    {
        var adminId = GetActorId();
        await _wallets.AdminBalanceCorrectionAsync(walletId, req.CorrectionAmount, req.Reason, adminId, ct);
        return NoContent();
    }

    /// <summary>Settle pending earnings → available balance (called by internal scheduler).</summary>
    [HttpPost("{walletId:guid}/settle-pending")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SettlePending(Guid walletId, CancellationToken ct)
    {
        await _wallets.SettlePendingEarningsAsync(walletId, ct);
        return NoContent();
    }

    /// <summary>Place a hold on available balance.</summary>
    [HttpPost("{walletId:guid}/holds")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Hold(Guid walletId, [FromBody] HoldRequest req, CancellationToken ct)
    {
        var adminId = GetActorId();
        await _wallets.HoldAsync(walletId, req.Amount, req.Reason, adminId, ActorRole.FinanceAdmin, ct);
        return NoContent();
    }

    /// <summary>Release an existing hold back to available balance.</summary>
    [HttpDelete("{walletId:guid}/holds")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReleaseHold(Guid walletId, [FromBody] HoldRequest req, CancellationToken ct)
    {
        var adminId = GetActorId();
        await _wallets.ReleaseHoldAsync(walletId, req.Amount, req.Reason, adminId, ActorRole.FinanceAdmin, ct);
        return NoContent();
    }

    /// <summary>
    /// Collect cash commission from driver.
    /// Reduces CashReceivable and restores AVAILABLE balance.
    /// </summary>
    [HttpPost("{walletId:guid}/collect-cash-commission")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CollectCashCommission(Guid walletId,
        [FromBody] CashCommissionRequest req, CancellationToken ct)
    {
        await _wallets.CollectCashCommissionAsync(walletId, req.Amount, ct);
        return NoContent();
    }

    private Guid GetActorId() =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}

public record AdminCorrectionRequest(decimal CorrectionAmount, string Reason);
public record HoldRequest(decimal Amount, string Reason);
public record CashCommissionRequest(decimal Amount);
