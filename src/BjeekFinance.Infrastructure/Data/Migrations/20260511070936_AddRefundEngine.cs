using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BjeekFinance.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "KycStatus",
                table: "Wallets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true,
                oldDefaultValue: "Unverified");

            migrationBuilder.AlterColumn<bool>(
                name: "IsInDunning",
                table: "Wallets",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "InstantPayTier",
                table: "Wallets",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true,
                oldDefaultValue: "TierA");

            migrationBuilder.AlterColumn<bool>(
                name: "InstantPayEnabled",
                table: "Wallets",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<int>(
                name: "InstantPayDailyCount",
                table: "Wallets",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "FraudScore",
                table: "Wallets",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Wallets",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true,
                oldDefaultValue: "SAR");

            migrationBuilder.AlterColumn<decimal>(
                name: "CashReceivable",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceRefundCredit",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalancePromoCredit",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalancePending",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceHold",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceCourtesyCredit",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceAvailable",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AutoCashoutThreshold",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                table: "Wallets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CourtesyCreditExpiresAt",
                table: "Wallets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DunningBucket",
                table: "Wallets",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DunningStartedAt",
                table: "Wallets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InstantPayDailyCountResetAt",
                table: "Wallets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFraudScoreDecayAt",
                table: "Wallets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyPoints",
                table: "Wallets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PayoutAccountId",
                table: "Wallets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingSince",
                table: "Wallets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PromoCreditExpiresAt",
                table: "Wallets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BulkReconciliationReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TotalGrossCollected = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalDriverPayouts = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalMerchantPayouts = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalPlatformRevenue = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalOutstandingReceivables = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalHolds = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalRefunds = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalWriteOffs = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    ImbalanceAmount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    ImbalanceDetected = table.Column<bool>(type: "bit", nullable: false),
                    AuditTamperDetected = table.Column<bool>(type: "bit", nullable: false),
                    ReportDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CsvContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExportFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "CSV"),
                    GeneratedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkReconciliationReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedCashTotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ReportedCashTotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    VarianceAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CommissionReceivableAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VarianceFlag = table.Column<bool>(type: "bit", nullable: false),
                    TripIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditLogEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EscalatedToFraudAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FraudCaseId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LedgerTotal = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    LedgerMatch = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashSettlements_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorporateAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    VatRegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TradeLicenseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuthorizedSignatoryId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BillingModel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PaymentTerms = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NegotiatedDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    LowBalanceAlertThreshold = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MonthlyBudgetCap = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MonthlyBudgetConsumed = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ContractTermsSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContractVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateAccounts_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinanceParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParameterKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParameterValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ActorType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Tier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PreviousValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ChangedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FraudCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TriggerEvent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AutoActionTaken = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssignedToActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvestigationNotesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    ResolutionCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Whitelisted = table.Column<bool>(type: "bit", nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraudCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FraudRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "all"),
                    Threshold = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AutoAction = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    WindowHours = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ChangedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraudRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayoutAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountIdentifier = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AccountHolderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CardFastFundEligible = table.Column<bool>(type: "bit", nullable: false),
                    KycDocumentReferences = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditLogEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalExpectedCash = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalReportedCash = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalVariance = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    AutoAdjustedCount = table.Column<int>(type: "int", nullable: false),
                    FlaggedCount = table.Column<int>(type: "int", nullable: false),
                    EscalatedCount = table.Column<int>(type: "int", nullable: false),
                    LedgerReconciled = table.Column<bool>(type: "bit", nullable: false),
                    ReportDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CsvContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RideId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GrossAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "decimal(6,4)", precision: 6, scale: 4, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    TipAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    FleetFeeAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ChargebackAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PspTransactionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InvoiceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    SagaCorrelationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VatReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MerchantActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TotalGross = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalVat = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalNet = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    InstantPayFeeVat = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    FlaggedMissingConfigCount = table.Column<int>(type: "int", nullable: false),
                    ReportDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CsvContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExportFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "CSV"),
                    GeneratedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorporateEmployees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorporateAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostCenter = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MonthlyBudget = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MonthlyBudgetConsumed = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MonthlyAllowance = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    AllowanceConsumed = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateEmployees_CorporateAccounts_CorporateAccountId",
                        column: x => x.CorporateAccountId,
                        principalTable: "CorporateAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorporateInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorporateAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SellerVatRegistration = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BuyerVatRegistration = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QrCodeData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateInvoices_CorporateAccounts_CorporateAccountId",
                        column: x => x.CorporateAccountId,
                        principalTable: "CorporateAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayoutRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayoutAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountRequested = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    NetTransferAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    DestinationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SarieWindowStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransferReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PspTransactionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    AuditLogEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutRequests_PayoutAccounts_PayoutAccountId",
                        column: x => x.PayoutAccountId,
                        principalTable: "PayoutAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayoutRequests_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EventSubtype = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeforeState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Delta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LocalTimestamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TamperHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogEntries_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefundType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PartialAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    ItemsRefunded = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefundCategory = table.Column<int>(type: "int", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitiatedBySupportAgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerVipTier = table.Column<int>(type: "int", nullable: false),
                    FraudScoreAtRequest = table.Column<int>(type: "int", nullable: false),
                    AvailableForRefundBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsAutoApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalTier = table.Column<int>(type: "int", nullable: true),
                    AssignedApproverActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AiRecommendationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinalDecision = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedAdjustedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ApproverNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReasonCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerWalletDelta = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WarningSentToDriver = table.Column<bool>(type: "bit", nullable: false),
                    RequestMoreInfoNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestMoreInfoRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestMoreInfoRespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaTargetHours = table.Column<int>(type: "int", nullable: false),
                    SlaAssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaBreachedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaPausedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaResumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedFromActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EscalatedFromTier = table.Column<int>(type: "int", nullable: true),
                    CommissionReversalAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    VatReversalAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DestinationMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserWalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlatformWalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PspReversalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuditLogEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupportTicketId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refunds_Transactions_OriginalTransactionId",
                        column: x => x.OriginalTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Wallets_UserWalletId",
                        column: x => x.UserWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Refunds_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ── Seed refund auto-approval threshold parameters (UC-FIN-REFUND-ENGINE-01 §13.2) ──
            var systemId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var seedNow = new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData("FinanceParameters", new[]
            {
                "Id", "ParameterKey", "ParameterValue", "Description",
                "Category", "ChangedByActorId", "EffectiveFrom", "IsActive", "Version",
                "CreatedAt", "UpdatedAt"
            }, new object[]
            {
                Guid.NewGuid(), "refund_auto_approve_threshold_standard", "100",
                "Auto-approve refund threshold — Standard tier (SAR)", "refund",
                systemId, seedNow, true, 1, seedNow, seedNow
            });

            migrationBuilder.InsertData("FinanceParameters", new[]
            {
                "Id", "ParameterKey", "ParameterValue", "Description",
                "Category", "ChangedByActorId", "EffectiveFrom", "IsActive", "Version",
                "CreatedAt", "UpdatedAt"
            }, new object[]
            {
                Guid.NewGuid(), "refund_auto_approve_threshold_silver", "250",
                "Auto-approve refund threshold — Silver tier (SAR)", "refund",
                systemId, seedNow, true, 1, seedNow, seedNow
            });

            migrationBuilder.InsertData("FinanceParameters", new[]
            {
                "Id", "ParameterKey", "ParameterValue", "Description",
                "Category", "ChangedByActorId", "EffectiveFrom", "IsActive", "Version",
                "CreatedAt", "UpdatedAt"
            }, new object[]
            {
                Guid.NewGuid(), "refund_auto_approve_threshold_gold", "500",
                "Auto-approve refund threshold — Gold tier (SAR)", "refund",
                systemId, seedNow, true, 1, seedNow, seedNow
            });

            migrationBuilder.InsertData("FinanceParameters", new[]
            {
                "Id", "ParameterKey", "ParameterValue", "Description",
                "Category", "ChangedByActorId", "EffectiveFrom", "IsActive", "Version",
                "CreatedAt", "UpdatedAt"
            }, new object[]
            {
                Guid.NewGuid(), "refund_auto_approve_threshold_platinum", "1000",
                "Auto-approve refund threshold — Platinum tier (SAR)", "refund",
                systemId, seedNow, true, 1, seedNow, seedNow
            });

            migrationBuilder.InsertData("FinanceParameters", new[]
            {
                "Id", "ParameterKey", "ParameterValue", "Description",
                "Category", "ChangedByActorId", "EffectiveFrom", "IsActive", "Version",
                "CreatedAt", "UpdatedAt"
            }, new object[]
            {
                Guid.NewGuid(), "refund_agent_authority_limit", "185",
                "Support Agent max refund request authority (SAR)", "refund",
                systemId, seedNow, true, 1, seedNow, seedNow
            });

            migrationBuilder.InsertData("FinanceParameters", new[]
            {
                "Id", "ParameterKey", "ParameterValue", "Description",
                "Category", "ChangedByActorId", "EffectiveFrom", "IsActive", "Version",
                "CreatedAt", "UpdatedAt"
            }, new object[]
            {
                Guid.NewGuid(), "refund_sla_hours_financeofficer", "4",
                "Refund SLA target hours — Finance Officer tier", "refund",
                systemId, seedNow, true, 1, seedNow, seedNow
            });

            migrationBuilder.InsertData("FinanceParameters", new[]
            {
                "Id", "ParameterKey", "ParameterValue", "Description",
                "Category", "ChangedByActorId", "EffectiveFrom", "IsActive", "Version",
                "CreatedAt", "UpdatedAt"
            }, new object[]
            {
                Guid.NewGuid(), "refund_sla_hours_financemanager", "8",
                "Refund SLA target hours — Finance Manager tier", "refund",
                systemId, seedNow, true, 1, seedNow, seedNow
            });

            migrationBuilder.InsertData("FinanceParameters", new[]
            {
                "Id", "ParameterKey", "ParameterValue", "Description",
                "Category", "ChangedByActorId", "EffectiveFrom", "IsActive", "Version",
                "CreatedAt", "UpdatedAt"
            }, new object[]
            {
                Guid.NewGuid(), "refund_sla_hours_vpfinance", "24",
                "Refund SLA target hours — VP Finance tier", "refund",
                systemId, seedNow, true, 1, seedNow, seedNow
            });

            migrationBuilder.InsertData("FinanceParameters", new[]
            {
                "Id", "ParameterKey", "ParameterValue", "Description",
                "Category", "ChangedByActorId", "EffectiveFrom", "IsActive", "Version",
                "CreatedAt", "UpdatedAt"
            }, new object[]
            {
                Guid.NewGuid(), "refund_sla_hours_cfo", "48",
                "Refund SLA target hours — CFO tier", "refund",
                systemId, seedNow, true, 1, seedNow, seedNow
            });

            migrationBuilder.CreateTable(
                name: "InstantPayCashouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountRequested = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    VatOnFee = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    NetTransferAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    DestinationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransferRail = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TransferStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransferReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MicroInvoiceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DailyCountBefore = table.Column<int>(type: "int", nullable: false),
                    DailyCountAfter = table.Column<int>(type: "int", nullable: false),
                    CityLocalTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAutoTriggered = table.Column<bool>(type: "bit", nullable: false),
                    IsFallback = table.Column<bool>(type: "bit", nullable: false),
                    PayoutRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuditLogEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstantPayCashouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstantPayCashouts_PayoutRequests_PayoutRequestId",
                        column: x => x.PayoutRequestId,
                        principalTable: "PayoutRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InstantPayCashouts_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_FraudScore",
                table: "Wallets",
                column: "FraudScore");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_IsInDunning",
                table: "Wallets",
                column: "IsInDunning");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_KycStatus",
                table: "Wallets",
                column: "KycStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_PayoutAccountId",
                table: "Wallets",
                column: "PayoutAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_ActorId",
                table: "AuditLogEntries",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_EventType_Timestamp",
                table: "AuditLogEntries",
                columns: new[] { "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_SubjectId_SubjectType",
                table: "AuditLogEntries",
                columns: new[] { "SubjectId", "SubjectType" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_Timestamp",
                table: "AuditLogEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_TransactionId",
                table: "AuditLogEntries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BulkReconciliationReports_DateFrom_DateTo_CityId_ServiceType",
                table: "BulkReconciliationReports",
                columns: new[] { "DateFrom", "DateTo", "CityId", "ServiceType" });

            migrationBuilder.CreateIndex(
                name: "IX_BulkReconciliationReports_GeneratedAt",
                table: "BulkReconciliationReports",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashSettlements_CityId",
                table: "CashSettlements",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_CashSettlements_CreatedAt_CityId",
                table: "CashSettlements",
                columns: new[] { "CreatedAt", "CityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashSettlements_DriverId",
                table: "CashSettlements",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_CashSettlements_Status",
                table: "CashSettlements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CashSettlements_WalletId",
                table: "CashSettlements",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAccounts_WalletId",
                table: "CorporateAccounts",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateEmployees_CorporateAccountId_UserId",
                table: "CorporateEmployees",
                columns: new[] { "CorporateAccountId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateInvoices_CorporateAccountId_InvoiceDate",
                table: "CorporateInvoices",
                columns: new[] { "CorporateAccountId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateInvoices_InvoiceNumber",
                table: "CorporateInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceParameters_ParameterKey_CityId_ServiceType_ActorType_Tier_IsActive",
                table: "FinanceParameters",
                columns: new[] { "ParameterKey", "CityId", "ServiceType", "ActorType", "Tier", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FraudCases_ActorId",
                table: "FraudCases",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_FraudCases_Severity",
                table: "FraudCases",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_FraudCases_Status",
                table: "FraudCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FraudCases_Status_Severity",
                table: "FraudCases",
                columns: new[] { "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_FraudRules_Domain_IsActive",
                table: "FraudRules",
                columns: new[] { "Domain", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FraudRules_RuleKey",
                table: "FraudRules",
                column: "RuleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstantPayCashouts_ActorId",
                table: "InstantPayCashouts",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantPayCashouts_ActorId_CityLocalTime",
                table: "InstantPayCashouts",
                columns: new[] { "ActorId", "CityLocalTime" });

            migrationBuilder.CreateIndex(
                name: "IX_InstantPayCashouts_PayoutRequestId",
                table: "InstantPayCashouts",
                column: "PayoutRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantPayCashouts_WalletId",
                table: "InstantPayCashouts",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAccounts_ActorId",
                table: "PayoutAccounts",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAccounts_VerificationStatus",
                table: "PayoutAccounts",
                column: "VerificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequests_ActorId",
                table: "PayoutRequests",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequests_PayoutAccountId",
                table: "PayoutRequests",
                column: "PayoutAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequests_Status",
                table: "PayoutRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequests_Status_SarieWindowStatus",
                table: "PayoutRequests",
                columns: new[] { "Status", "SarieWindowStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequests_WalletId",
                table: "PayoutRequests",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationReports_DateFrom_DateTo_CityId",
                table: "ReconciliationReports",
                columns: new[] { "DateFrom", "DateTo", "CityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationReports_GeneratedAt",
                table: "ReconciliationReports",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ActorId",
                table: "Refunds",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OriginalTransactionId",
                table: "Refunds",
                column: "OriginalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_UserWalletId",
                table: "Refunds",
                column: "UserWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_WalletId",
                table: "Refunds",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CreatedAt",
                table: "Transactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IdempotencyKey",
                table: "Transactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OrderId",
                table: "Transactions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RideId",
                table: "Transactions",
                column: "RideId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_WalletId",
                table: "Transactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_VatReports_GeneratedAt",
                table: "VatReports",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VatReports_PeriodStart_PeriodEnd_MerchantActorId",
                table: "VatReports",
                columns: new[] { "PeriodStart", "PeriodEnd", "MerchantActorId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Wallets_PayoutAccounts_PayoutAccountId",
                table: "Wallets",
                column: "PayoutAccountId",
                principalTable: "PayoutAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Wallets_PayoutAccounts_PayoutAccountId",
                table: "Wallets");

            migrationBuilder.DropTable(
                name: "AuditLogEntries");

            migrationBuilder.DropTable(
                name: "BulkReconciliationReports");

            migrationBuilder.DropTable(
                name: "CashSettlements");

            migrationBuilder.DropTable(
                name: "CorporateEmployees");

            migrationBuilder.DropTable(
                name: "CorporateInvoices");

            migrationBuilder.DropTable(
                name: "FinanceParameters");

            migrationBuilder.DropTable(
                name: "FraudCases");

            migrationBuilder.DropTable(
                name: "FraudRules");

            migrationBuilder.DropTable(
                name: "InstantPayCashouts");

            migrationBuilder.DropTable(
                name: "ReconciliationReports");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "VatReports");

            migrationBuilder.DropTable(
                name: "CorporateAccounts");

            migrationBuilder.DropTable(
                name: "PayoutRequests");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "PayoutAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Wallets_FraudScore",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_Wallets_IsInDunning",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_Wallets_KycStatus",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_Wallets_PayoutAccountId",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "AutoCashoutThreshold",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "CourtesyCreditExpiresAt",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "DunningBucket",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "DunningStartedAt",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "InstantPayDailyCountResetAt",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "LastFraudScoreDecayAt",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "LoyaltyPoints",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "PayoutAccountId",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "PendingSince",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "PromoCreditExpiresAt",
                table: "Wallets");

            migrationBuilder.AlterColumn<string>(
                name: "KycStatus",
                table: "Wallets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                defaultValue: "Unverified",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<bool>(
                name: "IsInDunning",
                table: "Wallets",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "InstantPayTier",
                table: "Wallets",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                defaultValue: "TierA",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<bool>(
                name: "InstantPayEnabled",
                table: "Wallets",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<int>(
                name: "InstantPayDailyCount",
                table: "Wallets",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "FraudScore",
                table: "Wallets",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Wallets",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true,
                defaultValue: "SAR",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "CashReceivable",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceRefundCredit",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalancePromoCredit",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalancePending",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceHold",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceCourtesyCredit",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceAvailable",
                table: "Wallets",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);
        }
    }
}
