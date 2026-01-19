using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

public class WikidataSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WikidataSyncService> _logger;
    private readonly TimeSpan _syncInterval;

    public WikidataSyncService(
        IServiceProvider serviceProvider,
        ILogger<WikidataSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Set sync interval - default to 24 hours
    _syncInterval = TimeSpan.FromHours(120);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WikidataSyncService is starting.");

        // Wait a bit on startup to let the application initialize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting scheduled Wikidata sync at {Time}", DateTime.UtcNow);

                // Create a scope to resolve scoped services
                using (var scope = _serviceProvider.CreateScope())
                {
                    var cityService = scope.ServiceProvider.GetRequiredService<ICityDataService>();

                    var recordsAffected = await cityService.SyncCitiesFromWikidataAsync();

                    _logger.LogInformation(
                        "Wikidata sync completed successfully. {RecordsAffected} records affected at {Time}",
                        recordsAffected,
                        DateTime.UtcNow
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Wikidata sync at {Time}", DateTime.UtcNow);
            }

            _logger.LogInformation(
                "Next Wikidata sync scheduled for {NextSync}",
                DateTime.UtcNow.Add(_syncInterval)
            );

            await Task.Delay(_syncInterval, stoppingToken);
        }

        _logger.LogInformation("WikidataSyncService is stopping.");
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WikidataSyncService is being stopped.");
        return base.StopAsync(cancellationToken);
    }
}