using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using CsvHelper.Configuration;

/// <summary>
/// Service that imports city data from JSON and CSV files in the /cities_data folder.
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

    /// <summary>
    /// Returns language codes found in the data directory (fallback if LoadedLanguages is empty).
    /// This scans the filesystem without loading actual city data.
    /// </summary>
    public List<string> GetAvailableLanguages()
    {
        if (LoadedLanguages.Count > 0)
        {
            return LoadedLanguages.ToList();
        }

        // Fallback: scan directory for language files
        if (!Directory.Exists(dataPath))
        {
            return new List<string>();
        }

        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var jsonFiles = Directory.GetFiles(dataPath, "*_cities.json");
        var csvFiles = Directory.GetFiles(dataPath, "*_cities.csv");

        foreach (var file in jsonFiles.Concat(csvFiles))
        {
            var lang = ExtractLanguageCode(file);
            if (!string.IsNullOrEmpty(lang))
            {
                languages.Add(lang);
            }
        }

        return languages.OrderBy(l => l).ToList();
    }

    public FileDataImportService(string dataPath, ILogger<FileDataImportService> logger)
    {
        this.dataPath = dataPath;
        this.logger = logger;
    }

    /// <summary>
    /// Loads English cities for MySQL (source of truth).
    /// Tries en_cities.json first, then en_cities.csv.
    /// </summary>
    public async Task<List<SparQLCityInfo>> LoadEnglishCitiesAsync()
    {
        var jsonFile = Path.Combine(dataPath, "en_cities.json");
        var csvFile = Path.Combine(dataPath, "en_cities.csv");

        List<SparQLCityInfo> cities = new();

        if (File.Exists(jsonFile))
        {
            logger.LogInformation("Loading English cities from JSON: {Path}", jsonFile);
            cities = await LoadCitiesFromJsonFileAsync(jsonFile);
        }
        else if (File.Exists(csvFile))
        {
            logger.LogInformation("Loading English cities from CSV: {Path}", csvFile);
            cities = await LoadCitiesFromCsvFileAsync(csvFile);
        }
        else
        {
            logger.LogWarning("English cities file not found (tried: {JsonPath}, {CsvPath})", jsonFile, csvFile);
            return new List<SparQLCityInfo>();
        }

        // Set language code for all cities
        foreach (var city in cities)
        {
            city.Language = "en";
        }

        logger.LogInformation("Loaded {Count} English cities", cities.Count);
        return cities;
    }

    /// <summary>
    /// Loads all language variants from all JSON and CSV files.
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

        // Get all city files (both JSON and CSV)
        var jsonFiles = Directory.GetFiles(dataPath, "*_cities.json");
        var csvFiles = Directory.GetFiles(dataPath, "*_cities.csv");

        // Combine and deduplicate by language (JSON takes precedence over CSV)
        var allFiles = jsonFiles.ToList();
        var jsonLanguages = jsonFiles.Select(f => ExtractLanguageCode(f)).Where(l => l != null).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Only add CSV files for languages not covered by JSON
        foreach (var csvFile in csvFiles)
        {
            var lang = ExtractLanguageCode(csvFile);
            if (lang != null && !jsonLanguages.Contains(lang))
            {
                allFiles.Add(csvFile);
            }
        }

        if (allFiles.Count == 0)
        {
            logger.LogWarning("No city files (JSON or CSV) found in {Path}", dataPath);
            return new List<SparQLCityInfo>();
        }

        logger.LogInformation("Found {Count} language files to process", allFiles.Count);

        var allCities = new List<SparQLCityInfo>();

        foreach (var file in allFiles)
        {
            var languageCode = ExtractLanguageCode(file);
            if (string.IsNullOrEmpty(languageCode))
            {
                logger.LogWarning("Could not extract language code from {File}", file);
                continue;
            }

            logger.LogInformation("Processing {Language} cities from {File}", languageCode, Path.GetFileName(file));

            List<SparQLCityInfo> cities;
            var extension = Path.GetExtension(file).ToLowerInvariant();

            if (extension == ".json")
            {
                cities = await LoadCitiesFromJsonFileAsync(file);
            }
            else if (extension == ".csv")
            {
                cities = await LoadCitiesFromCsvFileAsync(file);
            }
            else
            {
                logger.LogWarning("Unknown file extension for {File}", file);
                continue;
            }

            if (cities.Count == 0)
            {
                logger.LogWarning("No cities loaded from {File}, skipping language {Language}", Path.GetFileName(file), languageCode);
                continue;
            }

            // Set the language code for all cities from this file
            foreach (var city in cities)
            {
                city.Language = languageCode;
            }

            allCities.AddRange(cities);
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
    private async Task<List<SparQLCityInfo>> LoadCitiesFromJsonFileAsync(string filePath)
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
                return new List<SparQLCityInfo>();
            }

            logger.LogDebug("Loaded {Count} cities from {File}", document.Cities.Count, Path.GetFileName(filePath));

            return document.Cities.Select(c => new SparQLCityInfo
            {
                WikidataId = c.CityId,
                CityName = c.CityName,
                Latitude = c.Latitude,
                Longitude = c.Longitude,
                Country = c.Country,
                CountryCode = c.CountryCode,
                AdminRegion = c.AdminRegion,
                Population = c.Population
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse JSON file: {File}", filePath);
            return new List<SparQLCityInfo>();
        }
    }

    /// <summary>
    /// Loads cities from a single CSV file.
    /// </summary>
    private async Task<List<SparQLCityInfo>> LoadCitiesFromCsvFileAsync(string filePath)
    {
        try
        {
            var cities = new List<SparQLCityInfo>();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            await foreach (var record in csv.GetRecordsAsync<CsvCityRecord>())
            {
                if (string.IsNullOrEmpty(record.CityId) || string.IsNullOrEmpty(record.CityName))
                {
                    continue;
                }

                cities.Add(new SparQLCityInfo
                {
                    WikidataId = record.CityId,
                    CityName = record.CityName,
                    Latitude = record.Latitude,
                    Longitude = record.Longitude,
                    Country = record.Country,
                    CountryCode = record.CountryCode,
                    AdminRegion = record.AdminRegion,
                    Population = record.Population
                });
            }

            logger.LogDebug("Loaded {Count} cities from {File}", cities.Count, Path.GetFileName(filePath));
            return cities;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse CSV file: {File}", filePath);
            return new List<SparQLCityInfo>();
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

/// <summary>
/// Represents a single city record from CSV files.
/// Supports flexible column naming via CsvHelper mapping.
/// </summary>
public class CsvCityRecord
{
    // Using CsvHelper.Name attribute to support multiple column naming conventions
    [CsvHelper.Configuration.Attributes.Name("city_id", "id", "wikidata_id", "cityId")]
    public string CityId { get; set; } = string.Empty;

    [CsvHelper.Configuration.Attributes.Name("city_name", "name", "cityName", "city")]
    public string CityName { get; set; } = string.Empty;

    [CsvHelper.Configuration.Attributes.Name("latitude", "lat")]
    public double Latitude { get; set; }

    [CsvHelper.Configuration.Attributes.Name("longitude", "lon", "lng")]
    public double Longitude { get; set; }

    [CsvHelper.Configuration.Attributes.Name("country", "country_name")]
    public string? Country { get; set; }

    [CsvHelper.Configuration.Attributes.Name("country_code", "countryCode", "country_code", "cc")]
    public string? CountryCode { get; set; }

    [CsvHelper.Configuration.Attributes.Name("admin_region", "adminRegion", "region", "state", "province")]
    public string? AdminRegion { get; set; }

    [CsvHelper.Configuration.Attributes.Name("population", "pop")]
    public int? Population { get; set; }
}
