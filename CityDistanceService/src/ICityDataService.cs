// CityDataService.cs - Complete orchestration service
using Elastic.Clients.Elasticsearch;

public interface ICityDataService
{
    Task<int> SyncCitiesFromWikidataAsync();

    Task<CityInfo?> FindCityByNameAsync(string cityName);

    Task<IEnumerable<string>> GetCitySuggestionsAsync(string partialName);

    Task<CityInfo> AddCityAsync(NewCityInfo newCity);

    Task<CityInfo> UpdateCityAsync(CityInfo updatedCity);

    Task DeleteCityAsync(string cityId);
}

public class CityDataService : ICityDataService
{
    private readonly IDatabaseService _dbManager;
    private readonly IElasticSearchService _esService;
    private readonly IWikidataService _wikidataService;

    public CityDataService(
        IDatabaseService dbManager,
        IElasticSearchService esService,
        IWikidataService wikidataService)
    {
        _dbManager = dbManager;
        _esService = esService;
        _wikidataService = wikidataService;
    }

    public async Task<int> SyncCitiesFromWikidataAsync()
    {
        var languages = new[]
        {
            "en", "sk", //"de", "fr", "it", "es",
            // "pt", "nl", "sv", "no", "da", "fi",
            // "pl", "cs", "hu", "ro", "el", "bg",
            // "hr", "sr", "sl", "et", "lv", "lt",
            // "ru", "uk", "tr", "zh", "ja",
        };

        Console.WriteLine("=================================================");
        Console.WriteLine("Starting Wikidata city synchronization");
        Console.WriteLine("=================================================");

        var lastSync = await _dbManager.GetLastSyncAsync();
        Console.WriteLine($"Last sync was: {lastSync:yyyy-MM-dd HH:mm:ss}");

        int totalAffected = 0;
        var processedCityIds = new HashSet<string>();

        foreach (var lang in languages)
        {
            try
            {
                Console.WriteLine($"\n--- Processing Language: {lang} ---");

                var cities = await _wikidataService.FetchCitiesAsync(lastSync, lang);

                if (cities.Count == 0)
                {
                    Console.WriteLine($"No updates for {lang}.");
                    continue;
                }

                Console.WriteLine($"Fetched {cities.Count} cities in {lang}.");

                // For English: Add to MySQL )
                // NOTE: For now we only store English city names which could mean some ES cities will not be found within DB
                if (lang == "en")
                {
                    Console.WriteLine("Adding English cities to MySQL database...");
                    var mysqlAffected = await _dbManager.BulkUpsertCitiesAsync(cities);
                    totalAffected += mysqlAffected;

                    // Track which cities we added
                    foreach (var city in cities)
                    {
                        processedCityIds.Add(city.WikidataId);
                    }

                    Console.WriteLine($"Added {mysqlAffected} new cities to MySQL.");
                }

                // Always update Elasticsearch with all language names
                Console.WriteLine($"Updating Elasticsearch with {lang} city names...");
                await _esService.BulkUpsertCitiesAsync(cities);
                Console.WriteLine($"Updated Elasticsearch with {cities.Count} city names.");
            }
            catch (Exception ex)
            {
                // Log error but continue with next language
                Console.WriteLine($"[ERROR] Failed processing {lang}: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            // Rate limiting - be nice to Wikidata
            Console.WriteLine($"Waiting 30 seconds before processing next language...");
            await Task.Delay(30000);
        }

        // Update sync timestamp
        var newSyncTime = DateTime.UtcNow;
        await _dbManager.UpdateLastSyncAsync(newSyncTime);

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"Sync complete!");
        Console.WriteLine($"Total MySQL records affected: {totalAffected}");
        Console.WriteLine($"New sync timestamp: {newSyncTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("=================================================");

        return totalAffected;
    }

    public async Task<CityInfo?> FindCityByNameAsync(string cityName)
    {
        try
        {
            Console.WriteLine($"Searching for city: {cityName}");
            var cityId = await _esService.GetBestCityIdAsync(cityName);

            if (cityId == null)
            {
                Console.WriteLine($"No city found matching: {cityName}");
                return null;
            }

            Console.WriteLine($"Found city ID: {cityId}");

            // Step 2: Retrieve full city info from database
            var cityInfo = await _dbManager.GetCity(cityId);

            if (cityInfo == null)
            {
                Console.WriteLine($"Warning: City ID {cityId} found in ES but not in database.");
                return null;
            }

            Console.WriteLine($"Retrieved city: {cityInfo.CityName} (ID: {cityInfo.CityId})");
            return cityInfo;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in FindCityByNameAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetCitySuggestionsAsync(string partialName)
    {
        try
        {
            Console.WriteLine($"Getting suggestions for: {partialName}");

            // Elasticsearch handles the search suggestions
            var suggestions = await _esService.GetCitySuggestionsAsync(partialName);

            Console.WriteLine($"Found {suggestions.Count()} suggestions.");
            return suggestions;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetCitySuggestionsAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<CityInfo> AddCityAsync(NewCityInfo newCity)
    {
        try
        {
            Console.WriteLine($"Adding new city: {newCity.CityName}");

            // Step 1: Add to database first
            var addedCity = await _dbManager.AddCity(newCity);
            Console.WriteLine($"City added to database with ID: {addedCity.CityId}");

            // Step 2: Update Elasticsearch
            var cityDoc = new CityDoc
            {
                CityId = addedCity.CityId,
                CityNames = new List<string> { addedCity.CityName },
                Location = GeoLocation.LatitudeLongitude(new LatLonGeoLocation
                {
                    Lat = addedCity.Latitude,
                    Lon = addedCity.Longitude,
                }),
            };

            await _esService.UpsertCityAsync(cityDoc);
            Console.WriteLine($"City indexed in Elasticsearch.");

            return addedCity;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in AddCityAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<CityInfo> UpdateCityAsync(CityInfo updatedCity)
    {
        try
        {
            Console.WriteLine($"Updating city: {updatedCity.CityId}");

            // Step 1: Update database first
            var updated = await _dbManager.UpdateCity(updatedCity);
            Console.WriteLine($"City updated in database.");

            // Step 2: Update Elasticsearch
            var cityDoc = new CityDoc
            {
                CityId = updated.CityId,
                CityNames = new List<string> { updated.CityName },
                Location = GeoLocation.LatitudeLongitude(new LatLonGeoLocation
                {
                    Lat = updated.Latitude,
                    Lon = updated.Longitude
                })
            };

            await _esService.UpsertCityAsync(cityDoc);
            Console.WriteLine($"City updated in Elasticsearch.");

            return updated;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in UpdateCityAsync: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteCityAsync(string cityId)
    {
        try
        {
            Console.WriteLine($"Deleting city: {cityId}");

            // Delete from database
            await _dbManager.DeleteCity(cityId);
            Console.WriteLine($"City deleted from database.");

            // Note: Consider adding DeleteCity method to IElasticSearchService
            // For now, the city remains in ES but won't be found in DB
            Console.WriteLine($"Warning: City still exists in Elasticsearch index.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DeleteCityAsync: {ex.Message}");
            throw;
        }
    }
}