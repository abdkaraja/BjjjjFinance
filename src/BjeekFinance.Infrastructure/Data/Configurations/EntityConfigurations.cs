using BjeekFinance.Domain.Entities;
using BjeekFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BjeekFinance.Infrastructure.Data.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.ActorType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(w => w.Currency).HasMaxLength(3).IsRequired();

        // All monetary columns: DECIMAL(12,2) — required for financial accuracy
        builder.Property(w => w.BalanceAvailable).HasPrecision(12, 2).IsRequired();
        builder.Property(w => w.BalancePending).HasPrecision(12, 2).IsRequired();
        builder.Property(w => w.BalanceHold).HasPrecision(12, 2).IsRequired();
        builder.Property(w => w.CashReceivable).HasPrecision(12, 2).IsRequired();
        builder.Property(w => w.BalanceRefundCredit).HasPrecision(12, 2).IsRequired();
        builder.Property(w => w.BalancePromoCredit).HasPrecision(12, 2).IsRequired();
        builder.Property(w => w.BalanceCourtesyCredit).HasPrecision(12, 2).IsRequired();
        builder.Property(w => w.PendingSince);
        builder.Property(w => w.AutoCashoutThreshold).HasPrecision(12, 2);

        builder.Property(w => w.KycStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(w => w.InstantPayTier).HasConversion<string>().HasMaxLength(10);
        builder.Property(w => w.DunningBucket).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(w => new { w.ActorId, w.ActorType }).IsUnique();
        builder.HasIndex(w => w.KycStatus);
        builder.HasIndex(w => w.IsInDunning);
        builder.HasIndex(w => w.FraudScore);

        builder.HasMany(w => w.Transactions)
            .WithOne(t => t.Wallet)
            .HasForeignKey(t => t.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.PayoutRequests)
            .WithOne(p => p.Wallet)
            .HasForeignKey(p => p.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.InstantPayCashouts)
            .WithOne(i => i.Wallet)
            .HasForeignKey(i => i.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.PayoutAccount)
            .WithMany(a => a.Wallets)
            .HasForeignKey(w => w.PayoutAccountId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PayoutAccountConfiguration : IEntityTypeConfiguration<PayoutAccount>
{
    public void Configure(EntityTypeBuilder<PayoutAccount> builder)
    {
        builder.ToTable("PayoutAccounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.DestinationType).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.VerificationStatus).HasConversion<string>().HasMaxLength(20);
        // AccountIdentifier encrypted at infrastructure layer (AES-256)
        builder.Property(a => a.AccountIdentifier).HasMaxLength(500).IsRequired();
        builder.Property(a => a.AccountHolderName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.RejectionReason).HasMaxLength(500);
        builder.Property(a => a.KycDocumentReferences).HasColumnType("nvarchar(max)");

        builder.HasIndex(a => a.ActorId);
        builder.HasIndex(a => a.VerificationStatus);

        builder.HasMany(a => a.PayoutRequests)
            .WithOne(p => p.PayoutAccount)
            .HasForeignKey(p => p.PayoutAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.GrossAmount).HasPrecision(12, 2);
        builder.Property(t => t.CommissionAmount).HasPrecision(12, 2);
        builder.Property(t => t.CommissionRate).HasPrecision(6, 4);
        builder.Property(t => t.VatAmount).HasPrecision(12, 2);
        builder.Property(t => t.TipAmount).HasPrecision(12, 2);
        builder.Property(t => t.FleetFeeAmount).HasPrecision(12, 2);
        builder.Property(t => t.PenaltyAmount).HasPrecision(12, 2);
        builder.Property(t => t.ChargebackAmount).HasPrecision(12, 2);
        builder.Property(t => t.NetAmount).HasPrecision(12, 2);
        builder.Property(t => t.PaymentMethod).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.IdempotencyKey).HasMaxLength(200);
        builder.Property(t => t.PspTransactionId).HasMaxLength(200);
        builder.Property(t => t.InvoiceId).HasMaxLength(100);
        builder.Property(t => t.SagaCorrelationId).HasMaxLength(50);

        // Idempotency key must be globally unique — duplicate gateway events must not create double entries
        builder.HasIndex(t => t.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasIndex(t => t.RideId);
        builder.HasIndex(t => t.OrderId);
        builder.HasIndex(t => t.CreatedAt);
    }
}

public class PayoutRequestConfiguration : IEntityTypeConfiguration<PayoutRequest>
{
    public void Configure(EntityTypeBuilder<PayoutRequest> builder)
    {
        builder.ToTable("PayoutRequests");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.AmountRequested).HasPrecision(12, 2);
        builder.Property(p => p.FeeAmount).HasPrecision(12, 2);
        builder.Property(p => p.NetTransferAmount).HasPrecision(12, 2);
        builder.Property(p => p.DestinationType).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.SarieWindowStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.TransferReference).HasMaxLength(200);
        builder.Property(p => p.PspTransactionId).HasMaxLength(200);
        builder.Property(p => p.RejectionReasonCode).HasMaxLength(100);

        builder.HasIndex(p => p.ActorId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => new { p.Status, p.SarieWindowStatus });
    }
}

public class InstantPayCashoutConfiguration : IEntityTypeConfiguration<InstantPayCashout>
{
    public void Configure(EntityTypeBuilder<InstantPayCashout> builder)
    {
        builder.ToTable("InstantPayCashouts");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.AmountRequested).HasPrecision(12, 2);
        builder.Property(i => i.FeeAmount).HasPrecision(12, 2);
        builder.Property(i => i.VatOnFee).HasPrecision(12, 2);
        builder.Property(i => i.NetTransferAmount).HasPrecision(12, 2);
        builder.Property(i => i.DestinationType).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.TransferRail).HasConversion<string>().HasMaxLength(30);
        builder.Property(i => i.TransferStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.TransferReference).HasMaxLength(200);
        builder.Property(i => i.MicroInvoiceId).HasMaxLength(100);

        builder.HasIndex(i => i.ActorId);
        builder.HasIndex(i => new { i.ActorId, i.CityLocalTime });
    }
}

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EventType).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.EventSubtype).HasMaxLength(100);
        builder.Property(a => a.ActorRole).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.SubjectType).HasMaxLength(50);
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.DeviceId).HasMaxLength(200);
        builder.Property(a => a.TamperHash).HasMaxLength(64).IsRequired();

        // JSONB-style columns stored as nvarchar(max) on SQL Server
        builder.Property(a => a.BeforeState).HasColumnType("nvarchar(max)");
        builder.Property(a => a.AfterState).HasColumnType("nvarchar(max)");
        builder.Property(a => a.Delta).HasColumnType("nvarchar(max)");

        builder.HasIndex(a => new { a.SubjectId, a.SubjectType });
        builder.HasIndex(a => a.ActorId);
        builder.HasIndex(a => new { a.EventType, a.Timestamp });
        builder.HasIndex(a => a.Timestamp);

        // AuditLogEntries are append-only — no UPDATE or DELETE in application layer
        // Enforced via stored procedure / row-level security in production
    }
}

