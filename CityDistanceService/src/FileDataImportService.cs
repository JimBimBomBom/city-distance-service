using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

/// <summary>
/// Service that imports city data from CSV files in the cities_data directory.
/// CSV files are named as {language}_cities.csv (e.g., en_cities.csv, cs_cities.csv).
///
/// All CSV files feed into both MySQL (with INSERT IGNORE semantics) and Elasticsearch
/// (grouped by city_id with all language variants merged).
/// </summary>
public class FileDataImportService
{
    private readonly string dataPath;
    private readonly ILogger<FileDataImportService> logger;

    /// <summary>
    /// Language codes that were successfully loaded from CSV files during startup.
    /// Populated by <see cref="LoadAllLanguageVariantsAsync"/>.
    /// Used by the /languages endpoint.
    /// </summary>
    public List<string> LoadedLanguages { get; } = new();

    public FileDataImportService(string dataPath, ILogger<FileDataImportService> logger)
    {
        this.dataPath = dataPath;
        this.logger = logger;
    }

    /// <summary>
    /// Returns language codes found in the data directory.
    /// Scans for *_cities.csv files and extracts the language prefix.
    /// </summary>
    public List<string> GetAvailableLanguages()
    {
        if (!Directory.Exists(dataPath))
        {
            return new List<string>();
        }

        var languages = Directory
            .GetFiles(dataPath, "*_cities.csv")
            .Select(ExtractLanguageCode)
            .Where(lang => !string.IsNullOrEmpty(lang))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l)
            .ToList();

        return languages;
    }

    /// <summary>
    /// Loads all language variants from all *_cities.csv files in the data directory.
    /// Files are sorted so that <c>en_cities.csv</c> is processed first, ensuring that
    /// English records establish the baseline when used with <c>INSERT IGNORE</c> semantics.
    /// Each record includes its language code so that Elasticsearch can aggregate
    /// names per city_id for the /suggestions endpoint.
    /// </summary>
    public async Task<List<SparQLCityInfo>> LoadAllLanguageVariantsAsync()
    {
        LoadedLanguages.Clear();

        if (!Directory.Exists(dataPath))
        {
            logger.LogWarning("Data directory not found: {Path}", dataPath);
            return new List<SparQLCityInfo>();
        }

        var csvFiles = Directory.GetFiles(dataPath, "*_cities.csv");

        if (csvFiles.Length == 0)
        {
            logger.LogWarning("No CSV city files found in {Path}", dataPath);
            return new List<SparQLCityInfo>();
        }

        // Sort so that en_cities.csv is processed first
        var sortedFiles = csvFiles
            .OrderBy(f =>
            {
                var lang = ExtractLanguageCode(f);
                return lang == "en" ? 0 : 1;
            })
            .ThenBy(f => Path.GetFileName(f))
            .ToArray();

        logger.LogInformation("Found {Count} CSV language files to process", sortedFiles.Length);

        var allCities = new List<SparQLCityInfo>();

        foreach (var file in sortedFiles)
        {
            var languageCode = ExtractLanguageCode(file);
            if (string.IsNullOrEmpty(languageCode))
            {
                logger.LogWarning("Could not extract language code from {File}", file);
                continue;
            }

            logger.LogInformation("Processing {Language} cities from {File}", languageCode, Path.GetFileName(file));

            var cities = await LoadCitiesFromCsvFileAsync(file);

            if (cities.Count == 0)
            {
                logger.LogWarning("No cities loaded from {File}, skipping language {Language}", Path.GetFileName(file), languageCode);
                continue;
            }

            foreach (var city in cities)
            {
                city.Language = languageCode;
            }

            allCities.AddRange(cities);
            LoadedLanguages.Add(languageCode);
        }

        LoadedLanguages.Sort();

        logger.LogInformation(
            "Loaded {Count} total city records from {LangCount} languages: {Languages}",
            allCities.Count,
            LoadedLanguages.Count,
            string.Join(", ", LoadedLanguages));

        return allCities;
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
                MissingFieldFound = null,
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
                    Population = record.Population,
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
    /// Extracts language code from filename (e.g., "en_cities.csv" -> "en").
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
/// Represents a single city record from CSV files.
/// Supports flexible column naming via CsvHelper mapping.
/// </summary>
public class CsvCityRecord
{
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
