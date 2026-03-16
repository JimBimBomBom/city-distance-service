using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Background service that periodically fetches city data from Wikidata,
/// generates a SQL file with upsert statements, and stores it in memory.
/// After generating the file, it triggers a data reload.
/// </summary>
public class DataGenerationService : BackgroundService
{
    private readonly ILogger<DataGenerationService> _logger;
    private readonly IWikidataService _wikidataService;
    private readonly IDataReloadService _reloadService;
    private readonly TimeSpan _syncInterval;
    private readonly string _dataDirectory;

    private string _generatedSqlPath;
    private DateTime _lastGeneratedAt = DateTime.MinValue;
    private readonly object _lock = new();

    public DataGenerationService(
        ILogger<DataGenerationService> logger,
        IWikidataService wikidataService,
        IDataReloadService reloadService)
    {
        _logger = logger;
        _wikidataService = wikidataService;
        _reloadService = reloadService;
        
        // Get interval from environment variable (default: 30 days)
        var intervalDays = 30;
        if (int.TryParse(Environment.GetEnvironmentVariable("DATA_GENERATION_INTERVAL_DAYS"), out var envDays) && envDays > 0)
        {
            intervalDays = envDays;
        }
        _syncInterval = TimeSpan.FromDays(intervalDays);
        
        // Store in /app/data directory within the container
        _dataDirectory = "/app/data";
        _generatedSqlPath = Path.Combine(_dataDirectory, "cities.sql");
    }

    /// <summary>
    /// Gets the path to the generated SQL file, or null if not yet generated.
    /// </summary>
    public string? GetSqlFilePath()
    {
        lock (_lock)
        {
            return File.Exists(_generatedSqlPath) ? _generatedSqlPath : null;
        }
    }

