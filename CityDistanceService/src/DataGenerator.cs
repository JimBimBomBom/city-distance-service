using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

/// <summary>
/// Background service that periodically fetches city data from Wikidata,
/// generates a SQL file, and signals the app to reload.
/// </summary>
public class DataGenerator
{
    private readonly ILogger<DataGenerator> _logger;
    private readonly IWikidataService _wikidataService;
    private readonly HttpClient _httpClient;
    private readonly string _dataDirectory;
    private readonly string _appLanguagesEndpoint;
    private readonly string _appReloadEndpoint;
    private readonly TimeSpan _generationInterval;
    
    private string? _generatedSqlPath;
    private DateTime _lastGeneratedAt = DateTime.MinValue;
    private readonly object _lock = new();
    private bool _isGenerating = false;
    private CancellationTokenSource? _generationCts;

    public DataGenerator(
        ILogger<DataGenerator> logger,
        IWikidataService wikidataService,
        IConfiguration configuration)
    {
        _logger = logger;
        _wikidataService = wikidataService;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        
        _dataDirectory = configuration["DATA_DIRECTORY"] ?? "/app/data";
        _appLanguagesEndpoint = configuration["APP_LANGUAGES_ENDPOINT"] ?? "http://cds-app:8080/languages";
        _appReloadEndpoint = configuration["APP_RELOAD_ENDPOINT"] ?? "http://cds-app:8080/admin/reload-from-generator";
        
        // Get generation interval from environment (default: 30 days)
        var intervalDays = 30;
        if (int.TryParse(configuration["DATA_GENERATION_INTERVAL_DAYS"], out var envDays) && envDays > 0)
        {
            intervalDays = envDays;
        }
        _generationInterval = TimeSpan.FromDays(intervalDays);
        
        _generatedSqlPath = Path.Combine(_dataDirectory, "cities.sql");
        
        // Ensure data directory exists
        Directory.CreateDirectory(_dataDirectory);
    }

    /// <summary>
    /// Gets the status of data generation and SQL file availability.
    /// </summary>
    public DataGeneratorStatus GetStatus()
    {
        lock (_lock)
        {
            var sqlPath = _generatedSqlPath;
            bool fileExists = !string.IsNullOrEmpty(sqlPath) && File.Exists(sqlPath);
            
            return new DataGeneratorStatus
            {
                IsGenerating = _isGenerating,
                IsReady = fileExists,
                SqlFilePath = fileExists ? sqlPath : null,
                LastGeneratedAt = fileExists ? _lastGeneratedAt : null,
                RecordCount = fileExists ? GetRecordCount() : 0,
                FileSizeBytes = fileExists && sqlPath != null ? new FileInfo(sqlPath).Length : 0
            };
        }
    }

    private long GetRecordCount()
    {
        // Count lines starting with "INSERT INTO cities" as approximation
        if (_generatedSqlPath == null || !File.Exists(_generatedSqlPath))
            return 0;
            
        try
        {
            var content = File.ReadAllText(_generatedSqlPath);
            // Count INSERT statements (each batch is one INSERT)
            return content.Split("INSERT INTO cities", StringSplitOptions.None).Length - 1;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets the path to the generated SQL file, or null if not yet generated.
    /// </summary>
    public string? GetSqlFilePath()
    {
        lock (_lock)
        {
            return !string.IsNullOrEmpty(_generatedSqlPath) && File.Exists(_generatedSqlPath) 
                ? _generatedSqlPath 
                : null;
        }
    }

    /// <summary>
    /// Starts the continuous generation loop that runs periodically.
    /// </summary>
    public async Task StartGenerationLoopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting data generation loop. Interval: {Interval} days", _generationInterval.TotalDays);
        
        // Initial delay to let the app start up first (so we can fetch languages)
        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            // Check if we should generate (first run or interval passed)
            bool shouldGenerate = false;
            lock (_lock)
            {
                if (!_isGenerating)
                {
                    var timeSinceLastGen = DateTime.UtcNow - _lastGeneratedAt;
                    if (_lastGeneratedAt == DateTime.MinValue || timeSinceLastGen >= _generationInterval)
                    {
                        shouldGenerate = true;
                    }
                }
            }
            
            if (shouldGenerate)
            {
                await RunGenerationCycleAsync(cancellationToken);
            }
            
            // Wait before checking again
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }
    }

