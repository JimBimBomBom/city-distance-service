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

            // Load existing cities from current SQL file if it exists (for merging)
            var existingCities = LoadExistingCitiesFromSqlFile();
            _logger.LogInformation("Loaded {Count} existing cities from current SQL file", existingCities.Count);

            var allCities = new List<SparQLCityInfo>(existingCities);
            int newCitiesCount = 0;
            
            // Fetch cities for each language
            for (int i = 0; i < languageCodes.Count; i++)
            {
                var lang = languageCodes[i];
                
                if (_generationCts.Token.IsCancellationRequested)
                {
                    _logger.LogInformation("Data generation cancelled during language fetch.");
                    return;
                }

                try
                {
                    _logger.LogInformation("[{Progress}/{Total}] Fetching cities for language: {Language}", 
                        i + 1, languageCodes.Count, lang);
                    
                    var cities = await _wikidataService.FetchCitiesAsync(lang);
                    
                    if (cities.Count > 0)
                    {
                        // Merge new cities with existing (by WikidataId)
                        var existingIds = new HashSet<string>(allCities.Select(c => c.WikidataId));
                        foreach (var city in cities)
                        {
                            if (!existingIds.Contains(city.WikidataId))
                            {
                                allCities.Add(city);
                                newCitiesCount++;
                            }
                        }
                        
                        _logger.LogInformation("Fetched {Count} cities for {Language}, {NewCount} new unique", 
                            cities.Count, lang, newCitiesCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch cities for language {Language}. Continuing...", lang);
                }
            }

            if (allCities.Count == 0)
            {
                _logger.LogWarning("No cities available. Skipping SQL generation.");
                return;
            }

            // Generate SQL file with merged data
            var sqlPath = await GenerateSqlFileAsync(allCities);
            
            lock (_lock)
            {
                _generatedSqlPath = sqlPath;
                _lastGeneratedAt = DateTime.UtcNow;
            }

            _logger.LogInformation("SQL file generated: {Path} ({Count} total records, {NewCount} new)", 
                sqlPath, allCities.Count, newCitiesCount);
            
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

    private List<SparQLCityInfo> LoadExistingCitiesFromSqlFile()
    {
        var cities = new List<SparQLCityInfo>();
        
        if (_generatedSqlPath == null || !File.Exists(_generatedSqlPath))
        {
            return cities;
        }
        
        try
        {
            // Parse the SQL file to extract city data
            // This is a simple parser that looks for INSERT statement VALUES
            var lines = File.ReadAllLines(_generatedSqlPath);
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("('") || !trimmed.Contains("', '"))
                    continue;
                
                // Parse: ('Q123', 'City Name', 12.34567890, 67.89012345, 'CC', 'Country', 'Region', 12345),
                try
                {
                    var values = trimmed.TrimStart('(').TrimEnd(',', ')').Split("', ");
                    if (values.Length >= 4)
                    {
                        var wikidataId = values[0].Trim('\'');
                        var cityName = values[1].Trim('\'');
                        
                        if (double.TryParse(values[2], out double lat) && 
                            double.TryParse(values[3], out double lon))
                        {
                            cities.Add(new SparQLCityInfo
                            {
                                WikidataId = wikidataId,
                                CityName = cityName,
                                Latitude = lat,
                                Longitude = lon
                            });
                        }
                    }
                }
                catch
                {
                    // Skip malformed lines
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse existing SQL file. Starting fresh.");
        }
        
        return cities;
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
            var uniqueCities = cities
                .GroupBy(c => c.WikidataId)
                .Select(g => g.First())
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

        // Atomic rename
        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }
        File.Move(tempPath, finalPath);

        return finalPath;
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
