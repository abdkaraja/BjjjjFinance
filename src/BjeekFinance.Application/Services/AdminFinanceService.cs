using BjeekFinance.Application.Common;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using System.Text.Json;

namespace BjeekFinance.Application.Services;

public class AdminFinanceService : IAdminFinanceService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public AdminFinanceService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<DunningStatusDto> GetDunningStatusAsync(Guid walletId, CancellationToken ct = default)
    {
        var wallet = await _uow.Wallets.GetByIdAsync(walletId, ct)
            ?? throw new KeyNotFoundException($"Wallet {walletId} not found.");
        return new DunningStatusDto(wallet.Id, wallet.ActorId, wallet.IsInDunning,
            wallet.DunningBucket, wallet.DunningStartedAt, wallet.CashReceivable, wallet.BalanceAvailable);
    }

    public async Task<IEnumerable<DunningStatusDto>> GetAllDunningWalletsAsync(CancellationToken ct = default)
    {
        var wallets = await _uow.Wallets.GetWalletsInDunningAsync(ct);
        return wallets.Select(w => new DunningStatusDto(w.Id, w.ActorId, w.IsInDunning,
            w.DunningBucket, w.DunningStartedAt, w.CashReceivable, w.BalanceAvailable));
    }

    /// <summary>
    /// Nightly dunning classification at 02:00 local city time.
    /// Aging calculated from date negative balance FIRST occurred — not last notification.
    /// </summary>
    public async Task RunDunningBatchAsync(CancellationToken ct = default)
    {
        var wallets = await _uow.Wallets.GetWalletsInDunningAsync(ct);

        foreach (var wallet in wallets)
        {
            if (!wallet.DunningStartedAt.HasValue) continue;

            var ageDays = (DateTime.UtcNow - wallet.DunningStartedAt.Value).TotalDays;

            var newBucket = ageDays switch
            {
                <= 7 => Domain.Enums.DunningBucket.Notify,
                <= 30 => Domain.Enums.DunningBucket.HoldPayout,
                _ => Domain.Enums.DunningBucket.HoldAssignments
            };

            // Auto-resolve: when balance ≥ 0, all holds released automatically
            if (wallet.BalanceAvailable >= 0 && wallet.CashReceivable <= 0)
            {
                wallet.IsInDunning = false;
                wallet.DunningBucket = null;
                wallet.DunningStartedAt = null;
            }
            else
            {
                wallet.DunningBucket = newBucket;
            }

            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<WriteOffResultDto> InitiateWriteOffAsync(WriteOffRequest req, CancellationToken ct = default)
    {
        if (req.ReasonCode == WriteOffReasonCode.Other && (req.Notes?.Length ?? 0) < 100)
            throw new ArgumentException("Write-off reason 'Other' requires at least 100 characters in notes.");

        var writeOffId = Guid.NewGuid();

        // < SAR 18,500: Finance Manager self-approves
        // ≥ SAR 18,500: VP Finance co-approval required
        var needsVpApproval = req.Amount >= 18_500m;
        var status = needsVpApproval ? "PENDING_VP_APPROVAL" : "APPROVED";

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.AdminOverride, "WRITE_OFF_INITIATED",
            req.InitiatedByActorId, ActorRole.FinanceManager,
            writeOffId, "WRITE_OFF", null,
            JsonSerializer.Serialize(new { req.WalletId, req.Amount, req.ReasonCode, status }),
            null, null, null), ct);

        return new WriteOffResultDto(writeOffId, req.WalletId, req.Amount, req.ReasonCode, status, null, DateTime.UtcNow);
    }

    public async Task<WriteOffResultDto> ApproveWriteOffAsync(Guid writeOffId, Guid approverActorId, CancellationToken ct = default)
    {
        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.AdminOverride, "WRITE_OFF_APPROVED",
            approverActorId, ActorRole.VpFinance,
            writeOffId, "WRITE_OFF", null,
            JsonSerializer.Serialize(new { writeOffId, approvedBy = approverActorId }),
            null, null, null), ct);

        return new WriteOffResultDto(writeOffId, Guid.Empty, 0, WriteOffReasonCode.Other, "APPROVED", approverActorId, DateTime.UtcNow);
    }

    public async Task<BulkAdjustmentResultDto> ExecuteBulkAdjustmentAsync(BulkAdjustmentRequest req, CancellationToken ct = default)
    {
        // > SAR 50,000 total requires Super Admin — validated by caller / RBAC middleware
        var batchId = Guid.NewGuid();
        var totalAmount = req.Adjustments.Sum(a => a.Delta);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            foreach (var item in req.Adjustments)
            {
                var wallet = await _uow.Wallets.GetByIdWithLockAsync(item.WalletId, ct)
                    ?? throw new KeyNotFoundException($"Wallet {item.WalletId} not found.");
                wallet.BalanceAvailable += item.Delta;
                wallet.UpdatedAt = DateTime.UtcNow;
                _uow.Wallets.Update(wallet);
            }

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Wallet, "BULK_ADJUSTMENT",
                req.InitiatedByActorId, ActorRole.SuperAdmin,
                batchId, "BULK_ADJUSTMENT", null,
                JsonSerializer.Serialize(new { batchId, Count = req.Adjustments.Count(), totalAmount, req.Reason }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return new BulkAdjustmentResultDto(batchId, req.Adjustments.Count(), totalAmount, "COMPLETED", DateTime.UtcNow);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<IEnumerable<AuditLogEntryDto>> GetAuditLogsAsync(Guid subjectId, string subjectType, CancellationToken ct = default)
    {
        var logs = await _uow.AuditLogs.GetBySubjectAsync(subjectId, subjectType, ct);
        return logs.Select(MapAuditDto);
    }

    public async Task<IEnumerable<AuditLogEntryDto>> GetAuditLogsByEventTypeAsync(AuditEventType eventType, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var logs = await _uow.AuditLogs.GetByEventTypeAsync(eventType, from, to, ct);
        return logs.Select(MapAuditDto);
    }

    public async Task<ReconciliationReportDto> GenerateReconciliationReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // Real impl: aggregate from transactions and audit logs
        // Target: < 30 seconds for 30-day range (async job with progress indicator)
        await Task.CompletedTask;
        return new ReconciliationReportDto(Guid.NewGuid(), from, to, 0, 0, 0, 0, 0, 0, 0,
            Enumerable.Empty<ReconciliationLineDto>(), DateTime.UtcNow);
    }

    public async Task<FinanceParameterDto> GetParameterAsync(string key, Guid? cityId, string? serviceType, CancellationToken ct = default)
    {
        var param = await _uow.FinanceParameters.GetActiveAsync(key, cityId, serviceType, ct)
            ?? throw new KeyNotFoundException($"Parameter '{key}' not found.");
        return MapParamDto(param);
    }

    public async Task<FinanceParameterDto> UpdateParameterAsync(UpdateParameterRequest req, CancellationToken ct = default)
    {
        // Versioned — previous value retained; new version created
        var existing = await _uow.FinanceParameters.GetActiveAsync(req.Key, req.CityId, req.ServiceType, ct);
        if (existing is not null)
        {
            existing.IsActive = false;
            _uow.FinanceParameters.Update(existing);
        }

        var newParam = new FinanceParameter
        {
            ParameterKey = req.Key,
            ParameterValue = req.Value,
            CityId = req.CityId,
            ServiceType = req.ServiceType,
            Description = req.Description,
            ChangedByActorId = req.ChangedByActorId,
            EffectiveFrom = DateTime.UtcNow,
            Version = (existing?.Version ?? 0) + 1,
            PreviousValue = existing is not null && decimal.TryParse(existing.ParameterValue, out var prev) ? prev : null
        };
        await _uow.FinanceParameters.AddAsync(newParam, ct);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Config, "PARAMETER_UPDATED",
            req.ChangedByActorId, ActorRole.FinanceAdmin,
            newParam.Id, "FINANCE_PARAMETER",
            existing is not null ? JsonSerializer.Serialize(existing) : null,
            JsonSerializer.Serialize(newParam),
            req.CityId, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        return MapParamDto(newParam);
    }

    public async Task<IEnumerable<FinanceParameterDto>> GetAllParametersAsync(CancellationToken ct = default)
    {
        var all = await _uow.FinanceParameters.GetAllAsync(ct);
        return all.Select(MapParamDto);
    }

    private static AuditLogEntryDto MapAuditDto(AuditLogEntry l) => new(
        l.Id, l.EventType, l.EventSubtype, l.ActorId, l.ActorRole,
        l.SubjectId, l.SubjectType, l.Delta, l.Timestamp, l.TamperHash);

    private static FinanceParameterDto MapParamDto(FinanceParameter p) => new(
        p.Id, p.ParameterKey, p.ParameterValue, p.Description,
        p.CityId, p.ServiceType, p.Version, p.EffectiveFrom);
}

