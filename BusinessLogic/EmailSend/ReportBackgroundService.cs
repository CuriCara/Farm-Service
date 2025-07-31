using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BusinessLogic.EmailSend;

namespace Service;
public class ReportBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReportBackgroundService> _logger;

    public ReportBackgroundService(IServiceProvider services, ILogger<ReportBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var targetTime = new DateTime(now.Year, now.Month, now.Day, 18, 0, 0);
            
            if (now > targetTime)
                targetTime = targetTime.AddDays(1);

            var delay = targetTime - now;
            _logger.LogInformation($"Следующий отчет будет отправлен в {targetTime}");

            await Task.Delay(delay, stoppingToken);

            using (var scope = _services.CreateScope())
            {
                var reportService = scope.ServiceProvider.GetRequiredService<ReportService>();
                await reportService.GenerateDailyReportAsync();
            }
        }
    }
}