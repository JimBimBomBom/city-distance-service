using Microsoft.VisualBasic;
using Elastic.Clients.Elasticsearch;

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
        Console.WriteLine("=================================================");
        Console.WriteLine("Starting Wikidata city synchronization");
        Console.WriteLine("=================================================");

        int totalAffected = 0;
        var processedCityIds = new HashSet<string>();

        foreach (var lang in Constants.SupportedLanguages)
        {
            try
            {
                Console.WriteLine($"\n--- Processing Language: {lang} ---");

                var cities = await _wikidataService.FetchCitiesAsync(lang);

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
            await Task.Delay(10000);
        }

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"Sync complete!");
        Console.WriteLine($"Total MySQL records affected: {totalAffected}");
        Console.WriteLine("=================================================");

        return totalAffected;
    }

    public async Task<CityInfo?> FindCityByIdAsync(string cityId)
    {
        try
        {
            if (!string.IsNullOrEmpty(cityId))
            {
                return await _dbManager.GetCity(cityId);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in FindCityByIdAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<CityInfo?> FindCityByIdAsync(string cityId, string language = "en")
    {
        try
        {
            if (string.IsNullOrEmpty(cityId))
            {
                Console.WriteLine("No city ID provided");
                return null;
            }

            // Get authoritative data from MySQL
            var cityInfo = await _dbManager.GetCity(cityId);
            if (cityInfo == null)
            {
                Console.WriteLine($"City ID {cityId} not found in database.");
                return null;
            }

            // Overlay localized names from ES
            var cityDoc = await _esService.GetCityDocByIdAsync(cityId);
            if (cityDoc != null)
            {
                cityInfo.CityName = cityDoc.CityNames.GetValueOrDefault(language)
                    ?? cityDoc.CityNames.GetValueOrDefault(Constants.DefaultLanguage)
                    ?? cityInfo.CityName;  // MySQL fallback

                cityInfo.Country = cityDoc.Country.GetValueOrDefault(language)
                    ?? cityDoc.Country.GetValueOrDefault(Constants.DefaultLanguage)
                    ?? cityInfo.Country;

                cityInfo.AdminRegion = cityDoc.AdminRegion.GetValueOrDefault(language)
                    ?? cityDoc.AdminRegion.GetValueOrDefault(Constants.DefaultLanguage)
                    ?? cityInfo.AdminRegion;
            }

            Console.WriteLine($"Retrieved city: {cityInfo.CityName} (ID: {cityInfo.CityId}, lang: {language})");
            return cityInfo;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in FindCityByIdAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<List<CitySuggestion>> GetCitySuggestionsAsync(string partialName, string language)
    {
        try
        {
            Console.WriteLine($"Getting suggestions for: {partialName} (lang: {language})");
            var suggestions = await _esService.GetCitySuggestionsAsync(partialName, language);
            Console.WriteLine($"Found {suggestions.Count} suggestions.");
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
                CityNames = new Dictionary<string, string>
                    {
                        { Constants.DefaultLanguage, addedCity.CityName }
                    },
                AllNames = new List<string> { addedCity.CityName },
                Location = GeoLocation.LatitudeLongitude(new LatLonGeoLocation
                {
                    Lat = addedCity.Latitude,
                    Lon = addedCity.Longitude,
                }),
                CountryCode = addedCity.CountryCode,
                Country = !string.IsNullOrEmpty(addedCity.Country)
                    ? new Dictionary<string, string> { { Constants.DefaultLanguage, addedCity.Country } }
                    : new(),
                AdminRegion = !string.IsNullOrEmpty(addedCity.AdminRegion)
                    ? new Dictionary<string, string> { { Constants.DefaultLanguage, addedCity.AdminRegion } }
                    : new(),
                Population = addedCity.Population
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
                CityNames = new Dictionary<string, string>
                    {
                        { Constants.DefaultLanguage, updated.CityName }
                    },
                AllNames = new List<string> { updated.CityName },
                Location = GeoLocation.LatitudeLongitude(new LatLonGeoLocation
                {
                    Lat = updated.Latitude,
                    Lon = updated.Longitude
                }),
                CountryCode = updated.CountryCode,
                Country = !string.IsNullOrEmpty(updated.Country)
                    ? new Dictionary<string, string> { { Constants.DefaultLanguage, updated.Country } }
                    : new(),
                AdminRegion = !string.IsNullOrEmpty(updated.AdminRegion)
                    ? new Dictionary<string, string> { { Constants.DefaultLanguage, updated.AdminRegion } }
                    : new(),
                Population = updated.Population
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