    /// <summary>
    /// Gets the timestamp when the SQL file was last generated.
    /// </summary>
    public DateTime GetLastGeneratedAt()
    {
        lock (_lock)
        {
            return _lastGeneratedAt;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataGenerationService is starting. Sync interval: {Interval} days", 
            _syncInterval.TotalDays);

        // Ensure data directory exists
        Directory.CreateDirectory(_dataDirectory);

        // Initial delay to let the application stabilize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting Wikidata fetch and SQL generation at {Time}", DateTime.UtcNow);
                
                await GenerateAndStoreDataAsync(stoppingToken);
                
                _logger.LogInformation("Data generation complete. Sleeping for {Days} days until next sync.", 
                    _syncInterval.TotalDays);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Data generation was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data generation. Will retry in {Days} days.", 
                    _syncInterval.TotalDays);
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }

        _logger.LogInformation("DataGenerationService is stopping.");
    }

    public async Task GenerateAndStoreDataAsync(CancellationToken cancellationToken)
    {
        var allCities = new List<SparQLCityInfo>();
        
        // Fetch cities from all supported languages
        foreach (var lang in Constants.SupportedLanguages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Data generation cancelled during language fetch.");
                return;
            }

            try
            {
                _logger.LogInformation("Fetching cities for language: {Language}", lang);
                var cities = await _wikidataService.FetchCitiesAsync(lang);
                
                if (cities.Count > 0)
                {
                    allCities.AddRange(cities);
                    _logger.LogInformation("Fetched {Count} cities for {Language}", cities.Count, lang);
                }
                
                // Rate limiting - wait 10 seconds between languages
                if (lang != Constants.SupportedLanguages.Last())
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch cities for language {Language}. Continuing...", lang);
                // Continue with other languages
            }
        }

        if (allCities.Count == 0)
        {
            _logger.LogWarning("No cities fetched from Wikidata. Skipping SQL generation.");
            return;
        }

        // Generate SQL file
        var sqlPath = await GenerateSqlFileAsync(allCities);
        
        lock (_lock)
        {
            _generatedSqlPath = sqlPath;
            _lastGeneratedAt = DateTime.UtcNow;
        }

        _logger.LogInformation("SQL file generated: {Path} ({Count} records)", sqlPath, allCities.Count);

        // Trigger data reload
        _logger.LogInformation("Triggering data reload...");
        await _reloadService.ReloadFromSqlFileAsync(sqlPath, cancellationToken);
    }

    private async Task<string> GenerateSqlFileAsync(List<SparQLCityInfo> cities)
    {
        var tempPath = Path.Combine(_dataDirectory, $"cities_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql");
        var finalPath = Path.Combine(_dataDirectory, "cities.sql");

        try
        {
            await using var writer = new StreamWriter(tempPath, false, Encoding.UTF8);
            
            // Write header
            await writer.WriteLineAsync("-- Auto-generated SQL file from Wikidata");
            await writer.WriteLineAsync($"-- Generated at: {DateTime.UtcNow:u}");
            await writer.WriteLineAsync($"-- Total records: {cities.Count}");
            await writer.WriteLineAsync("-- Format: INSERT ... ON DUPLICATE KEY UPDATE");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("USE CityDistanceService;");
            await writer.WriteLineAsync();

            // Group cities by their ID for upsert logic
            // Each city appears once per language, but we want one INSERT per city
            var uniqueCities = cities
                .GroupBy(c => c.WikidataId)
                .Select(g => g.First())  // Take first language occurrence for MySQL
                .ToList();

            // Write batch inserts with ON DUPLICATE KEY UPDATE
            const int batchSize = 1000;
            for (int i = 0; i < uniqueCities.Count; i += batchSize)
            {
                var batch = uniqueCities.Skip(i).Take(batchSize).ToList();
                await WriteBatchInsertAsync(writer, batch);
                
                if (i + batchSize < uniqueCities.Count)
                {
                    await writer.WriteLineAsync();
                }
            }

            await writer.FlushAsync();
        }
        catch
        {
            // Clean up temp file on error
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            throw;
        }

        // Atomic rename (or copy/delete on Windows)
        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }
        File.Move(tempPath, finalPath);

        return finalPath;
    }

    private static async Task WriteBatchInsertAsync(StreamWriter writer, List<SparQLCityInfo> cities)
    {
        // Build complete INSERT ... ON DUPLICATE KEY UPDATE statement
        var sb = new StringBuilder();
        sb.AppendLine("INSERT INTO cities (CityId, CityName, Latitude, Longitude, CountryCode, Country, AdminRegion, Population) VALUES");

        for (int i = 0; i < cities.Count; i++)
        {
            var city = cities[i];
            var isLast = i == cities.Count - 1;
            
            var sql = $"    ('{EscapeSql(city.WikidataId)}', '{EscapeSql(city.CityName)}', {city.Latitude:F8}, {city.Longitude:F8}, " +
                      $"{(city.CountryCode != null ? $"'{EscapeSql(city.CountryCode)}'" : "NULL")}, " +
                      $"{(city.Country != null ? $"'{EscapeSql(city.Country)}'" : "NULL")}, " +
                      $"{(city.AdminRegion != null ? $"'{EscapeSql(city.AdminRegion)}'" : "NULL")}, " +
                      $"{(city.Population.HasValue ? city.Population.Value.ToString() : "NULL")}){(isLast ? "" : ",")}";
            
            sb.AppendLine(sql);
        }

        sb.AppendLine("ON DUPLICATE KEY UPDATE");
        sb.AppendLine("    CityName = VALUES(CityName),");
        sb.AppendLine("    Latitude = VALUES(Latitude),");
        sb.AppendLine("    Longitude = VALUES(Longitude),");
        sb.AppendLine("    CountryCode = VALUES(CountryCode),");
        sb.AppendLine("    Country = VALUES(Country),");
        sb.AppendLine("    AdminRegion = VALUES(AdminRegion),");
        sb.AppendLine("    Population = VALUES(Population);");

        await writer.WriteAsync(sb.ToString());
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("'", "''").Replace("\\", "\\\\");
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DataGenerationService is being stopped.");
        return base.StopAsync(cancellationToken);
    }
}