public class CorporateAccountConfiguration : IEntityTypeConfiguration<CorporateAccount>
{
    public void Configure(EntityTypeBuilder<CorporateAccount> builder)
    {
        builder.ToTable("CorporateAccounts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CompanyName).HasMaxLength(300).IsRequired();
        builder.Property(c => c.VatRegistrationNumber).HasMaxLength(50).IsRequired();
        builder.Property(c => c.TradeLicenseNumber).HasMaxLength(100).IsRequired();
        builder.Property(c => c.AuthorizedSignatoryId).HasMaxLength(100).IsRequired();
        builder.Property(c => c.BillingModel).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.PaymentTerms).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.NegotiatedDiscountPercent).HasPrecision(5, 2);
        builder.Property(c => c.CreditLimit).HasPrecision(14, 2);
        builder.Property(c => c.LowBalanceAlertThreshold).HasPrecision(12, 2);
        builder.Property(c => c.MonthlyBudgetCap).HasPrecision(12, 2);
        builder.Property(c => c.MonthlyBudgetConsumed).HasPrecision(12, 2);
        builder.Property(c => c.ContractTermsSnapshot).HasColumnType("nvarchar(max)");

        builder.HasMany(c => c.Employees)
            .WithOne(e => e.CorporateAccount)
            .HasForeignKey(e => e.CorporateAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Invoices)
            .WithOne(i => i.CorporateAccount)
            .HasForeignKey(i => i.CorporateAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CorporateEmployeeConfiguration : IEntityTypeConfiguration<CorporateEmployee>
{
    public void Configure(EntityTypeBuilder<CorporateEmployee> builder)
    {
        builder.ToTable("CorporateEmployees");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CostCenter).HasMaxLength(100);
        builder.Property(e => e.MonthlyBudget).HasPrecision(12, 2);
        builder.Property(e => e.MonthlyBudgetConsumed).HasPrecision(12, 2);
        builder.Property(e => e.MonthlyAllowance).HasPrecision(12, 2);
        builder.Property(e => e.AllowanceConsumed).HasPrecision(12, 2);

        builder.HasIndex(e => new { e.CorporateAccountId, e.UserId }).IsUnique();
    }
}

public class CorporateInvoiceConfiguration : IEntityTypeConfiguration<CorporateInvoice>
{
    public void Configure(EntityTypeBuilder<CorporateInvoice> builder)
    {
        builder.ToTable("CorporateInvoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber).HasMaxLength(100).IsRequired();
        builder.Property(i => i.SubtotalAmount).HasPrecision(14, 2);
        builder.Property(i => i.VatAmount).HasPrecision(14, 2);
        builder.Property(i => i.TotalAmount).HasPrecision(14, 2);
        builder.Property(i => i.SellerVatRegistration).HasMaxLength(50);
        builder.Property(i => i.BuyerVatRegistration).HasMaxLength(50);
        builder.Property(i => i.QrCodeData).HasColumnType("nvarchar(max)");

        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasIndex(i => new { i.CorporateAccountId, i.InvoiceDate });
    }
}

public class FinanceParameterConfiguration : IEntityTypeConfiguration<FinanceParameter>
{
    public void Configure(EntityTypeBuilder<FinanceParameter> builder)
    {
        builder.ToTable("FinanceParameters");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ParameterKey).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ParameterValue).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.ServiceType).HasMaxLength(50);
        builder.Property(p => p.PreviousValue).HasPrecision(18, 4);

        builder.HasIndex(p => new { p.ParameterKey, p.CityId, p.ServiceType, p.IsActive });
    }
}
