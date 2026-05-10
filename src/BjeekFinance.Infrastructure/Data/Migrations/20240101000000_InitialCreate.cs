using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BjeekFinance.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── FinanceParameters ──────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "FinanceParameters",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ParameterKey = table.Column<string>(maxLength: 100, nullable: false),
                ParameterValue = table.Column<string>(maxLength: 500, nullable: false),
                Description = table.Column<string>(maxLength: 500, nullable: true),
                CityId = table.Column<Guid>(nullable: true),
                ServiceType = table.Column<string>(maxLength: 50, nullable: true),
                PreviousValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                ChangedByActorId = table.Column<Guid>(nullable: false),
                EffectiveFrom = table.Column<DateTime>(nullable: false),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                Version = table.Column<int>(nullable: false, defaultValue: 1),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table => table.PrimaryKey("PK_FinanceParameters", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_FinanceParameters_Key_City_Service_Active",
            table: "FinanceParameters",
            columns: new[] { "ParameterKey", "CityId", "ServiceType", "IsActive" });

        // ── PayoutAccounts ─────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "PayoutAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ActorId = table.Column<Guid>(nullable: false),
                DestinationType = table.Column<string>(maxLength: 20, nullable: false),
                AccountIdentifier = table.Column<string>(maxLength: 500, nullable: false),
                AccountHolderName = table.Column<string>(maxLength: 200, nullable: false),
                VerificationStatus = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Pending"),
                RejectionReason = table.Column<string>(maxLength: 500, nullable: true),
                CardFastFundEligible = table.Column<bool>(nullable: false, defaultValue: false),
                KycDocumentReferences = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AuditLogEntryId = table.Column<Guid>(nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table => table.PrimaryKey("PK_PayoutAccounts", x => x.Id));

        migrationBuilder.CreateIndex("IX_PayoutAccounts_ActorId", "PayoutAccounts", "ActorId");
        migrationBuilder.CreateIndex("IX_PayoutAccounts_VerificationStatus", "PayoutAccounts", "VerificationStatus");

        // ── Wallets ────────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Wallets",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ActorType = table.Column<string>(maxLength: 20, nullable: false),
                ActorId = table.Column<Guid>(nullable: false),
                Currency = table.Column<string>(maxLength: 3, nullable: false, defaultValue: "SAR"),
                BalanceAvailable = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                BalancePending = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                BalanceHold = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                PendingSince = table.Column<DateTime>(nullable: true),
                CashReceivable = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                BalanceRefundCredit = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                BalancePromoCredit = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                BalanceCourtesyCredit = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                PromoCreditExpiresAt = table.Column<DateTime>(nullable: true),
                CourtesyCreditExpiresAt = table.Column<DateTime>(nullable: true),
                LoyaltyPoints = table.Column<int>(nullable: false, defaultValue: 0),
                KycStatus = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Unverified"),
                PayoutAccountId = table.Column<Guid>(nullable: true),
                InstantPayTier = table.Column<string>(maxLength: 10, nullable: false, defaultValue: "TierA"),
                InstantPayEnabled = table.Column<bool>(nullable: false, defaultValue: true),
                InstantPayDailyCount = table.Column<int>(nullable: false, defaultValue: 0),
                InstantPayDailyCountResetAt = table.Column<DateTime>(nullable: true),
                AutoCashoutThreshold = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                IsInDunning = table.Column<bool>(nullable: false, defaultValue: false),
                DunningStartedAt = table.Column<DateTime>(nullable: true),
                DunningBucket = table.Column<string>(maxLength: 30, nullable: true),
                FraudScore = table.Column<int>(nullable: false, defaultValue: 0),
                LastFraudScoreDecayAt = table.Column<DateTime>(nullable: true),
                CityId = table.Column<Guid>(nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Wallets", x => x.Id);
                table.ForeignKey("FK_Wallets_PayoutAccounts", x => x.PayoutAccountId,
                    "PayoutAccounts", "Id", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex("IX_Wallets_ActorId_ActorType", "Wallets",
            new[] { "ActorId", "ActorType" }, unique: true);
        migrationBuilder.CreateIndex("IX_Wallets_KycStatus", "Wallets", "KycStatus");
        migrationBuilder.CreateIndex("IX_Wallets_IsInDunning", "Wallets", "IsInDunning");
        migrationBuilder.CreateIndex("IX_Wallets_FraudScore", "Wallets", "FraudScore");

        // ── Transactions ───────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Transactions",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                WalletId = table.Column<Guid>(nullable: false),
                RideId = table.Column<Guid>(nullable: true),
                OrderId = table.Column<Guid>(nullable: true),
                GrossAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                CommissionAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                CommissionRate = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                VatAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                TipAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                FleetFeeAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                PenaltyAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                ChargebackAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                NetAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                PaymentMethod = table.Column<string>(maxLength: 30, nullable: false),
                IdempotencyKey = table.Column<string>(maxLength: 200, nullable: true),
                PspTransactionId = table.Column<string>(maxLength: 200, nullable: true),
                InvoiceId = table.Column<string>(maxLength: 100, nullable: true),
                IsReversed = table.Column<bool>(nullable: false, defaultValue: false),
                SagaCorrelationId = table.Column<string>(maxLength: 50, nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Transactions", x => x.Id);
                table.ForeignKey("FK_Transactions_Wallets", x => x.WalletId,
                    "Wallets", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_Transactions_IdempotencyKey", "Transactions", "IdempotencyKey",
            unique: true, filter: "[IdempotencyKey] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_Transactions_RideId", "Transactions", "RideId");
        migrationBuilder.CreateIndex("IX_Transactions_OrderId", "Transactions", "OrderId");
        migrationBuilder.CreateIndex("IX_Transactions_CreatedAt", "Transactions", "CreatedAt");

        // ── PayoutRequests ─────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "PayoutRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ActorId = table.Column<Guid>(nullable: false),
                WalletId = table.Column<Guid>(nullable: false),
                PayoutAccountId = table.Column<Guid>(nullable: false),
                AmountRequested = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                FeeAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                NetTransferAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                DestinationType = table.Column<string>(maxLength: 20, nullable: false),
                Status = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Pending"),
                SarieWindowStatus = table.Column<string>(maxLength: 20, nullable: false),
                TransferReference = table.Column<string>(maxLength: 200, nullable: true),
                PspTransactionId = table.Column<string>(maxLength: 200, nullable: true),
                ApprovedByActorId = table.Column<Guid>(nullable: true),
                ApprovedAt = table.Column<DateTime>(nullable: true),
                RejectionReasonCode = table.Column<string>(maxLength: 100, nullable: true),
                ScheduledAt = table.Column<DateTime>(nullable: true),
                RetryCount = table.Column<int>(nullable: false, defaultValue: 0),
                AuditLogEntryId = table.Column<Guid>(nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PayoutRequests", x => x.Id);
                table.ForeignKey("FK_PayoutRequests_Wallets", x => x.WalletId,
                    "Wallets", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PayoutRequests_PayoutAccounts", x => x.PayoutAccountId,
                    "PayoutAccounts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_PayoutRequests_ActorId", "PayoutRequests", "ActorId");
        migrationBuilder.CreateIndex("IX_PayoutRequests_Status", "PayoutRequests", "Status");
        migrationBuilder.CreateIndex("IX_PayoutRequests_Status_SarieWindow", "PayoutRequests",
            new[] { "Status", "SarieWindowStatus" });

        // ── InstantPayCashouts ─────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "InstantPayCashouts",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ActorId = table.Column<Guid>(nullable: false),
                WalletId = table.Column<Guid>(nullable: false),
                AmountRequested = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                FeeAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                VatOnFee = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                NetTransferAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                DestinationType = table.Column<string>(maxLength: 20, nullable: false),
                TransferRail = table.Column<string>(maxLength: 30, nullable: false),
                TransferStatus = table.Column<string>(maxLength: 20, nullable: false),
                TransferReference = table.Column<string>(maxLength: 200, nullable: true),
                MicroInvoiceId = table.Column<string>(maxLength: 100, nullable: true),
                DailyCountBefore = table.Column<int>(nullable: false),
                DailyCountAfter = table.Column<int>(nullable: false),
                CityLocalTime = table.Column<DateTime>(nullable: false),
                IsAutoTriggered = table.Column<bool>(nullable: false, defaultValue: false),
                IsFallback = table.Column<bool>(nullable: false, defaultValue: false),
                AuditLogEntryId = table.Column<Guid>(nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InstantPayCashouts", x => x.Id);
                table.ForeignKey("FK_InstantPayCashouts_Wallets", x => x.WalletId,
                    "Wallets", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_InstantPayCashouts_ActorId", "InstantPayCashouts", "ActorId");
        migrationBuilder.CreateIndex("IX_InstantPayCashouts_ActorId_CityLocalTime", "InstantPayCashouts",
            new[] { "ActorId", "CityLocalTime" });

        // ── AuditLogEntries ────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "AuditLogEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                EventType = table.Column<string>(maxLength: 30, nullable: false),
                EventSubtype = table.Column<string>(maxLength: 100, nullable: false),
                ActorId = table.Column<Guid>(nullable: false),
                ActorRole = table.Column<string>(maxLength: 30, nullable: false),
                SubjectId = table.Column<Guid>(nullable: false),
                SubjectType = table.Column<string>(maxLength: 50, nullable: false),
                BeforeState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AfterState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Delta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CityId = table.Column<Guid>(nullable: true),
                IpAddress = table.Column<string>(maxLength: 45, nullable: true),
                DeviceId = table.Column<string>(maxLength: 200, nullable: true),
                Timestamp = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                LocalTimestamp = table.Column<DateTime>(nullable: true),
                TamperHash = table.Column<string>(maxLength: 64, nullable: false),
                TransactionId = table.Column<Guid>(nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table => table.PrimaryKey("PK_AuditLogEntries", x => x.Id));

        migrationBuilder.CreateIndex("IX_AuditLogEntries_SubjectId_SubjectType", "AuditLogEntries",
            new[] { "SubjectId", "SubjectType" });
        migrationBuilder.CreateIndex("IX_AuditLogEntries_ActorId", "AuditLogEntries", "ActorId");
        migrationBuilder.CreateIndex("IX_AuditLogEntries_EventType_Timestamp", "AuditLogEntries",
            new[] { "EventType", "Timestamp" });
        migrationBuilder.CreateIndex("IX_AuditLogEntries_Timestamp", "AuditLogEntries", "Timestamp");

        // ── CorporateAccounts ──────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "CorporateAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CompanyName = table.Column<string>(maxLength: 300, nullable: false),
                VatRegistrationNumber = table.Column<string>(maxLength: 50, nullable: false),
                TradeLicenseNumber = table.Column<string>(maxLength: 100, nullable: false),
                AuthorizedSignatoryId = table.Column<string>(maxLength: 100, nullable: false),
                BillingModel = table.Column<string>(maxLength: 30, nullable: false),
                PaymentTerms = table.Column<string>(maxLength: 20, nullable: false),
                NegotiatedDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                WalletId = table.Column<Guid>(nullable: false),
                CreditLimit = table.Column<decimal>(type: "decimal(14,2)", nullable: false, defaultValue: 0m),
                LowBalanceAlertThreshold = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                MonthlyBudgetCap = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                MonthlyBudgetConsumed = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                ContractTermsSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ContractVersion = table.Column<int>(nullable: false, defaultValue: 1),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table => table.PrimaryKey("PK_CorporateAccounts", x => x.Id));

        // ── CorporateEmployees ─────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "CorporateEmployees",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CorporateAccountId = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: false),
                CostCenter = table.Column<string>(maxLength: 100, nullable: false),
                MonthlyBudget = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                MonthlyBudgetConsumed = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                MonthlyAllowance = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                AllowanceConsumed = table.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CorporateEmployees", x => x.Id);
                table.ForeignKey("FK_CorporateEmployees_CorporateAccounts", x => x.CorporateAccountId,
                    "CorporateAccounts", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_CorporateEmployees_AccountId_UserId", "CorporateEmployees",
            new[] { "CorporateAccountId", "UserId" }, unique: true);

        // ── CorporateInvoices ──────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "CorporateInvoices",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CorporateAccountId = table.Column<Guid>(nullable: false),
                InvoiceNumber = table.Column<string>(maxLength: 100, nullable: false),
                SubtotalAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                VatAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                TotalAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                InvoiceDate = table.Column<DateTime>(nullable: false),
                DueDate = table.Column<DateTime>(nullable: false),
                SellerVatRegistration = table.Column<string>(maxLength: 50, nullable: false),
                BuyerVatRegistration = table.Column<string>(maxLength: 50, nullable: false),
                QrCodeData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsPaid = table.Column<bool>(nullable: false, defaultValue: false),
                PaidAt = table.Column<DateTime>(nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CorporateInvoices", x => x.Id);
                table.ForeignKey("FK_CorporateInvoices_CorporateAccounts", x => x.CorporateAccountId,
                    "CorporateAccounts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_CorporateInvoices_InvoiceNumber", "CorporateInvoices",
            "InvoiceNumber", unique: true);
        migrationBuilder.CreateIndex("IX_CorporateInvoices_AccountId_Date", "CorporateInvoices",
            new[] { "CorporateAccountId", "InvoiceDate" });

        // ── CashSettlements (UC-FIN-CASH-01 / UC-AD-FIN-03) ─────────────────────
        migrationBuilder.CreateTable(
            name: "CashSettlements",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                DriverId = table.Column<Guid>(nullable: false),
                WalletId = table.Column<Guid>(nullable: false),
                ExpectedCashTotal = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                ReportedCashTotal = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                VarianceAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                CommissionReceivableAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                Status = table.Column<string>(maxLength: 20, nullable: false),
                VarianceFlag = table.Column<bool>(nullable: false, defaultValue: false),
                TripIdsJson = table.Column<string>(nullable: true),
                Notes = table.Column<string>(maxLength: 500, nullable: true),
                ReviewedByActorId = table.Column<Guid>(nullable: true),
                ReviewedAt = table.Column<DateTime>(nullable: true),
                CompletedAt = table.Column<DateTime>(nullable: true),
                CityId = table.Column<Guid>(nullable: true),
                EscalatedToFraudAt = table.Column<DateTime>(nullable: true),
                FraudCaseId = table.Column<string>(maxLength: 100, nullable: true),
                LedgerTotal = table.Column<decimal>(type: "decimal(14,2)", nullable: true),
                LedgerMatch = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashSettlements", x => x.Id);
                table.ForeignKey("FK_CashSettlements_Wallets", x => x.WalletId,
                    "Wallets", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_CashSettlements_DriverId", "CashSettlements", "DriverId");
        migrationBuilder.CreateIndex("IX_CashSettlements_Status", "CashSettlements", "Status");
        migrationBuilder.CreateIndex("IX_CashSettlements_CityId", "CashSettlements", "CityId");
        migrationBuilder.CreateIndex("IX_CashSettlements_CreatedAt_CityId", "CashSettlements", new[] { "CreatedAt", "CityId" });

        // ── ReconciliationReports (UC-AD-FIN-03) ───────────────────────────────
        migrationBuilder.CreateTable(
            name: "ReconciliationReports",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                DateFrom = table.Column<DateTime>(nullable: false),
                DateTo = table.Column<DateTime>(nullable: false),
                CityId = table.Column<Guid>(nullable: true),
                TotalExpectedCash = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                TotalReportedCash = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                TotalVariance = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                AutoAdjustedCount = table.Column<int>(nullable: false, defaultValue: 0),
                FlaggedCount = table.Column<int>(nullable: false, defaultValue: 0),
                EscalatedCount = table.Column<int>(nullable: false, defaultValue: 0),
                LedgerReconciled = table.Column<bool>(nullable: false, defaultValue: true),
                ReportDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                GeneratedByActorId = table.Column<Guid>(nullable: false),
                CsvContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                GeneratedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table => table.PrimaryKey("PK_ReconciliationReports", x => x.Id));
        migrationBuilder.CreateIndex("IX_ReconciliationReports_DateRange", "ReconciliationReports", new[] { "DateFrom", "DateTo", "CityId" });
        migrationBuilder.CreateIndex("IX_ReconciliationReports_GeneratedAt", "ReconciliationReports", "GeneratedAt");

        // ── Seed default finance parameters ────────────────────────────────────
        var systemActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var parameters = new[]
        {
            ("vat_rate",                          "0.15",    "ZATCA standard VAT rate"),
            ("payout_minimum_threshold",          "50",      "Minimum payout amount in SAR"),
            ("payout_fee",                        "0",       "Standard payout fee in SAR"),
            ("payout_auto_approve_threshold",     "5000",    "Payouts above this SAR amount require Finance Admin approval"),
            ("payout_super_admin_threshold",      "10000",   "Payouts above this SAR amount require Super Admin approval"),
            ("instant_pay_min_balance",           "5",       "Minimum AVAILABLE balance to initiate Instant Pay in SAR"),
            ("instant_pay_daily_limit_tier_b",    "3",       "Tier B daily manual cashout limit"),
            ("instant_pay_daily_limit_tier_c",    "5",       "Tier C daily manual cashout limit"),
            ("instant_pay_tier_a_trips",          "50",      "Trip count threshold to graduate to Tier B"),
            ("instant_pay_tier_c_trips",          "500",     "Trip count threshold to graduate to Tier C"),
            ("tip_window_hours",                  "2",       "Hours after ride completion within which a tip can be added"),
            ("cash_settlement_pending_minutes",   "15",      "Minutes before digital earnings move from PENDING to AVAILABLE"),
            ("promo_credit_expiry_days",          "30",      "Days until promo credit expires"),
            ("courtesy_credit_expiry_days",       "90",      "Days until courtesy credit expires"),
            ("courtesy_credit_monthly_cap",       "100",     "Maximum courtesy credit issuable per customer per month in SAR"),
            ("write_off_finance_manager_limit",   "18500",   "Write-offs below this SAR amount are Finance Manager self-approve"),
            ("bulk_adjustment_super_admin_limit", "50000",   "Bulk adjustments above this SAR total require Super Admin"),
            ("cash_settlement_variance_threshold", "3",      "Cash settlement variance threshold in SAR — ≤ this auto-adjusts, > this flags for review")
        };

        foreach (var (key, value, description) in parameters)
        {
            migrationBuilder.InsertData("FinanceParameters", new[]
            {
                "Id", "ParameterKey", "ParameterValue", "Description",
                "ChangedByActorId", "EffectiveFrom", "IsActive", "Version",
                "CreatedAt", "UpdatedAt"
            },
            new object[]
            {
                Guid.NewGuid(), key, value, description,
                systemActorId, now, true, 1, now, now
            });
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ReconciliationReports");
        migrationBuilder.DropTable("CashSettlements");
        migrationBuilder.DropTable("CorporateInvoices");
        migrationBuilder.DropTable("CorporateEmployees");
        migrationBuilder.DropTable("CorporateAccounts");
        migrationBuilder.DropTable("AuditLogEntries");
        migrationBuilder.DropTable("InstantPayCashouts");
        migrationBuilder.DropTable("PayoutRequests");
        migrationBuilder.DropTable("Transactions");
        migrationBuilder.DropTable("Wallets");
        migrationBuilder.DropTable("PayoutAccounts");
        migrationBuilder.DropTable("FinanceParameters");
    }
}
