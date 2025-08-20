using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PixelSolution.Services
{
    public class BackgroundPaymentReminderService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundPaymentReminderService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(24); // Check daily

        public BackgroundPaymentReminderService(
            IServiceProvider serviceProvider,
            ILogger<BackgroundPaymentReminderService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var paymentReminderService = scope.ServiceProvider.GetRequiredService<IPaymentReminderService>();
                        await paymentReminderService.SendMonthlyPaymentRemindersAsync();
                    }

                    _logger.LogInformation($"Payment reminder check completed at {DateTime.UtcNow}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing payment reminders");
                }

                await Task.Delay(_period, stoppingToken);
            }
        }
    }
}
