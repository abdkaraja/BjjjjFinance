using BjeekFinance.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace BjeekFinance.API.Middleware;

/// <summary>
/// Global exception handler — translates domain and infrastructure exceptions
/// to RFC 7807 Problem Details JSON responses with structured error codes.
/// All 500-level errors are logged; domain errors are returned verbatim to client.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ctx, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext ctx, Exception ex)
    {
        var (status, errorCode, message) = ex switch
        {
            InsufficientBalanceException e       => (HttpStatusCode.UnprocessableEntity, e.ErrorCode, e.Message),
            KycNotVerifiedException e            => (HttpStatusCode.Forbidden,           e.ErrorCode, e.Message),
            InstantPayNotEligibleException e     => (HttpStatusCode.UnprocessableEntity, e.ErrorCode, e.Message),
            InstantPayDailyLimitExceededException e => (HttpStatusCode.TooManyRequests,  e.ErrorCode, e.Message),
            DunningHoldException e               => (HttpStatusCode.Forbidden,           e.ErrorCode, e.Message),
            SarieWindowClosedException e         => (HttpStatusCode.Accepted,            e.ErrorCode, e.Message),
            WalletFrozenException e              => (HttpStatusCode.Forbidden,           e.ErrorCode, e.Message),
            IbanValidationException e            => (HttpStatusCode.BadRequest,          e.ErrorCode, e.Message),
            PayoutBelowMinimumException e        => (HttpStatusCode.UnprocessableEntity, e.ErrorCode, e.Message),
            CorporateBudgetExceededException e   => (HttpStatusCode.PaymentRequired,     e.ErrorCode, e.Message),
            CorporateWalletInsufficientException e => (HttpStatusCode.PaymentRequired,   e.ErrorCode, e.Message),
            RefundWindowExpiredException e       => (HttpStatusCode.Gone,                e.ErrorCode, e.Message),
            IdempotencyConflictException e       => (HttpStatusCode.Conflict,            e.ErrorCode, e.Message),
            KeyNotFoundException _               => (HttpStatusCode.NotFound,            "NOT_FOUND", ex.Message),
            InvalidOperationException _          => (HttpStatusCode.BadRequest,          "INVALID_OPERATION", ex.Message),
            UnauthorizedAccessException _        => (HttpStatusCode.Unauthorized,        "UNAUTHORIZED", "Unauthorized."),
            ArgumentException _                  => (HttpStatusCode.BadRequest,          "BAD_REQUEST", ex.Message),
            _                                    => (HttpStatusCode.InternalServerError,  "INTERNAL_ERROR", "An unexpected error occurred.")
        };

        if (status == HttpStatusCode.InternalServerError)
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

        ctx.Response.ContentType = "application/problem+json";
        ctx.Response.StatusCode = (int)status;

        var problem = new
        {
            type = $"https://bjeek.com/finance/errors/{errorCode.ToLowerInvariant()}",
            title = errorCode,
            status = (int)status,
            detail = message,
            instance = ctx.Request.Path.ToString(),
            traceId = ctx.TraceIdentifier
        };

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