    private async Task RunGenerationCycleAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_isGenerating)
            {
                _logger.LogWarning("Data generation is already in progress.");
                return;
            }
            _isGenerating = true;
            _generationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        try
        {
            _logger.LogInformation("Starting data generation cycle at {Time}", DateTime.UtcNow);
            
            // Fetch language codes from the main app
            var languageCodes = await FetchLanguageCodesAsync(cancellationToken);
            if (languageCodes.Count == 0)
            {
                _logger.LogError("No language codes available. Skipping generation cycle.");
                return;
            }

            // Work on a fresh SQL file for this cycle, separate from the live one.
            // This way the live cities.sql stays available for download throughout the run.
            var cycleFile = Path.Combine(_dataDirectory, "cities_inprogress.sql");
            
            // Start with a copy of the existing SQL file so previous cycles are not lost.
            var finalPath = Path.Combine(_dataDirectory, "cities.sql");
            if (File.Exists(finalPath))
            {
                File.Copy(finalPath, cycleFile, overwrite: true);
                _logger.LogInformation("Copied existing SQL file as base for this cycle ({Bytes} bytes)", new FileInfo(cycleFile).Length);
            }
            else
            {
                // Write header only
                await File.WriteAllTextAsync(cycleFile,
                    $"-- Auto-generated SQL file from Wikidata\n" +
                    $"-- Cycle started: {DateTime.UtcNow:u}\n" +
                    $"-- Format: INSERT ... ON DUPLICATE KEY UPDATE\n\n" +
                    $"USE CityDistanceService;\n\n");
            }

            int totalNew = 0;
            int languagesCompleted = 0;

            // Fetch each language and immediately append to the cycle file
            for (int i = 0; i < languageCodes.Count; i++)
            {
                var lang = languageCodes[i];
                
                if (_generationCts.Token.IsCancellationRequested)
                {
                    _logger.LogInformation("Data generation cancelled before language {Language}.", lang);
                    break;
                }

                try
                {
                    _logger.LogInformation("[{Progress}/{Total}] Fetching cities for language: {Language}", 
                        i + 1, languageCodes.Count, lang);
                    
                    var cities = await _wikidataService.FetchCitiesAsync(lang);
                    
                    if (cities.Count > 0)
                    {
                        // Append this language's cities as upsert batches to the cycle file
                        await AppendCitiesToFileAsync(cycleFile, cities, lang);
                        totalNew += cities.Count;
                        languagesCompleted++;
                        
                        _logger.LogInformation(
                            "[{Progress}/{Total}] {Language} done: {Count} cities written to file. Running total: {Total2}",
                            i + 1, languageCodes.Count, lang, cities.Count, totalNew);
                    }
                    else
                    {
                        _logger.LogWarning("[{Progress}/{Total}] {Language}: no cities returned, skipping.",
                            i + 1, languageCodes.Count, lang);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch cities for language {Language}. Continuing with next language.", lang);
                }
            }

            if (totalNew == 0)
            {
                _logger.LogWarning("No new cities fetched in this cycle. Keeping existing SQL file.");
                if (File.Exists(cycleFile)) File.Delete(cycleFile);
                return;
            }

            // Atomically replace the live file with the completed cycle file
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(cycleFile, finalPath);
            
            lock (_lock)
            {
                _generatedSqlPath = finalPath;
                _lastGeneratedAt = DateTime.UtcNow;
            }

            _logger.LogInformation(
                "Cycle complete. {Languages}/{Total} languages, {Cities} cities written to {Path}",
                languagesCompleted, languageCodes.Count, totalNew, finalPath);
            
            // Signal the app to reload with retry logic
            await SignalAppToReloadAsync();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Data generation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data generation cycle.");
        }
        finally
        {
            lock (_lock) { _isGenerating = false; }
        }
    }

    /// <summary>
    /// Appends a language's cities as INSERT ... ON DUPLICATE KEY UPDATE batches to the given file.
    /// </summary>
    private static async Task AppendCitiesToFileAsync(string filePath, List<SparQLCityInfo> cities, string language)
    {
        const int batchSize = 1000;
        
        await using var writer = new StreamWriter(filePath, append: true, Encoding.UTF8);
        await writer.WriteLineAsync($"-- Language: {language} ({cities.Count} cities, written {DateTime.UtcNow:u})");
        
        for (int i = 0; i < cities.Count; i += batchSize)
        {
            var batch = cities.Skip(i).Take(batchSize).ToList();
            await WriteBatchInsertAsync(writer, batch);
            await writer.WriteLineAsync();
        }
        
        await writer.FlushAsync();
    }

    private async Task SignalAppToReloadAsync()
    {
        _logger.LogInformation("Signaling app to reload data...");
        
        const int maxRetries = 10;
        const int delaySeconds = 5;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsync(_appReloadEndpoint, null);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Successfully signaled app to reload data (attempt {Attempt})", attempt);
                    return;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("App returned 403 - reload endpoint requires localhost access. This is expected in Docker networking.");
                    return;
                }
                else
                {
                    _logger.LogWarning("App returned {StatusCode} (attempt {Attempt}/{Max})", 
                        (int)response.StatusCode, attempt, maxRetries);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to signal app (attempt {Attempt}/{Max}): {Message}", 
                    attempt, maxRetries, ex.Message);
            }
            
            if (attempt < maxRetries)
            {
                _logger.LogInformation("Retrying in {Delay}s...", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
        
        _logger.LogError("Failed to signal app after {Max} attempts. App will need to be restarted to load new data.", maxRetries);
    }

    private async Task<List<string>> FetchLanguageCodesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching language codes from {Endpoint}...", _appLanguagesEndpoint);
            
            var response = await _httpClient.GetAsync(_appLanguagesEndpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var languages = JsonSerializer.Deserialize<List<LanguageInfo>>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            if (languages == null || languages.Count == 0)
            {
                _logger.LogWarning("No languages returned from app endpoint.");
                return new List<string>();
            }

            // Extract base 2-letter codes, remove duplicates
            var languageCodes = languages
                .Select(l => l.Code.Split('-')[0].ToLowerInvariant())
                .Distinct()
                .ToList();

            _logger.LogInformation("Retrieved {Count} unique language codes: {Languages}", 
                languageCodes.Count, string.Join(", ", languageCodes));
            
            return languageCodes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch language codes from {Endpoint}. Will retry on next cycle.", 
                _appLanguagesEndpoint);
            return new List<string>();
        }
    }

    private static async Task WriteBatchInsertAsync(StreamWriter writer, List<SparQLCityInfo> cities)
    {
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

    /// <summary>
    /// Cancels any ongoing data generation.
    /// </summary>
    public void CancelGeneration()
    {
        lock (_lock)
        {
            if (_generationCts != null)
            {
                _logger.LogInformation("Cancelling data generation...");
                _generationCts.Cancel();
            }
        }
    }
}

public class DataGeneratorStatus
{
    public bool IsGenerating { get; set; }
    public bool IsReady { get; set; }
    public string? SqlFilePath { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public long RecordCount { get; set; }
    public long FileSizeBytes { get; set; }
}

public class LanguageInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
