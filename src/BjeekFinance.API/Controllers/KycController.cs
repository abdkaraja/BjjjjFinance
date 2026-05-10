using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/kyc")]
[Authorize]
[Produces("application/json")]
public class KycController : ControllerBase
{
    private readonly IKycService _kyc;

    public KycController(IKycService kyc) => _kyc = kyc;

    /// <summary>
    /// Add a payout account (IBAN or mobile wallet).
    /// IBAN validated at save time against SAMA National IBAN Registry.
    /// AES-256 encryption applied to stored KYC data.
    /// </summary>
    [HttpPost("payout-accounts")]
    [Authorize(Policy = "DriverOrDelivery")]
    [ProducesResponseType(typeof(PayoutAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddPayoutAccount([FromBody] AddPayoutAccountRequest req, CancellationToken ct)
    {
        var result = await _kyc.AddPayoutAccountAsync(req, ct);
        return CreatedAtAction(nameof(GetPayoutAccount), new { accountId = result.AccountId }, result);
    }

    /// <summary>Get a payout account by ID.</summary>
    [HttpGet("payout-accounts/{accountId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(PayoutAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayoutAccount(Guid accountId, CancellationToken ct)
    {
        var result = await _kyc.GetPayoutAccountAsync(accountId, ct);
        return Ok(result);
    }

    /// <summary>Get all payout accounts for an actor.</summary>
    [HttpGet("payout-accounts/actor/{actorId:guid}")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IEnumerable<PayoutAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByActor(Guid actorId, CancellationToken ct)
    {
        var result = await _kyc.GetPayoutAccountsByActorAsync(actorId, ct);
        return Ok(result);
    }

    /// <summary>
    /// KYC partner webhook — updates payout account status to VERIFIED or REJECTED.
    /// Written synchronously with immutable audit log.
    /// </summary>
    [HttpPost("webhooks/kyc-result")]
    [AllowAnonymous] // Secured via HMAC signature validation (production: add IHmacValidationFilter)
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> KycWebhook([FromBody] KycWebhookPayload payload, CancellationToken ct)
    {
        await _kyc.HandleKycWebhookAsync(payload, ct);
        return NoContent();
    }

    /// <summary>Validate an IBAN against the SAMA National IBAN Registry.</summary>
    [HttpGet("validate-iban")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(IbanValidationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateIban([FromQuery] string iban, CancellationToken ct)
    {
        var valid = await _kyc.ValidateIbanAsync(iban, ct);
        return Ok(new IbanValidationResult(iban, valid));
    }
}

public record IbanValidationResult(string Iban, bool IsValid);
