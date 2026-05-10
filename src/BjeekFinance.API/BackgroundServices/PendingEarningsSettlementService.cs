using BjeekFinance.Application.Interfaces;

namespace BjeekFinance.API.BackgroundServices;

public class PendingEarningsSettlementService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingEarningsSettlementService> _logger;

    public PendingEarningsSettlementService(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingEarningsSettlementService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pending earnings settlement service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();
                await walletService.SettlePendingForAllEligibleWalletsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while settling pending earnings.");
            }

            // Check every 60 seconds
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
