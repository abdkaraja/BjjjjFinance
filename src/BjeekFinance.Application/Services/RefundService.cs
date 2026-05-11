using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using BjeekFinance.Domain.Exceptions;
using System.Text.Json;

namespace BjeekFinance.Application.Services;

/// <summary>
/// UC-FIN-REFUND-ENGINE-01: Refund Request & Auto-Routing.
/// Full lifecycle: agent submission → pre-flight checks → auto-approval or
/// approval-tier routing → approver review → execution → audit.
/// </summary>
public class RefundService : IRefundService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly IRefundAutoApprovalEngine _autoApproval;
    private readonly IRefundAiRecommendationService _aiRec;

    public RefundService(
        IUnitOfWork uow,
        IAuditService audit,
        IRefundAutoApprovalEngine autoApproval,
        IRefundAiRecommendationService aiRec)
    {
        _uow = uow;
        _audit = audit;
        _autoApproval = autoApproval;
        _aiRec = aiRec;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Step 1: Agent submits refund request
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<RefundDto> SubmitRefundRequestAsync(InitiateRefundRequest req, CancellationToken ct = default)
    {
        var isPartial = req.RefundType == "PARTIAL";
        if (!isPartial && req.RefundType != "FULL")
            throw new ArgumentException($"RefundType must be FULL or PARTIAL. Got: {req.RefundType}.");

        // ── 1. Load original transaction ──────────────────────────────────────
        var originalTxn = await _uow.Transactions.GetByIdAsync(req.OriginalTransactionId, ct)
            ?? throw new KeyNotFoundException($"Transaction {req.OriginalTransactionId} not found.");

        if (originalTxn.IsReversed)
            throw new InvalidOperationException("Transaction has already been fully refunded.");

        // ── 2. Validate refund window ─────────────────────────────────────────
        var serviceType = originalTxn.RideId.HasValue ? "ride" : "food_delivery";
        var refundWindowHours = await _uow.FinanceParameters.GetIntAsync(
            $"refund_window_{serviceType}_hours", serviceType == "ride" ? 168 : 24, null, null, ct);
        var txnAge = DateTime.UtcNow - originalTxn.CreatedAt;
        if (txnAge.TotalHours > refundWindowHours)
            throw new RefundWindowExpiredException(serviceType, refundWindowHours / 24);

        // ── 3. Calculate refund amount & pre-flight checks ────────────────────
        var refundAmount = isPartial
            ? req.PartialAmount ?? throw new ArgumentException("PartialAmount is required for PARTIAL refund.")
            : originalTxn.GrossAmount;

        if (refundAmount <= 0)
            throw new ArgumentException("Refund amount must be positive.");

        var totalRefunded = await _uow.Refunds.GetTotalRefundedAmountAsync(req.OriginalTransactionId, ct);
        var availableForRefund = originalTxn.GrossAmount - totalRefunded;

        // AF1: amount > available-for-refund
        if (refundAmount > availableForRefund)
            throw new RefundAmountExceedsAvailableException(refundAmount, availableForRefund);

        // Pre-flight: justification >= 50 chars
        if (string.IsNullOrWhiteSpace(req.Justification) || req.Justification.Trim().Length < 50)
            throw new RefundPreFlightFailedException("Justification must be at least 50 characters.");

        // AF2: agent authority check (SAR 185 ceiling for standard support agent)
        var agentAuthorityLimit = await _uow.FinanceParameters.GetDecimalAsync(
            "refund_agent_authority_limit", 185m, null, null, ct);
        if (refundAmount > agentAuthorityLimit)
            throw new RefundAgentAuthorityExceededException(agentAuthorityLimit);

        // ── 4. Load customer wallet for VIP tier & fraud score ───────────────
        var customerWallet = await _uow.Wallets.GetByActorAsync(req.UserActorId, ActorType.User, ct)
            ?? throw new KeyNotFoundException($"User wallet for actor {req.UserActorId} not found.");

        var customerVipTier = MapVipTier(customerWallet.LoyaltyPoints);
        var fraudScore = customerWallet.FraudScore;

        // ── 5. Auto-Approval Rule Engine ──────────────────────────────────────
        var autoResult = await _autoApproval.EvaluateAsync(
            refundAmount, customerVipTier, fraudScore, req.RefundCategory,
            originalTxn.RideId.HasValue ? customerWallet.CityId : null,
            serviceType, ct);

        // ── 6. Calculate proportional commission reversal ─────────────────────
        var commissionReversal = isPartial
            ? Math.Round(refundAmount / originalTxn.GrossAmount * originalTxn.CommissionAmount, 2)
            : originalTxn.CommissionAmount;

        var vatReversal = isPartial
            ? Math.Round(refundAmount / originalTxn.GrossAmount * originalTxn.VatAmount, 2)
            : originalTxn.VatAmount;

        var netDebit = isPartial
            ? Math.Round(refundAmount / originalTxn.GrossAmount * originalTxn.NetAmount, 2)
            : originalTxn.NetAmount;

        var destinationMethod = originalTxn.PaymentMethod switch
        {
            PaymentMethod.Card or PaymentMethod.PartialWalletCard => PayoutDestinationType.DebitCard,
            _ => PayoutDestinationType.SaudiIban
        };

        // ── 7. Determine approval tier & generate AI recommendation ──────────
        ApprovalTier? approvalTier = null;
        string? aiRecJson = null;
        var isAutoApproved = autoResult.IsAutoApproved;

        if (!isAutoApproved)
        {
            approvalTier = ResolveApprovalTier(refundAmount);
            aiRecJson = await GenerateAiRecommendationAsync(
                refundAmount, customerVipTier, fraudScore, req.RefundCategory,
                req.UserActorId, ct);
        }

        // ── 8. SLA target hours config ────────────────────────────────────────
        var slaHours = await _uow.FinanceParameters.GetIntAsync(
            $"refund_sla_hours_{approvalTier?.ToString().ToLowerInvariant() ?? "auto"}",
            approvalTier switch
            {
                Domain.Enums.ApprovalTier.FinanceOfficer => 4,
                Domain.Enums.ApprovalTier.FinanceManager => 8,
                Domain.Enums.ApprovalTier.VpFinance => 24,
                Domain.Enums.ApprovalTier.Cfo => 48,
                _ => 4
            }, null, null, ct);

        // ── 9. Create refund record ───────────────────────────────────────────
        var refund = new Refund
        {
            OriginalTransactionId = req.OriginalTransactionId,
            RefundType = req.RefundType,
            Amount = refundAmount,
            PartialAmount = isPartial ? refundAmount : null,
            ItemsRefunded = isPartial ? req.ItemsRefunded : null,
            RefundCategory = req.RefundCategory,
            Justification = req.Justification.Trim(),
            EvidenceUrls = req.EvidenceUrls,
            InitiatedBySupportAgentId = req.InitiatedBySupportAgentId,
            UserActorId = req.UserActorId,
            CustomerVipTier = customerVipTier,
            FraudScoreAtRequest = fraudScore,
            AvailableForRefundBalance = availableForRefund,
            IsAutoApproved = isAutoApproved,
            ApprovalTier = approvalTier,
            AssignedApproverActorId = null,
            AssignedAt = isAutoApproved ? null : DateTime.UtcNow,
            AiRecommendationJson = aiRecJson,
            Status = isAutoApproved ? RefundStatus.AutoApproved : RefundStatus.AwaitingApproval,
            SlaTargetHours = slaHours,
            SlaAssignedAt = isAutoApproved ? null : DateTime.UtcNow,
            CommissionReversalAmount = commissionReversal,
            VatReversalAmount = vatReversal,
            ReasonCode = req.ReasonCode,
            DestinationMethod = destinationMethod,
            ActorId = req.ActorId,
            ActorRole = req.ActorRole,
            UserWalletId = customerWallet?.Id,
            WalletId = originalTxn.WalletId,
            PlatformWalletId = Guid.Empty,
        };

        await _uow.BeginTransactionAsync(ct);
        try
        {
            await _uow.Refunds.AddAsync(refund, ct);

            // If auto-approved, execute the wallet/card operations immediately
            if (isAutoApproved)
            {
                await ExecuteRefundAsync(refund, originalTxn, customerWallet, netDebit, commissionReversal, vatReversal, ct);
                refund.Status = RefundStatus.Completed;
                refund.CompletedAt = DateTime.UtcNow;
            }

            // Audit log
            var auditSubtype = isAutoApproved ? "REFUND_AUTO_APPROVED" : "REFUND_REQUEST_SUBMITTED";
            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Refund, auditSubtype,
                req.ActorId, req.ActorRole,
                refund.Id, "REFUND",
                JsonSerializer.Serialize(new
                {
                    originalTxnId = req.OriginalTransactionId,
                    refundType = req.RefundType,
                    refundAmount,
                    category = req.RefundCategory.ToString(),
                    isAutoApproved,
                    approvalTier = approvalTier?.ToString(),
                    aiRecommendation = aiRecJson
                }),
                JsonSerializer.Serialize(new
                {
                    refundId = refund.Id,
                    destinationMethod,
                    status = refund.Status,
                    customerVipTier = customerVipTier.ToString()
                }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }

        return MapToDto(refund);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Pre-filled form data
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<RefundRequestPreFillDto> GetRefundPreFillAsync(Guid transactionId, CancellationToken ct = default)
    {
        var txn = await _uow.Transactions.GetByIdAsync(transactionId, ct)
            ?? throw new KeyNotFoundException($"Transaction {transactionId} not found.");

        var totalRefunded = await _uow.Refunds.GetTotalRefundedAmountAsync(transactionId, ct);
        var availableForRefund = txn.GrossAmount - totalRefunded;
        var serviceType = txn.RideId.HasValue ? "ride" : "food_delivery";

        return new RefundRequestPreFillDto(
            txn.Id, txn.GrossAmount, totalRefunded, availableForRefund,
            serviceType, txn.CreatedAt, txn.PaymentMethod);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Read operations
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<RefundDto> GetRefundAsync(Guid refundId, CancellationToken ct = default)
    {
        var refund = await _uow.Refunds.GetByIdAsync(refundId, ct)
            ?? throw new KeyNotFoundException($"Refund {refundId} not found.");
        return MapToDto(refund);
    }

    public async Task<RefundDto?> GetRefundByTransactionAsync(Guid transactionId, CancellationToken ct = default)
    {
        var refunds = await _uow.Refunds.GetByOriginalTransactionAsync(transactionId, ct);
        return refunds.FirstOrDefault() is Refund r ? MapToDto(r) : null;
    }

    public async Task<IEnumerable<RefundDto>> GetRefundsByActorAsync(Guid actorId, CancellationToken ct = default)
    {
        var refunds = await _uow.Refunds.GetByActorAsync(actorId, ct);
        return refunds.Select(MapToDto);
    }

    public async Task<IEnumerable<RefundDto>> GetRefundsByStatusAsync(RefundStatus status, CancellationToken ct = default)
    {
        var refunds = await _uow.Refunds.GetByStatusAsync(status, ct);
        return refunds.Select(MapToDto);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Approver queue
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<IEnumerable<RefundApprovalQueueItemDto>> GetPendingApprovalQueueAsync(CancellationToken ct = default)
    {
        var pending = await _uow.Refunds.GetPendingApprovalQueueAsync(ct);
        return pending.Select(r => new RefundApprovalQueueItemDto(
            r.Id, r.Amount, r.RefundCategory, r.CustomerVipTier, r.FraudScoreAtRequest,
            r.Justification.Length > 80 ? r.Justification[..80] + "..." : r.Justification,
            r.CreatedAt,
            (int)(DateTime.UtcNow - r.CreatedAt).TotalHours,
            r.ApprovalTier ?? Domain.Enums.ApprovalTier.FinanceOfficer,
            r.AssignedApproverActorId,
            r.SlaBreachedAt));
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Refund review detail
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<RefundReviewDto> GetRefundReviewAsync(Guid refundId, CancellationToken ct = default)
    {
        var refund = await _uow.Refunds.GetByIdWithIncludesAsync(refundId, ct)
            ?? throw new KeyNotFoundException($"Refund {refundId} not found.");

        var txn = refund.OriginalTransaction;
        var serviceType = txn.RideId.HasValue ? "ride" : "food_delivery";
        var totalRefunded = await _uow.Refunds.GetTotalRefundedAmountAsync(txn.Id, ct);

        // Customer profile data (simplified — in production this would call a user service)
        var refundsLast12Months = (await _uow.Refunds.GetByActorAsync(refund.UserActorId, ct))
            .Where(r => r.CreatedAt >= DateTime.UtcNow.AddMonths(-12))
            .ToList();
        var refundCount = refundsLast12Months.Count;
        var refundRate = refundCount > 0 ? (decimal)refundCount / Math.Max(refundCount, 1) * 100 : 0;

        var ageHours = refund.SlaAssignedAt.HasValue
            ? (int)(DateTime.UtcNow - refund.SlaAssignedAt.Value).TotalHours
            : 0;

        return new RefundReviewDto(
            refund.Id, refund.Amount, refund.ApprovedAdjustedAmount,
            refund.RefundCategory, refund.Justification, refund.EvidenceUrls,
            refund.Status, refund.ApprovalTier, refund.CreatedAt,
            refund.CustomerVipTier,
            LifetimeSpend: 0, // Requires user service integration
            refundCount, refundRate, refund.FraudScoreAtRequest,
            refund.AiRecommendationJson,
            DriverName: null, // Requires driver service integration
            DriverRating: 0,
            txn.GrossAmount, serviceType, txn.PaymentMethod, txn.CreatedAt,
            totalRefunded, refund.AvailableForRefundBalance,
            InitiatedByAgentName: refund.InitiatedBySupportAgentId.ToString(),
            refund.CreatedAt, ageHours,
            refund.SlaTargetHours, refund.SlaAssignedAt,
            refund.SlaReminderSentAt, refund.SlaBreachedAt);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Approve refund
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<RefundDto> ApproveRefundAsync(Guid refundId, ApproveRefundRequest req, CancellationToken ct = default)
    {
        var refund = await _uow.Refunds.GetByIdWithIncludesAsync(refundId, ct)
            ?? throw new KeyNotFoundException($"Refund {refundId} not found.");

        if (refund.Status != RefundStatus.AwaitingApproval && refund.Status != RefundStatus.RequestMoreInfo)
            throw new RefundAlreadyCompletedException(refundId);

        var txn = refund.OriginalTransaction;
        var approvalAmount = req.AdjustedAmount ?? refund.Amount;

        var customerWallet = await _uow.Wallets.GetByActorAsync(refund.UserActorId, ActorType.User, ct);
        var driverWallet = await _uow.Wallets.GetByIdWithLockAsync(refund.WalletId, ct)
            ?? throw new KeyNotFoundException($"Driver wallet {refund.WalletId} not found.");

        // Recalculate proportional amounts if amount was adjusted
        var (commissionReversal, vatReversal, netDebit) = RecalculateProportionalAmounts(
            approvalAmount, txn, refund.Amount, refund.CommissionReversalAmount, refund.VatReversalAmount);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // Execute the refund with potentially adjusted amounts
            await ExecuteRefundAsync(refund, txn, customerWallet, netDebit, commissionReversal, vatReversal, ct);

            refund.Status = RefundStatus.Approved;
            refund.FinalDecision = "APPROVE";
            refund.ApprovedAdjustedAmount = req.AdjustedAmount;
            refund.ApprovedByActorId = req.ApproverActorId;
            refund.ApprovedAt = DateTime.UtcNow;
            refund.ApproverNotes = req.Notes;
            refund.CustomerWalletDelta = customerWallet?.BalanceRefundCredit;
            refund.WarningSentToDriver = req.WarnDriver;

            // Mark completed if wallet was credited (card reversals stay in Processing)
            refund.Status = refund.DestinationMethod == PayoutDestinationType.DebitCard
                ? RefundStatus.Processing
                : RefundStatus.Completed;
            refund.CompletedAt = refund.Status == RefundStatus.Completed ? DateTime.UtcNow : null;

            // Auto-close ticket notification (integration point)
            // TODO: Notify customer (push + email, Arabic primary)
            // TODO: Auto-close support ticket
            // TODO: If WarnDriver, send driver warning notification

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Refund, "REFUND_APPROVED",
                req.ApproverActorId, req.ApproverRole,
                refund.Id, "REFUND",
                JsonSerializer.Serialize(new { previousStatus = RefundStatus.AwaitingApproval }),
                JsonSerializer.Serialize(new
                {
                    refundId,
                    approvedAmount = approvalAmount,
                    adjustedAmount = req.AdjustedAmount,
                    warnDriver = req.WarnDriver
                }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }

        return MapToDto(refund);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Deny refund
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<RefundDto> DenyRefundAsync(Guid refundId, DenyRefundRequest req, CancellationToken ct = default)
    {
        var refund = await _uow.Refunds.GetByIdAsync(refundId, ct)
            ?? throw new KeyNotFoundException($"Refund {refundId} not found.");

        if (refund.Status != RefundStatus.AwaitingApproval && refund.Status != RefundStatus.RequestMoreInfo)
            throw new RefundAlreadyCompletedException(refundId);

        refund.Status = RefundStatus.Rejected;
        refund.FinalDecision = "DENY";
        refund.RejectionReasonCode = req.RejectionReasonCode;
        refund.ApprovedByActorId = req.ApproverActorId;
        refund.ApprovedAt = DateTime.UtcNow;
        refund.ApproverNotes = req.Notes;

        // TODO: Notify customer with reason code
        // TODO: Close support ticket

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Refund, "REFUND_DENIED",
            req.ApproverActorId, req.ApproverRole,
            refund.Id, "REFUND",
            JsonSerializer.Serialize(new { previousStatus = RefundStatus.AwaitingApproval }),
            JsonSerializer.Serialize(new
            {
                refundId,
                rejectionReasonCode = req.RejectionReasonCode,
                notes = req.Notes
            }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapToDto(refund);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Request more info (AF3)
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<RefundDto> RequestMoreInfoAsync(Guid refundId, RequestMoreInfoRefundRequest req, CancellationToken ct = default)
    {
        var refund = await _uow.Refunds.GetByIdAsync(refundId, ct)
            ?? throw new KeyNotFoundException($"Refund {refundId} not found.");

        if (refund.Status != RefundStatus.AwaitingApproval)
            throw new RefundAlreadyCompletedException(refundId);

        refund.Status = RefundStatus.RequestMoreInfo;
        refund.FinalDecision = "REQUEST_MORE_INFO";
        refund.RequestMoreInfoNotes = req.Notes;
        refund.RequestMoreInfoRequestedAt = DateTime.UtcNow;
        refund.SlaPausedAt = DateTime.UtcNow;

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Refund, "REFUND_REQUEST_MORE_INFO",
            req.ApproverActorId, req.ApproverRole,
            refund.Id, "REFUND",
            JsonSerializer.Serialize(new { previousStatus = RefundStatus.AwaitingApproval }),
            JsonSerializer.Serialize(new { notes = req.Notes }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapToDto(refund);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Respond to more info (AF3)
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<RefundDto> RespondToMoreInfoAsync(Guid refundId, RespondMoreInfoRefundRequest req, CancellationToken ct = default)
    {
        var refund = await _uow.Refunds.GetByIdAsync(refundId, ct)
            ?? throw new KeyNotFoundException($"Refund {refundId} not found.");

        if (refund.Status != RefundStatus.RequestMoreInfo)
            throw new InvalidOperationException("Refund is not in RequestMoreInfo state.");

        refund.Status = RefundStatus.AwaitingApproval;
        refund.RequestMoreInfoRespondedAt = DateTime.UtcNow;
        refund.SlaResumedAt = DateTime.UtcNow;
        refund.SlaPausedAt = null;

        // Prepend response to justification for audit trail
        refund.Justification += $"\n[Agent response on {DateTime.UtcNow:O}]: {req.AdditionalInfo}";

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Refund, "REFUND_MORE_INFO_RESPONDED",
            req.SupportAgentActorId, req.SupportAgentRole,
            refund.Id, "REFUND",
            null,
            JsonSerializer.Serialize(new { additionalInfo = req.AdditionalInfo }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapToDto(refund);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // SLA: Process reminders (AF4)
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<IEnumerable<RefundDto>> ProcessSlaRemindersAsync(CancellationToken ct = default)
    {
        var due = await _uow.Refunds.GetDueSlaRemindersAsync(ct);
        var results = new List<RefundDto>();

        foreach (var refund in due)
        {
            refund.SlaReminderSentAt = DateTime.UtcNow;
            // TODO: Send in-app reminder to assigned approver
            results.Add(MapToDto(refund));
        }

        if (results.Count > 0)
            await _uow.SaveChangesAsync(ct);

        return results;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // SLA: Auto-escalate breached (AF5)
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<IEnumerable<RefundDto>> ProcessSlaEscalationsAsync(CancellationToken ct = default)
    {
        var breached = await _uow.Refunds.GetSlaBreachedNotEscalatedAsync(ct);
        var results = new List<RefundDto>();

        foreach (var refund in breached)
        {
            var currentTier = refund.ApprovalTier ?? Domain.Enums.ApprovalTier.FinanceOfficer;
            var nextTier = GetNextTier(currentTier);

            refund.SlaBreachedAt = DateTime.UtcNow;
            refund.EscalatedFromActorId = refund.AssignedApproverActorId;
            refund.EscalatedFromTier = currentTier;

            if (nextTier.HasValue)
            {
                refund.ApprovalTier = nextTier.Value;
                refund.AssignedApproverActorId = null;
                refund.AssignedAt = DateTime.UtcNow;
                refund.SlaAssignedAt = DateTime.UtcNow;
                refund.SlaReminderSentAt = null;
                refund.SlaBreachedAt = null;
            }

            // TODO: Notify breached approver and their manager
            // TODO: Notify new tier approver of escalation

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Refund, "REFUND_SLA_ESCALATED",
                Guid.Empty, ActorRole.System,
                refund.Id, "REFUND",
                JsonSerializer.Serialize(new { previousTier = currentTier.ToString() }),
                JsonSerializer.Serialize(new { escalatedToTier = nextTier?.ToString(), slaBreachedAt = refund.SlaBreachedAt }),
                null, null, null), ct);

            results.Add(MapToDto(refund));
        }

        if (results.Count > 0)
            await _uow.SaveChangesAsync(ct);

        return results;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // EX1: Retry wallet credit for processing refund
    // ═══════════════════════════════════════════════════════════════════════════════
    public async Task<RefundDto> RetryWalletCreditAsync(Guid refundId, CancellationToken ct = default)
    {
        var refund = await _uow.Refunds.GetByIdWithIncludesAsync(refundId, ct)
            ?? throw new KeyNotFoundException($"Refund {refundId} not found.");

        if (refund.Status != RefundStatus.Processing)
            throw new InvalidOperationException($"Refund {refundId} is not in Processing state.");

        if (refund.RetryCount >= 3)
            throw new RefundWalletCreditFailedException(refundId, "Maximum retry attempts (3) reached. Admin intervention required.");

        var customerWallet = await _uow.Wallets.GetByActorAsync(refund.UserActorId, ActorType.User, ct);
        var txn = refund.OriginalTransaction;

        refund.RetryCount++;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            if (refund.DestinationMethod != PayoutDestinationType.DebitCard && customerWallet != null)
            {
                customerWallet.BalanceRefundCredit += refund.Amount;
                customerWallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(customerWallet);
                refund.Status = RefundStatus.Completed;
                refund.CompletedAt = DateTime.UtcNow;
            }

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Refund, "REFUND_RETRY_WALLET_CREDIT",
                Guid.Empty, ActorRole.System,
                refund.Id, "REFUND",
                null,
                JsonSerializer.Serialize(new { retryCount = refund.RetryCount, newStatus = refund.Status }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }

        return MapToDto(refund);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════════════════════════

    private async Task ExecuteRefundAsync(
        Refund refund, Transaction originalTxn, Wallet? customerWallet,
        decimal netDebit, decimal commissionReversal, decimal vatReversal,
        CancellationToken ct)
    {
        // Credit user via original payment method
        if (originalTxn.PaymentMethod == PaymentMethod.Wallet && customerWallet != null)
        {
            customerWallet.BalanceRefundCredit += refund.Amount;
            customerWallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(customerWallet);
        }
        // Card → gateway reversal (T+3) - TODO: gateway integration

        // Debit driver/merchant wallet
        var driverWallet = await _uow.Wallets.GetByIdWithLockAsync(refund.WalletId, ct)
            ?? throw new KeyNotFoundException($"Driver wallet {refund.WalletId} not found.");

        if (driverWallet.BalanceAvailable >= netDebit)
        {
            driverWallet.BalanceAvailable -= netDebit;
        }
        else
        {
            var shortfall = netDebit - driverWallet.BalanceAvailable;
            driverWallet.BalanceAvailable = 0;
            driverWallet.CashReceivable += shortfall;

            if (!driverWallet.IsInDunning)
            {
                driverWallet.IsInDunning = true;
                driverWallet.DunningStartedAt = DateTime.UtcNow;
                driverWallet.DunningBucket = Domain.Enums.DunningBucket.Notify;
            }
        }
        driverWallet.UpdatedAt = DateTime.UtcNow;
        _uow.Wallets.Update(driverWallet);

        // Platform wallet - commission reversal
        var platformWallet = await _uow.Wallets.GetByActorAsync(Guid.Empty, ActorType.Platform, ct);
        if (platformWallet != null)
        {
            platformWallet.BalanceAvailable -= commissionReversal;
            platformWallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(platformWallet);
        }

        // Mark original transaction reversed if full refund or cumulative reaches gross
        if (refund.RefundType == "FULL")
        {
            originalTxn.IsReversed = true;
            originalTxn.UpdatedAt = DateTime.UtcNow;
            _uow.Transactions.Update(originalTxn);
        }
        else
        {
            var cumulativeAfter = await _uow.Refunds.GetTotalRefundedAmountAsync(originalTxn.Id, ct) + refund.Amount;
            if (Math.Abs(cumulativeAfter - originalTxn.GrossAmount) < 0.01m)
            {
                originalTxn.IsReversed = true;
                originalTxn.UpdatedAt = DateTime.UtcNow;
                _uow.Transactions.Update(originalTxn);
            }
        }
    }

    private ApprovalTier ResolveApprovalTier(decimal amount)
    {
        // Authority Matrix (Section 12.2):
        // SAR 0 - 1,000: Finance Officer
        // SAR 1,001 - 5,000: Finance Manager
        // SAR 5,001 - 25,000: VP Finance
        // SAR 25,001+: CFO
        return amount switch
        {
            <= 1000 => Domain.Enums.ApprovalTier.FinanceOfficer,
            <= 5000 => Domain.Enums.ApprovalTier.FinanceManager,
            <= 25000 => Domain.Enums.ApprovalTier.VpFinance,
            _ => Domain.Enums.ApprovalTier.Cfo
        };
    }

    private static ApprovalTier? GetNextTier(ApprovalTier current) => current switch
    {
        Domain.Enums.ApprovalTier.FinanceOfficer => Domain.Enums.ApprovalTier.FinanceManager,
        Domain.Enums.ApprovalTier.FinanceManager => Domain.Enums.ApprovalTier.VpFinance,
        Domain.Enums.ApprovalTier.VpFinance => Domain.Enums.ApprovalTier.Cfo,
        Domain.Enums.ApprovalTier.Cfo => null,
        _ => null
    };

    private static CustomerVipTier MapVipTier(int loyaltyPoints) => loyaltyPoints switch
    {
        >= 5000 => CustomerVipTier.Platinum,
        >= 2000 => CustomerVipTier.Gold,
        >= 500 => CustomerVipTier.Silver,
        _ => CustomerVipTier.Standard
    };

    private async Task<string> GenerateAiRecommendationAsync(
        decimal amount, CustomerVipTier vipTier, int fraudScore,
        RefundCategory category, Guid userActorId, CancellationToken ct)
    {
        var refundsLast12Months = (await _uow.Refunds.GetByActorAsync(userActorId, ct))
            .Where(r => r.CreatedAt >= DateTime.UtcNow.AddMonths(-12))
            .ToList();
        var refundCount = refundsLast12Months.Count;
        var refundRate = refundCount > 0 ? (decimal)refundCount / Math.Max(refundCount, 1) * 100 : 0;

        var rec = await _aiRec.GenerateAsync(
            amount, vipTier, fraudScore, category, refundCount, refundRate, 0, ct);

        return _aiRec.Serialize(rec);
    }

    private static (decimal commissionReversal, decimal vatReversal, decimal netDebit) RecalculateProportionalAmounts(
        decimal approvalAmount, Transaction txn, decimal originalRefundAmount,
        decimal originalCommissionReversal, decimal originalVatReversal)
    {
        if (Math.Abs(approvalAmount - originalRefundAmount) < 0.01m)
            return (originalCommissionReversal, originalVatReversal,
                Math.Round(approvalAmount / txn.GrossAmount * txn.NetAmount, 2));

        var ratio = approvalAmount / txn.GrossAmount;
        return (
            Math.Round(ratio * txn.CommissionAmount, 2),
            Math.Round(ratio * txn.VatAmount, 2),
            Math.Round(ratio * txn.NetAmount, 2));
    }

    private static RefundDto MapToDto(Refund r) => new(
        r.Id, r.OriginalTransactionId, r.RefundType, r.Amount,
        r.PartialAmount, r.ItemsRefunded,
        r.RefundCategory, r.Justification, r.EvidenceUrls,
        r.InitiatedBySupportAgentId, r.UserActorId,
        r.CustomerVipTier, r.FraudScoreAtRequest, r.AvailableForRefundBalance,
        r.IsAutoApproved, r.ApprovalTier, r.AssignedApproverActorId, r.AssignedAt,
        r.AiRecommendationJson,
        r.FinalDecision, r.ApprovedAdjustedAmount, r.ApproverNotes,
        r.CommissionReversalAmount, r.VatReversalAmount, r.ReasonCode,
        r.DestinationMethod, r.Status, r.ActorId, r.ActorRole,
        r.PspReversalReference, r.ActorId,
        r.CustomerWalletDelta, r.WarningSentToDriver,
        r.RequestMoreInfoNotes, r.RequestMoreInfoRequestedAt, r.RequestMoreInfoRespondedAt,
        r.SlaTargetHours, r.SlaAssignedAt, r.SlaReminderSentAt, r.SlaBreachedAt,
        r.SlaPausedAt, r.SlaResumedAt,
        r.EscalatedFromActorId, r.EscalatedFromTier,
        r.SupportTicketId, r.RetryCount,
        r.CreatedAt, r.CompletedAt);
}
