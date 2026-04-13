using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Service that imports city data from JSON files in the output folder.
/// English cities go to MySQL as source of truth.
/// All languages are aggregated and indexed in Elasticsearch.
/// Tracks which languages were successfully loaded for the /languages endpoint.
/// </summary>
public class FileDataImportService
{
    private readonly string dataPath;
    private readonly ILogger<FileDataImportService> logger;

    /// <summary>
    /// Language codes that were successfully loaded from data files during startup.
    /// Populated by LoadAllLanguageVariantsAsync(). Used by the /languages endpoint.
    /// </summary>
    public List<string> LoadedLanguages { get; } = new();

    public FileDataImportService(string dataPath, ILogger<FileDataImportService> logger)
    {
        this.dataPath = dataPath;
        this.logger = logger;
    }

    /// <summary>
    /// Loads English cities for MySQL (source of truth).
    /// Only loads en_cities.json.
    /// </summary>
    public async Task<List<SparQLCityInfo>> LoadEnglishCitiesAsync()
    {
        var englishFile = Path.Combine(dataPath, "en_cities.json");

        if (!File.Exists(englishFile))
        {
            logger.LogWarning("English cities file not found: {Path}", englishFile);
            return new List<SparQLCityInfo>();
        }

        logger.LogInformation("Loading English cities from {Path}", englishFile);

        var cities = await LoadCitiesFromJsonFileAsync(englishFile);

        var result = cities.Select(c => new SparQLCityInfo
        {
            WikidataId = c.CityId,
            CityName = c.CityName,
            Language = c.Language,
            Latitude = c.Latitude,
            Longitude = c.Longitude,
            Country = c.Country,
            CountryCode = c.CountryCode,
            AdminRegion = c.AdminRegion,
            Population = c.Population
        }).ToList();

        logger.LogInformation("Loaded {Count} English cities", result.Count);
        return result;
    }

    /// <summary>
    /// Loads all language variants from all JSON files.
    /// Each city record includes its language code for ES aggregation.
    /// Also populates LoadedLanguages with successfully loaded language codes.
    /// </summary>
    public async Task<List<SparQLCityInfo>> LoadAllLanguageVariantsAsync()
    {
        LoadedLanguages.Clear();

        if (!Directory.Exists(dataPath))
        {
            logger.LogWarning("Data directory not found: {Path}", dataPath);
            return new List<SparQLCityInfo>();
        }

        var jsonFiles = Directory.GetFiles(dataPath, "*_cities.json");

        if (jsonFiles.Length == 0)
        {
            logger.LogWarning("No city JSON files found in {Path}", dataPath);
            return new List<SparQLCityInfo>();
        }

        logger.LogInformation("Found {Count} language files to process", jsonFiles.Length);

        var allCities = new List<SparQLCityInfo>();

        foreach (var file in jsonFiles)
        {
            var languageCode = ExtractLanguageCode(file);
            if (string.IsNullOrEmpty(languageCode))
            {
                logger.LogWarning("Could not extract language code from {File}", file);
                continue;
            }

            logger.LogInformation("Processing {Language} cities from {File}", languageCode, Path.GetFileName(file));

            var cities = await LoadCitiesFromJsonFileAsync(file);

            if (cities.Count == 0)
            {
                logger.LogWarning("No cities loaded from {File}, skipping language {Language}", Path.GetFileName(file), languageCode);
                continue;
            }

            foreach (var city in cities)
            {
                allCities.Add(new SparQLCityInfo
                {
                    WikidataId = city.CityId,
                    CityName = city.CityName,
                    Language = languageCode,
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Country = city.Country,
                    CountryCode = city.CountryCode,
                    AdminRegion = city.AdminRegion,
                    Population = city.Population
                });
            }

            LoadedLanguages.Add(languageCode);
        }

        LoadedLanguages.Sort();

        logger.LogInformation("Loaded {Count} total city records from {LangCount} languages: {Languages}",
            allCities.Count, LoadedLanguages.Count, string.Join(", ", LoadedLanguages));

        return allCities;
    }

    /// <summary>
    /// Loads cities from a single JSON file.
    /// </summary>
    private async Task<List<JsonCityRecord>> LoadCitiesFromJsonFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var document = JsonSerializer.Deserialize<JsonCityFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (document?.Cities == null)
            {
                logger.LogWarning("No cities found in {File}", filePath);
                return new List<JsonCityRecord>();
            }

            logger.LogDebug("Loaded {Count} cities from {File}", document.Cities.Count, Path.GetFileName(filePath));
            return document.Cities;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse JSON file: {File}", filePath);
            return new List<JsonCityRecord>();
        }
    }

    /// <summary>
    /// Extracts language code from filename (e.g., "en_cities.json" -> "en").
    /// </summary>
    private static string? ExtractLanguageCode(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = fileName.Split('_');

        if (parts.Length >= 2 && parts[1].Equals("cities", StringComparison.OrdinalIgnoreCase))
        {
            return parts[0].ToLowerInvariant();
        }

        return null;
    }
}

/// <summary>
/// Represents the structure of the JSON city files.
/// </summary>
public class JsonCityFile
{
    public JsonMetadata? Metadata { get; set; }

    public List<JsonCityRecord> Cities { get; set; } = new();
}

/// <summary>
/// Metadata section of JSON city files.
/// </summary>
public class JsonMetadata
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("fetched_at")]
    public string FetchedAt { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("tool")]
    public string Tool { get; set; } = string.Empty;

    [JsonPropertyName("tool_version")]
    public string ToolVersion { get; set; } = string.Empty;

    [JsonPropertyName("total_records")]
    public int TotalRecords { get; set; }
}

/// <summary>
/// Represents a single city record from the JSON files (snake_case fields).
/// </summary>
public class JsonCityRecord
{
    [JsonPropertyName("city_id")]
    public string CityId { get; set; } = string.Empty;

    [JsonPropertyName("city_name")]
    public string CityName { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("admin_region")]
    public string? AdminRegion { get; set; }

    [JsonPropertyName("population")]
    public int? Population { get; set; }
}
