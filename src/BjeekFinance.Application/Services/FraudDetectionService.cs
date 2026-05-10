using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using System.Text.Json;

namespace BjeekFinance.Application.Services;

/// <summary>
/// UC-AD-FIN-05: Fraud detection engine.
/// Event-driven rule evaluation, case lifecycle management, auto-suspend actions.
/// All fraud rules are admin-configurable — no hardcoded thresholds in application layer.
/// </summary>
public class FraudDetectionService : IFraudDetectionService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public FraudDetectionService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<FraudCaseDto?> EvaluateEventAsync(string ruleKey, Guid actorId, string actorType, string triggerEvent, Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken ct = default)
    {
        var rule = await _uow.FraudRules.GetByKeyAsync(ruleKey, ct);
        if (rule is null || !rule.IsActive) return null;

        var caseDto = await CreateCaseAsync(new CreateFraudCaseRequest(
            ruleKey, actorId, actorType, triggerEvent, rule.Severity, relatedEntityId, relatedEntityType), ct);

        // Auto-action based on severity and rule config
        if (rule.AutoAction == FraudAutoAction.SuspendInstantPay || rule.Severity >= FraudSeverity.High)
        {
            var wallets = await _uow.Wallets.GetByActorOnlyAsync(actorId, ct);
            foreach (var w in wallets)
            {
                w.InstantPayEnabled = false;
                w.FraudScore = Math.Min(w.FraudScore + 20, 100);
                w.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(w);
            }
        }

        if (rule.AutoAction == FraudAutoAction.FreezeWallet || rule.Severity == FraudSeverity.Critical)
        {
            var wallets = await _uow.Wallets.GetByActorOnlyAsync(actorId, ct);
            foreach (var w in wallets)
            {
                w.FraudScore = Math.Min(w.FraudScore + 50, 100);
                w.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(w);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return caseDto;
    }

    public async Task<FraudCaseDto> CreateCaseAsync(CreateFraudCaseRequest req, CancellationToken ct = default)
    {
        var fraudCase = new FraudCase
        {
            RuleKey = req.RuleKey,
            ActorId = req.ActorId,
            ActorType = Enum.Parse<ActorType>(req.ActorType),
            TriggerEvent = req.TriggerEvent,
            Severity = req.Severity,
            Status = FraudCaseStatus.Open,
            AutoActionTaken = FraudAutoAction.NotifyOnly,
            RelatedEntityId = req.RelatedEntityId,
            RelatedEntityType = req.RelatedEntityType
        };
        await _uow.FraudCases.AddAsync(fraudCase, ct);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Fraud, "FRAUD_CASE_CREATED",
            Guid.Empty, ActorRole.System,
            fraudCase.Id, "FRAUD_CASE", null,
            JsonSerializer.Serialize(new
            {
                caseId = fraudCase.Id,
                req.RuleKey, req.ActorId, req.ActorType,
                req.Severity, req.TriggerEvent
            }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapCaseToDto(fraudCase);
    }

    public async Task<FraudCaseDto> AssignCaseAsync(Guid caseId, AssignFraudCaseRequest req, CancellationToken ct = default)
    {
        var fraudCase = await LoadCaseAsync(caseId, ct);
        if (fraudCase.Status == FraudCaseStatus.Resolved || fraudCase.Status == FraudCaseStatus.Archived)
            throw new InvalidOperationException($"Cannot assign a {fraudCase.Status} case.");

        var previousAssignee = fraudCase.AssignedToActorId;
        fraudCase.AssignedToActorId = req.AssignedToActorId;
        if (fraudCase.Status == FraudCaseStatus.Open)
            fraudCase.Status = FraudCaseStatus.Investigating;
        _uow.FraudCases.Update(fraudCase);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Fraud, "FRAUD_CASE_ASSIGNED",
            req.AssignedToActorId, ActorRole.FraudManager,
            caseId, "FRAUD_CASE",
            previousAssignee is not null ? JsonSerializer.Serialize(new { previousAssignee }) : null,
            JsonSerializer.Serialize(new { assignedTo = req.AssignedToActorId, status = fraudCase.Status.ToString() }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapCaseToDto(fraudCase);
    }

    public async Task<FraudCaseDto> AddNoteAsync(Guid caseId, AddInvestigationNoteRequest req, CancellationToken ct = default)
    {
        var fraudCase = await LoadCaseAsync(caseId, ct);
        var notes = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(fraudCase.InvestigationNotesJson) ?? new();
        notes.Add(new Dictionary<string, string>
        {
            ["note"] = req.Note,
            ["addedBy"] = req.ActorId.ToString(),
            ["addedAt"] = DateTime.UtcNow.ToString("O")
        });
        fraudCase.InvestigationNotesJson = JsonSerializer.Serialize(notes);
        _uow.FraudCases.Update(fraudCase);

        await _uow.SaveChangesAsync(ct);
        return MapCaseToDto(fraudCase);
    }

    public async Task<FraudCaseDto> ResolveCaseAsync(Guid caseId, ResolveFraudCaseRequest req, Guid resolvedByActorId, CancellationToken ct = default)
    {
        var fraudCase = await LoadCaseAsync(caseId, ct);
        if (fraudCase.Status == FraudCaseStatus.Resolved || fraudCase.Status == FraudCaseStatus.Archived)
            throw new InvalidOperationException($"Case is already {fraudCase.Status}.");

        fraudCase.Status = FraudCaseStatus.Resolved;
        fraudCase.ResolutionCode = req.ResolutionCode;
        fraudCase.ResolutionNotes = req.ResolutionNotes;
        fraudCase.ResolvedAt = DateTime.UtcNow;
        fraudCase.ResolvedByActorId = resolvedByActorId;
        fraudCase.Whitelisted = req.Whitelist;

        // If whitelisted, restore wallet access
        if (req.Whitelist)
        {
            var wallets = await _uow.Wallets.GetByActorOnlyAsync(fraudCase.ActorId, ct);
            foreach (var w in wallets)
            {
                if (!w.InstantPayEnabled) w.InstantPayEnabled = true;
                w.FraudScore = 0;
                w.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(w);
            }
        }

        _uow.FraudCases.Update(fraudCase);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Fraud, "FRAUD_CASE_RESOLVED",
            resolvedByActorId, ActorRole.FraudOfficer,
            caseId, "FRAUD_CASE", null,
            JsonSerializer.Serialize(new
            {
                caseId, resolutionCode = req.ResolutionCode.ToString(),
                whitelisted = req.Whitelist, req.ResolutionNotes
            }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapCaseToDto(fraudCase);
    }

    public async Task<FraudCaseDto> ArchiveCaseAsync(Guid caseId, Guid archivedByActorId, CancellationToken ct = default)
    {
        var fraudCase = await LoadCaseAsync(caseId, ct);
        if (fraudCase.Status == FraudCaseStatus.Archived)
            throw new InvalidOperationException("Case is already archived.");

        fraudCase.Status = FraudCaseStatus.Archived;
        fraudCase.ArchivedAt = DateTime.UtcNow;
        fraudCase.ArchivedByActorId = archivedByActorId;
        _uow.FraudCases.Update(fraudCase);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Fraud, "FRAUD_CASE_ARCHIVED",
            archivedByActorId, ActorRole.FraudOfficer,
            caseId, "FRAUD_CASE", null,
            JsonSerializer.Serialize(new { caseId }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapCaseToDto(fraudCase);
    }

    public async Task<IEnumerable<FraudCaseDto>> GetOpenCasesAsync(FraudSeverity minSeverity = FraudSeverity.Medium, CancellationToken ct = default)
    {
        var cases = await _uow.FraudCases.GetOpenBySeverityAsync(minSeverity, ct);
        return cases.Select(MapCaseToDto);
    }

    public async Task<IEnumerable<FraudCaseDto>> GetCasesByActorAsync(Guid actorId, CancellationToken ct = default)
    {
        var cases = await _uow.FraudCases.GetByActorAsync(actorId, ct);
        return cases.Select(MapCaseToDto);
    }

    public async Task<FraudCaseDto> GetCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        var fraudCase = await LoadCaseAsync(caseId, ct);
        return MapCaseToDto(fraudCase);
    }

    // ── Rule management ──────────────────────────────────────────────────────

    public async Task<IEnumerable<FraudRuleDto>> GetActiveRulesAsync(string? domain = null, CancellationToken ct = default)
    {
        var rules = await _uow.FraudRules.GetActiveRulesAsync(domain, ct);
        return rules.Select(MapRuleToDto);
    }

    public async Task<FraudRuleDto> GetRuleAsync(string ruleKey, CancellationToken ct = default)
    {
        var rule = await _uow.FraudRules.GetByKeyAsync(ruleKey, ct)
            ?? throw new KeyNotFoundException($"Fraud rule '{ruleKey}' not found.");
        return MapRuleToDto(rule);
    }

    public async Task<FraudRuleDto> UpdateRuleAsync(string ruleKey, UpdateFraudRuleRequest req, CancellationToken ct = default)
    {
        var rule = await _uow.FraudRules.GetByKeyAsync(ruleKey, ct)
            ?? throw new KeyNotFoundException($"Fraud rule '{ruleKey}' not found.");

        if (req.Threshold.HasValue) rule.Threshold = req.Threshold.Value;
        if (req.Severity.HasValue) rule.Severity = req.Severity.Value;
        if (req.AutoAction.HasValue) rule.AutoAction = req.AutoAction.Value;
        if (req.IsActive.HasValue)
        {
            rule.IsActive = req.IsActive.Value;
            if (!req.IsActive.Value) rule.DeactivatedAt = DateTime.UtcNow;
        }
        rule.ChangedByActorId = req.ChangedByActorId;
        _uow.FraudRules.Update(rule);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Fraud, "FRAUD_RULE_UPDATED",
            req.ChangedByActorId, ActorRole.FraudManager,
            rule.Id, "FRAUD_RULE", null,
            JsonSerializer.Serialize(new { ruleKey, req.Threshold, req.Severity, req.AutoAction, req.IsActive }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapRuleToDto(rule);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<FraudCase> LoadCaseAsync(Guid caseId, CancellationToken ct)
        => await _uow.FraudCases.GetByIdAsync(caseId, ct)
            ?? throw new KeyNotFoundException($"Fraud case {caseId} not found.");

    private static FraudRuleDto MapRuleToDto(FraudRule r) => new(
        r.Id, r.RuleKey, r.Description, r.Domain, r.Threshold,
        r.Severity, r.AutoAction, r.WindowHours, r.IsActive);

    private static FraudCaseDto MapCaseToDto(FraudCase c) => new(
        c.Id, c.RuleKey, c.ActorId, c.ActorType.ToString(), c.TriggerEvent,
        c.Severity, c.Status, c.AutoActionTaken, c.AssignedToActorId,
        c.InvestigationNotesJson, c.ResolutionCode, c.ResolutionNotes,
        c.Whitelisted, c.RelatedEntityId, c.RelatedEntityType,
        c.ResolvedAt, c.ArchivedAt, c.CreatedAt);
}
