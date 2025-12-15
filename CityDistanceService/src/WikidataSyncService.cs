public class WikidataSyncService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly TimeSpan syncInterval = TimeSpan.FromDays(30);
    private readonly TimeSpan retryDelay = TimeSpan.FromHours(1);

    public WikidataSyncService(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDatabaseManager>();

            try
            {
                Console.WriteLine($"[SYNC] Starting Wikidata sync at {DateTime.Now}");
                await RequestHandler.UpdateCityDatabaseAsync(db);
                Console.WriteLine($"[SYNC] Completed Wikidata sync");
                await Task.Delay(syncInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC] FAILED: {ex.Message}. Will retry in {retryDelay}");
                await Task.Delay(retryDelay, stoppingToken);
            }
        }
    }
}
