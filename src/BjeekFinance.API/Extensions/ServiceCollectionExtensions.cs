using BjeekFinance.API.BackgroundServices;
using BjeekFinance.Application.Interfaces;
using BjeekFinance.Application.Services;
using BjeekFinance.Infrastructure.Data;
using BjeekFinance.Infrastructure.Repositories;
using BjeekFinance.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace BjeekFinance.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IEncryptionService, AesEncryptionService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IPaymentCollectionService, PaymentCollectionService>();
        services.AddScoped<IPayoutService, PayoutService>();
        services.AddScoped<IInstantPayService, InstantPayService>();
        services.AddScoped<IAdminFinanceService, AdminFinanceService>();
        services.AddScoped<ICorporateBillingService, CorporateBillingService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IKycService, KycService>();
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<ICashSettlementService, CashSettlementService>();
        services.AddScoped<IVatReportService, VatReportService>();
        services.AddScoped<IFraudDetectionService, FraudDetectionService>();

        // UC-FIN-REFUND-ENGINE-01: Refund auto-approval & AI recommendation
        services.AddScoped<IRefundAutoApprovalEngine, RefundAutoApprovalEngine>();
        services.AddScoped<IRefundAiRecommendationService, RefundAiRecommendationService>();

        // Background services
        services.AddHostedService<PendingEarningsSettlementService>();
        services.AddHostedService<RefundSlaBackgroundService>();
        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<BjeekFinanceDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("DefaultConnection"),
                sql =>
                {
                    sql.MigrationsAssembly("BjeekFinance.Infrastructure");
                    sql.CommandTimeout(60);
                    sql.EnableRetryOnFailure(maxRetryCount: 3);
                }));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var jwtSection = config.GetSection("Jwt");
        var secret = jwtSection["Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidAudience = jwtSection["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("FinanceAdmin",      p => p.RequireRole("FinanceAdmin", "SuperAdmin"));
            options.AddPolicy("FinanceManager",    p => p.RequireRole("FinanceManager", "VpFinance", "Cfo", "SuperAdmin"));
            options.AddPolicy("FinanceOfficer",    p => p.RequireRole("FinanceOfficer", "FinanceManager", "VpFinance", "Cfo", "SuperAdmin"));
            options.AddPolicy("VpFinance",         p => p.RequireRole("VpFinance", "Cfo", "SuperAdmin"));
            options.AddPolicy("SuperAdmin",        p => p.RequireRole("SuperAdmin"));
            options.AddPolicy("SupportAgent",      p => p.RequireRole("SupportAgent", "FinanceAdmin", "SuperAdmin"));
            options.AddPolicy("FraudTeam",         p => p.RequireRole("FraudOfficer", "FraudManager", "FinanceAdmin", "SuperAdmin"));
            options.AddPolicy("FraudManager",      p => p.RequireRole("FraudManager", "FinanceAdmin", "SuperAdmin"));
            options.AddPolicy("CorporateManager",  p => p.RequireRole("CorporateAccountManager", "FinanceAdmin", "SuperAdmin"));
            options.AddPolicy("DriverOrDelivery",  p => p.RequireRole("Driver", "Delivery"));
            options.AddPolicy("User",              p => p.RequireRole("User"));
        });

        return services;
    }

    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Bjeek Finance API",
                Version = "v1",
                Description = "Internal Finance Platform API — Wallets, Payments, Payouts, Instant Pay, Admin Ops, Corporate Billing."
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter: Bearer {token}"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });

            // Group endpoints by domain tag
            c.TagActionsBy(api =>
            {
                var tag = api.RelativePath?.Split('/').Skip(2).FirstOrDefault() ?? "General";
                return new[] { System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(tag) };
            });
        });

        return services;
    }
}