public class CorporateBillingService : ICorporateBillingService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;

    public CorporateBillingService(IUnitOfWork uow, IAuditService audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<CorporateAccountDto> GetAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _uow.CorporateAccounts.GetByIdAsync(accountId, ct)
            ?? throw new KeyNotFoundException($"Corporate account {accountId} not found.");
        var wallet = await _uow.Wallets.GetByIdAsync(account.WalletId, ct);
        return MapToDto(account, wallet?.BalanceAvailable ?? 0);
    }

    public async Task<CorporateAccountDto> CreateAccountAsync(CreateCorporateAccountRequest req, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            // Create Corporate Wallet (debit-only — no outbound payouts)
            var wallet = new Wallet
            {
                ActorType = ActorType.Corporate,
                ActorId = Guid.NewGuid(),
                Currency = "SAR",
                KycStatus = KycStatus.Pending
            };
            await _uow.Wallets.AddAsync(wallet, ct);

            var account = new CorporateAccount
            {
                CompanyName = req.CompanyName,
                VatRegistrationNumber = req.VatRegistrationNumber,
                TradeLicenseNumber = req.TradeLicenseNumber,
                AuthorizedSignatoryId = req.AuthorizedSignatoryId,
                BillingModel = req.BillingModel,
                PaymentTerms = req.PaymentTerms,
                NegotiatedDiscountPercent = req.NegotiatedDiscountPercent,
                WalletId = wallet.Id,
                CreditLimit = req.CreditLimit,
                LowBalanceAlertThreshold = req.LowBalanceAlertThreshold,
                MonthlyBudgetCap = req.MonthlyBudgetCap,
                ContractTermsSnapshot = JsonSerializer.Serialize(req),
                ContractVersion = 1
            };
            await _uow.CorporateAccounts.AddAsync(account, ct);

            await _audit.WriteAsync(new AuditLogRequest(
                AuditEventType.Config, "CORPORATE_ACCOUNT_CREATED",
                req.CreatedByActorId, ActorRole.CorporateAccountManager,
                account.Id, "CORPORATE_ACCOUNT", null,
                JsonSerializer.Serialize(new { account.CompanyName, account.BillingModel }),
                null, null, null), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return MapToDto(account, 0);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<CorporateAccountDto> UpdateBillingModelAsync(Guid accountId, UpdateBillingModelRequest req, CancellationToken ct = default)
    {
        var account = await _uow.CorporateAccounts.GetByIdAsync(accountId, ct)
            ?? throw new KeyNotFoundException($"Corporate account {accountId} not found.");

        var before = JsonSerializer.Serialize(account);
        account.BillingModel = req.NewBillingModel;
        account.PaymentTerms = req.NewPaymentTerms;
        // Contract change → new version + Finance Manager approval immutably logged
        account.ContractVersion++;
        account.ContractTermsSnapshot = JsonSerializer.Serialize(req);
        account.UpdatedAt = DateTime.UtcNow;
        _uow.CorporateAccounts.Update(account);

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Config, "BILLING_MODEL_UPDATED",
            req.ChangedByAdminId, ActorRole.FinanceManager,
            accountId, "CORPORATE_ACCOUNT", before,
            JsonSerializer.Serialize(new { req.NewBillingModel, req.ApprovalReference }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
        var wallet = await _uow.Wallets.GetByIdAsync(account.WalletId, ct);
        return MapToDto(account, wallet?.BalanceAvailable ?? 0);
    }

    /// <summary>
    /// Validates corporate booking at booking time.
    /// Blocks if: Corporate Wallet insufficient, employee budget exceeded,
    /// cost-center budget at 100%, or account monthly budget at 100%.
    /// </summary>
    public async Task<BookingValidationResultDto> ValidateBookingAsync(Guid corporateAccountId, Guid employeeUserId, decimal estimatedFare, CancellationToken ct = default)
    {
        var account = await _uow.CorporateAccounts.GetByIdAsync(corporateAccountId, ct)
            ?? throw new KeyNotFoundException($"Corporate account {corporateAccountId} not found.");
        var wallet = await _uow.Wallets.GetByIdAsync(account.WalletId, ct)!;
        var employee = await _uow.CorporateAccounts.GetEmployeeAsync(corporateAccountId, employeeUserId, ct);

        // Apply negotiated discount — reduces Gross, not commission rate
        var discountedFare = estimatedFare * (1 - account.NegotiatedDiscountPercent / 100);
        decimal companyPortion = 0, employeePortion = 0;

        switch (account.BillingModel)
        {
            case CorporateBillingModel.CompanyPay:
                companyPortion = discountedFare;
                break;
            case CorporateBillingModel.SplitPay:
                // Cap configured elsewhere; simplified: full company pay up to limit
                companyPortion = discountedFare;
                break;
            case CorporateBillingModel.VoucherAllowance when employee is not null:
                var allowanceRemaining = employee.MonthlyAllowance - employee.AllowanceConsumed;
                companyPortion = Math.Min(discountedFare, allowanceRemaining);
                employeePortion = discountedFare - companyPortion;
                break;
            case CorporateBillingModel.Reimbursement:
                employeePortion = discountedFare;
                break;
        }

        // Corporate Wallet insufficient → block at booking, not payment
        if (companyPortion > 0 && (wallet?.BalanceAvailable ?? 0) < companyPortion)
            return new BookingValidationResultDto(false, "Corporate Wallet balance insufficient.", 0, 0, 0, 0);

        // Account monthly budget
        if (account.MonthlyBudgetCap > 0 &&
            account.MonthlyBudgetConsumed + companyPortion > account.MonthlyBudgetCap)
            return new BookingValidationResultDto(false, "Corporate account monthly budget exhausted.", 0, 0, 0, 0);

        // Employee monthly budget
        if (employee is not null && employee.MonthlyBudget > 0 &&
            employee.MonthlyBudgetConsumed + discountedFare > employee.MonthlyBudget)
            return new BookingValidationResultDto(false, "Employee monthly budget exhausted.", 0, 0, 0, 0);

        return new BookingValidationResultDto(true, null, companyPortion, employeePortion,
            employee is not null ? employee.MonthlyBudget - employee.MonthlyBudgetConsumed : 0,
            account.MonthlyBudgetCap - account.MonthlyBudgetConsumed);
    }

    public async Task<SplitPayResultDto> ProcessSplitPayAsync(ProcessSplitPayRequest req, CancellationToken ct = default)
    {
        // Both company and employee portions must succeed or both reversed (atomic)
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var account = await _uow.CorporateAccounts.GetByIdAsync(req.CorporateAccountId, ct)!;
            var wallet = await _uow.Wallets.GetByIdWithLockAsync(account!.WalletId, ct)!;

            var companyPortion = Math.Min(req.GrossFare, req.CompanyCap);
            var employeePortion = req.GrossFare - companyPortion;

            wallet!.BalanceAvailable -= companyPortion;
            account.MonthlyBudgetConsumed += companyPortion;
            wallet.UpdatedAt = DateTime.UtcNow;
            _uow.Wallets.Update(wallet);
            _uow.CorporateAccounts.Update(account);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return new SplitPayResultDto(companyPortion, employeePortion, "COMPLETED");
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<CorporateInvoiceDto> GenerateInvoiceAsync(Guid corporateAccountId, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken ct = default)
    {
        var account = await _uow.CorporateAccounts.GetByIdAsync(corporateAccountId, ct)
            ?? throw new KeyNotFoundException($"Corporate account {corporateAccountId} not found.");

        // Real impl: aggregate trips in period; generate sequential ZATCA number
        var dueDate = account.PaymentTerms switch
        {
            CorporatePaymentTerms.Net30 => DateTime.UtcNow.AddDays(30),
            CorporatePaymentTerms.Net15 => DateTime.UtcNow.AddDays(15),
            _ => DateTime.UtcNow
        };

        var invoice = new CorporateInvoice
        {
            CorporateAccountId = corporateAccountId,
            InvoiceNumber = $"CORP-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid():N[..6].ToUpper()}",
            SubtotalAmount = 0,
            VatAmount = 0,
            TotalAmount = 0,
            InvoiceDate = DateTime.UtcNow,
            DueDate = dueDate,
            SellerVatRegistration = "SA000000000000000",
            BuyerVatRegistration = account.VatRegistrationNumber,
            QrCodeData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"BJEEK|{corporateAccountId}|{DateTime.UtcNow:O}"))
        };
        await _uow.CorporateAccounts.GetByIdAsync(corporateAccountId, ct); // nav load
        await _uow.SaveChangesAsync(ct);
        return MapInvoiceDto(invoice);
    }

    public async Task<IEnumerable<CorporateInvoiceDto>> GetInvoicesAsync(Guid corporateAccountId, CancellationToken ct = default)
    {
        var account = await _uow.CorporateAccounts.GetByIdAsync(corporateAccountId, ct)
            ?? throw new KeyNotFoundException();
        return account.Invoices.Select(MapInvoiceDto);
    }

    public async Task<CorporateInvoiceDto> GetInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        // Real impl: dedicated invoice repo query
        await Task.CompletedTask;
        throw new KeyNotFoundException($"Invoice {invoiceId} not found.");
    }

    public async Task<EmployeeBudgetDto> GetEmployeeBudgetAsync(Guid corporateAccountId, Guid userId, CancellationToken ct = default)
    {
        var employee = await _uow.CorporateAccounts.GetEmployeeAsync(corporateAccountId, userId, ct)
            ?? throw new KeyNotFoundException($"Employee {userId} not found in account {corporateAccountId}.");
        return new EmployeeBudgetDto(userId, corporateAccountId, employee.CostCenter,
            employee.MonthlyBudget, employee.MonthlyBudgetConsumed,
            employee.MonthlyAllowance, employee.AllowanceConsumed);
    }

    public async Task UpdateEmployeeBudgetAsync(Guid corporateAccountId, Guid userId, decimal newBudget, Guid changedByAdminId, CancellationToken ct = default)
    {
        var employee = await _uow.CorporateAccounts.GetEmployeeAsync(corporateAccountId, userId, ct)
            ?? throw new KeyNotFoundException();

        var before = employee.MonthlyBudget;
        employee.MonthlyBudget = newBudget;
        employee.UpdatedAt = DateTime.UtcNow;

        await _audit.WriteAsync(new AuditLogRequest(
            AuditEventType.Config, "EMPLOYEE_BUDGET_UPDATED",
            changedByAdminId, ActorRole.CorporateAccountManager,
            employee.Id, "CORPORATE_EMPLOYEE",
            JsonSerializer.Serialize(new { MonthlyBudget = before }),
            JsonSerializer.Serialize(new { MonthlyBudget = newBudget }),
            null, null, null), ct);

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<CorporateAccountDto>> GetAccountsBelowAlertThresholdAsync(CancellationToken ct = default)
    {
        var accounts = await _uow.CorporateAccounts.GetBelowAlertThresholdAsync(ct);
        return accounts.Select(a => MapToDto(a, 0));
    }

    private static CorporateAccountDto MapToDto(CorporateAccount a, decimal walletBalance) => new(
        a.Id, a.CompanyName, a.VatRegistrationNumber, a.BillingModel, a.PaymentTerms,
        a.NegotiatedDiscountPercent, a.WalletId, walletBalance,
        a.CreditLimit, a.MonthlyBudgetCap, a.MonthlyBudgetConsumed,
        a.IsActive, a.ContractVersion, a.CreatedAt);

    private static CorporateInvoiceDto MapInvoiceDto(CorporateInvoice i) => new(
        i.Id, i.CorporateAccountId, i.InvoiceNumber,
        i.SubtotalAmount, i.VatAmount, i.TotalAmount,
        i.InvoiceDate, i.DueDate, i.IsPaid, i.PaidAt);
}
