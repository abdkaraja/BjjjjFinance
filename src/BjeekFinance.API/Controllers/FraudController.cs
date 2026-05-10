using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BjeekFinance.API.Controllers;

[ApiController]
[Route("api/v1/fraud")]
[Authorize]
[Produces("application/json")]
public class FraudController : ControllerBase
{
    private readonly IFraudDetectionService _fraud;

    public FraudController(IFraudDetectionService fraud) => _fraud = fraud;

    // ── Cases ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// UC-AD-FIN-05: Get all open fraud cases, optionally filtered by minimum severity.
    /// Dashboard for Fraud Team.
    /// </summary>
    [HttpGet("cases/open")]
    [Authorize(Policy = "FraudTeam")]
    [ProducesResponseType(typeof(IEnumerable<FraudCaseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOpenCases(
        CancellationToken ct,
        [FromQuery] FraudSeverity minSeverity = FraudSeverity.Medium)
    {
        var result = await _fraud.GetOpenCasesAsync(minSeverity, ct);
        return Ok(result);
    }

    /// <summary>Get all fraud cases for a specific actor.</summary>
    [HttpGet("cases/actor/{actorId:guid}")]
    [Authorize(Policy = "FraudTeam")]
    [ProducesResponseType(typeof(IEnumerable<FraudCaseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCasesByActor(Guid actorId, CancellationToken ct)
    {
        var result = await _fraud.GetCasesByActorAsync(actorId, ct);
        return Ok(result);
    }

    /// <summary>Get a single fraud case by ID.</summary>
    [HttpGet("cases/{caseId:guid}")]
    [Authorize(Policy = "FraudTeam")]
    [ProducesResponseType(typeof(FraudCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCase(Guid caseId, CancellationToken ct)
    {
        var result = await _fraud.GetCaseAsync(caseId, ct);
        return Ok(result);
    }

    /// <summary>Manually create a fraud case (admin escalation).</summary>
    [HttpPost("cases")]
    [Authorize(Policy = "FraudTeam")]
    [ProducesResponseType(typeof(FraudCaseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCase([FromBody] CreateFraudCaseRequest req, CancellationToken ct)
    {
        var result = await _fraud.CreateCaseAsync(req, ct);
        return CreatedAtAction(nameof(GetCase), new { caseId = result.CaseId }, result);
    }

    /// <summary>Assign a fraud case to a team member for investigation.</summary>
    [HttpPost("cases/{caseId:guid}/assign")]
    [Authorize(Policy = "FraudManager")]
    [ProducesResponseType(typeof(FraudCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignCase(Guid caseId, [FromBody] AssignFraudCaseRequest req, CancellationToken ct)
    {
        var result = await _fraud.AssignCaseAsync(caseId, req, ct);
        return Ok(result);
    }

    /// <summary>Add an investigation note to a fraud case.</summary>
    [HttpPost("cases/{caseId:guid}/notes")]
    [Authorize(Policy = "FraudTeam")]
    [ProducesResponseType(typeof(FraudCaseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddNote(Guid caseId, [FromBody] AddInvestigationNoteRequest req, CancellationToken ct)
    {
        var result = await _fraud.AddNoteAsync(caseId, req, ct);
        return Ok(result);
    }

    /// <summary>
    /// Resolve a fraud case with a resolution code.
    /// Whitelist option restores wallet access (Instant Pay re-enabled, fraud score reset).
    /// </summary>
    [HttpPost("cases/{caseId:guid}/resolve")]
    [Authorize(Policy = "FraudManager")]
    [ProducesResponseType(typeof(FraudCaseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveCase(Guid caseId, [FromBody] ResolveFraudCaseRequest req, CancellationToken ct)
    {
        var resolvedByActorId = GetActorId();
        var result = await _fraud.ResolveCaseAsync(caseId, req, resolvedByActorId, ct);
        return Ok(result);
    }

    /// <summary>Archive a fraud case (closed investigation, record retention).</summary>
    [HttpPost("cases/{caseId:guid}/archive")]
    [Authorize(Policy = "FraudManager")]
    [ProducesResponseType(typeof(FraudCaseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ArchiveCase(Guid caseId, CancellationToken ct)
    {
        var archivedByActorId = GetActorId();
        var result = await _fraud.ArchiveCaseAsync(caseId, archivedByActorId, ct);
        return Ok(result);
    }

    // ── Rules ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// UC-AD-FIN-05: Get all active fraud rules, optionally filtered by domain.
    /// Rules are admin-configurable — no hardcoded thresholds.
    /// </summary>
    [HttpGet("rules")]
    [Authorize(Policy = "FraudTeam")]
    [ProducesResponseType(typeof(IEnumerable<FraudRuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRules([FromQuery] string? domain, CancellationToken ct)
    {
        var result = await _fraud.GetActiveRulesAsync(domain, ct);
        return Ok(result);
    }

    /// <summary>Get a specific fraud rule by key.</summary>
    [HttpGet("rules/{ruleKey}")]
    [Authorize(Policy = "FraudTeam")]
    [ProducesResponseType(typeof(FraudRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRule(string ruleKey, CancellationToken ct)
    {
        var result = await _fraud.GetRuleAsync(ruleKey, ct);
        return Ok(result);
    }

    /// <summary>
    /// Update a fraud rule's threshold, severity, auto-action, or active status.
    /// All fraud rule parameters are admin-configurable.
    /// </summary>
    [HttpPut("rules/{ruleKey}")]
    [Authorize(Policy = "FraudManager")]
    [ProducesResponseType(typeof(FraudRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRule(string ruleKey, [FromBody] UpdateFraudRuleRequest req, CancellationToken ct)
    {
        var result = await _fraud.UpdateRuleAsync(ruleKey, req, ct);
        return Ok(result);
    }

    /// <summary>
    /// Event-driver endpoint: evaluate a trigger event against active fraud rules.
    /// Used by microservice event bus or internal services.
    /// </summary>
    [HttpPost("evaluate")]
    [Authorize(Policy = "FraudTeam")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> EvaluateEvent([FromBody] CreateFraudCaseRequest req, CancellationToken ct)
    {
        var result = await _fraud.EvaluateEventAsync(req.RuleKey, req.ActorId, req.ActorType, req.TriggerEvent, req.RelatedEntityId, req.RelatedEntityType, ct);
        if (result is null) return Ok(new { triggered = false });
        return Ok(new { triggered = true, fraudCase = result });
    }

    private Guid GetActorId() =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}
