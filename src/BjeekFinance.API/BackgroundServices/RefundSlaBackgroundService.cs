using BjeekFinance.Application.Interfaces;

namespace BjeekFinance.API.BackgroundServices;

/// <summary>
/// UC-FIN-REFUND-ENGINE-01 AF4/AF5: Background service that monitors refund
/// SLA deadlines. Runs every 60 seconds.
/// - 75% of target SLA elapsed → sends in-app reminder (AF4)
/// - 100% of target SLA elapsed → auto-escalates to next approver tier (AF5)
/// </summary>
public class RefundSlaBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefundSlaBackgroundService> _logger;

    public RefundSlaBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RefundSlaBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Refund SLA background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var refundService = scope.ServiceProvider.GetRequiredService<IRefundService>();

                var reminders = await refundService.ProcessSlaRemindersAsync(stoppingToken);
                if (reminders.Any())
                    _logger.LogInformation("Sent SLA reminders for {Count} refund(s).", reminders.Count());

                var escalations = await refundService.ProcessSlaEscalationsAsync(stoppingToken);
                if (escalations.Any())
                    _logger.LogWarning("Auto-escalated {Count} SLA-breached refund(s).", escalations.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund SLA checks.");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
