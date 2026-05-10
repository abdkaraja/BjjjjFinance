using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/corporate")]
[Authorize]
[Produces("application/json")]
public class CorporateController : ControllerBase
{
    private readonly ICorporateBillingService _corporate;

    public CorporateController(ICorporateBillingService corporate) => _corporate = corporate;

    // ── Accounts ───────────────────────────────────────────────────────────────

    /// <summary>Get a corporate account by ID.</summary>
    [HttpGet("{accountId:guid}")]
    [Authorize(Policy = "CorporateManager")]
    [ProducesResponseType(typeof(CorporateAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccount(Guid accountId, CancellationToken ct)
    {
        var result = await _corporate.GetAccountAsync(accountId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Create a new corporate account.
    /// Billing model, payment terms, negotiated discount, and budget caps all set at creation.
    /// Contract terms snapshot stored immutably.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "CorporateManager")]
    [ProducesResponseType(typeof(CorporateAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAccount([FromBody] CreateCorporateAccountRequest req, CancellationToken ct)
    {
        var result = await _corporate.CreateAccountAsync(req, ct);
        return CreatedAtAction(nameof(GetAccount), new { accountId = result.AccountId }, result);
    }

    /// <summary>
    /// Update billing model or payment terms.
    /// Requires Finance Manager approval (contract version incremented).
    /// Change is immutably logged with approval reference.
    /// </summary>
    [HttpPut("{accountId:guid}/billing-model")]
    [Authorize(Policy = "FinanceManager")]
    [ProducesResponseType(typeof(CorporateAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBillingModel(Guid accountId,
        [FromBody] UpdateBillingModelRequest req, CancellationToken ct)
    {
        var result = await _corporate.UpdateBillingModelAsync(accountId, req, ct);
        return Ok(result);
    }

    /// <summary>Get corporate accounts below their configured low-balance alert threshold.</summary>
    [HttpGet("alerts/low-balance")]
    [Authorize(Policy = "CorporateManager")]
    [ProducesResponseType(typeof(IEnumerable<CorporateAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowBalanceAccounts(CancellationToken ct)
    {
        var result = await _corporate.GetAccountsBelowAlertThresholdAsync(ct);
        return Ok(result);
    }

    // ── Booking Validation ─────────────────────────────────────────────────────

    /// <summary>
    /// Validate a corporate booking at booking time.
    /// Checks: Corporate Wallet balance, employee budget, cost-center budget, account monthly budget.
    /// Trip is blocked at booking — not at payment time.
    /// </summary>
    [HttpPost("{accountId:guid}/bookings/validate")]
    [Authorize(Policy = "CorporateManager")]
    [ProducesResponseType(typeof(BookingValidationResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateBooking(Guid accountId,
        [FromBody] ValidateBookingRequest req, CancellationToken ct)
    {
        var result = await _corporate.ValidateBookingAsync(accountId, req.EmployeeUserId, req.EstimatedFare, ct);
        return Ok(result);
    }

    // ── Split Pay ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Process a split-pay transaction.
    /// Both company and employee portions must succeed or both are reversed (atomic).
    /// </summary>
    [HttpPost("split-pay")]
    [Authorize(Policy = "CorporateManager")]
    [ProducesResponseType(typeof(SplitPayResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    public async Task<IActionResult> ProcessSplitPay([FromBody] ProcessSplitPayRequest req, CancellationToken ct)
    {
        var result = await _corporate.ProcessSplitPayAsync(req, ct);
        return Ok(result);
    }

    // ── Invoices ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generate a ZATCA-compliant invoice for a billing period.
    /// Sequential invoice number, QR code, seller and buyer VAT registration.
    /// Immutable once generated — errors require a credit note.
    /// </summary>
    [HttpPost("{accountId:guid}/invoices/generate")]
    [Authorize(Policy = "FinanceAdmin")]
    [ProducesResponseType(typeof(CorporateInvoiceDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> GenerateInvoice(Guid accountId,
        [FromBody] GenerateInvoiceRequest req, CancellationToken ct)
    {
        var result = await _corporate.GenerateInvoiceAsync(accountId, req.PeriodStart, req.PeriodEnd, ct);
        return CreatedAtAction(nameof(GetInvoice), new { accountId, invoiceId = result.InvoiceId }, result);
    }

    /// <summary>Get all invoices for a corporate account.</summary>
    [HttpGet("{accountId:guid}/invoices")]
    [Authorize(Policy = "CorporateManager")]
    [ProducesResponseType(typeof(IEnumerable<CorporateInvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoices(Guid accountId, CancellationToken ct)
    {
        var result = await _corporate.GetInvoicesAsync(accountId, ct);
        return Ok(result);
    }

    /// <summary>Get a specific invoice.</summary>
    [HttpGet("{accountId:guid}/invoices/{invoiceId:guid}")]
    [Authorize(Policy = "CorporateManager")]
    [ProducesResponseType(typeof(CorporateInvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoice(Guid accountId, Guid invoiceId, CancellationToken ct)
    {
        var result = await _corporate.GetInvoiceAsync(invoiceId, ct);
        return Ok(result);
    }

    // ── Employees ──────────────────────────────────────────────────────────────

    /// <summary>Get employee budget and allowance status.</summary>
    [HttpGet("{accountId:guid}/employees/{userId:guid}/budget")]
    [Authorize(Policy = "CorporateManager")]
    [ProducesResponseType(typeof(EmployeeBudgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployeeBudget(Guid accountId, Guid userId, CancellationToken ct)
    {
        var result = await _corporate.GetEmployeeBudgetAsync(accountId, userId, ct);
        return Ok(result);
    }

    /// <summary>Update employee monthly budget. Change immutably logged.</summary>
    [HttpPut("{accountId:guid}/employees/{userId:guid}/budget")]
    [Authorize(Policy = "CorporateManager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEmployeeBudget(Guid accountId, Guid userId,
        [FromBody] UpdateEmployeeBudgetRequest req, CancellationToken ct)
    {
        var adminId = GetActorId();
        await _corporate.UpdateEmployeeBudgetAsync(accountId, userId, req.NewBudget, adminId, ct);
        return NoContent();
    }

    private Guid GetActorId() =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}

public record ValidateBookingRequest(Guid EmployeeUserId, decimal EstimatedFare);
public record GenerateInvoiceRequest(DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);
public record UpdateEmployeeBudgetRequest(decimal NewBudget);
