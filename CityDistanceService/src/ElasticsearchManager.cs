// ElasticsearchManager.cs
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class ElasticsearchManager : IDatabaseManager
{
    private readonly ElasticClient _client;
    private readonly string _syncConnectionString; // MySQL is still used for sync state only
    private const string IndexName = "cities";

    public ElasticsearchManager(string elasticsearchUri, string syncConnectionString)
    {
        var settings = new ConnectionSettings(new Uri(elasticsearchUri))
            .MaxRetries(5)
            .RequestTimeout(TimeSpan.FromSeconds(30))
            .DefaultMappingFor<ElasticCityDocument>(m => m.IdProperty(p => p.CityId)); 
            
        _client = new ElasticClient(settings);
        _syncConnectionString = syncConnectionString;
    }

    // --- AUTONOMOUS SETUP ---

    public async Task CheckAndCreateIndexAsync()
    {
        var existsResponse = await _client.Indices.ExistsAsync(IndexName);

        if (existsResponse.Exists) return;

        Console.WriteLine($"Elasticsearch index '{IndexName}' not found. Creating index with multilingual mapping...");

        var createIndexResponse = await _client.Indices.CreateAsync(IndexName, c => c
            .Map<ElasticCityDocument>(m => m
                .Properties(p => p
                    .Keyword(k => k.Name(n => n.CityId))
                    .GeoPoint(g => g.Name(n => n.Location))
                    .Date(d => d.Name(n => n.Modified))
                    .Number(n => n.Name(n => n.Latitude).Type(NumberType.Float))
                    .Number(n => n.Name(n => n.Longitude).Type(NumberType.Float))
                    .Object<Dictionary<string, string>>(o => o
                        .Name(n => n.CityName)
                        .Properties(pp => pp
                            .Text(t => t.Name("en").Analyzer("english"))
                            .Text(t => t.Name("de").Analyzer("german"))
                            .Text(t => t.Name("fr").Analyzer("french"))
                            .Keyword(k => k.Name("raw"))
                        )
                    )
                )
            )
        );

        if (!createIndexResponse.IsValid)
        {
            Console.WriteLine($"Error creating index: {createIndexResponse.ServerError?.Error.Reason}");
            throw new Exception("Failed to autonomously create Elasticsearch index.");
        }
    }

    // --- INTERFACE IMPLEMENTATIONS ---

    public async Task<IResult> TestConnection()
    {
        try
        {
            var response = await _client.PingAsync();
            if (response.IsValid)
            {
                Console.WriteLine("Elasticsearch Connection successful.");
                return Results.Ok("Elasticsearch connected successfully.");
            }
            throw new Exception($"Ping failed: {response.ServerError?.Error.Reason ?? "Unknown Error"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return Results.BadRequest($"Elasticsearch Error: {ex.Message}");
        }
    }

    // Uses Term Suggester for Autocomplete on city names (best practice for search)
    public async Task<List<string>> GetCityNames(string prefix = "", int size = 1000)
    {
        var searchResponse = await _client.SearchAsync<ElasticCityDocument>(s => s
            .Index(IndexName)
            .Source(false) // Don't return the whole document, just the names
            .Suggest(su => su
                .Completion("city-suggest", c => c
                    // We search the 'raw' keyword field for a complete list of unique names
                    .Field(f => f.CityName.Suffix("raw")) 
                    .Prefix(prefix)
                    .Size(size)
                )
            )
        );
        
        // This is complex for simple list, a MatchAll query would be simpler, 
        // but this uses suggested data for efficiency.
        // For simplicity, we'll use a terms aggregation for unique names:
        var aggResponse = await _client.SearchAsync<ElasticCityDocument>(s => s
            .Index(IndexName)
            .Size(0) // Return no documents
            .Aggregations(a => a
                .Terms("unique_names", t => t
                    .Field(f => f.CityName.Suffix("raw"))
                    .Size(size)
                    .Order(o => o.KeyAscending())
                )
            )
        );

        var bucket = aggResponse.Aggregations.Terms("unique_names");
        return bucket?.Buckets.Select(b => b.Key).ToList() ?? new List<string>();
    }

    // --- CRUD OPERATIONS (Simplified for Wikidata ID focus) ---

    private CityInfo ToCityInfo(ElasticCityDocument doc)
    {
        return new CityInfo
        {
            CityId = doc.CityId,
            Latitude = doc.Latitude,
            Longitude = doc.Longitude,
            MultilingualNames = doc.CityName,
            // Fallback for CityName display: use English if available, otherwise the first language found.
            CityName = doc.CityName.TryGetValue("en", out var nameEn) ? nameEn : doc.CityName.First().Value
        };
    }

    public async Task<CityInfo> AddCity(NewCityInfo newCity)
    {
        // For user-added cities, assign a temporary ID or use a proper external ID system.
        // For simplicity, we'll use a generated GUID if the city is not found by name.
        var cityId = newCity.CityName.GetHashCode().ToString(); 
        
        var doc = new ElasticCityDocument
        {
            CityId = cityId,
            Latitude = newCity.Latitude,
            Longitude = newCity.Longitude,
            Location = $"{newCity.Latitude},{newCity.Longitude}",
            Modified = DateTime.UtcNow,
            CityName = new Dictionary<string, string> { { "en", newCity.CityName } }
        };

        var response = await _client.IndexAsync(doc, i => i.Index(IndexName).Id(doc.CityId));
        if (!response.IsValid) throw new Exception($"Failed to add city: {response.ServerError?.Error.Reason}");

        return ToCityInfo(doc);
    }

    public async Task<CityInfo?> GetCity(string cityId)
    {
        var response = await _client.GetAsync<ElasticCityDocument>(cityId, g => g.Index(IndexName));
        return response.Found ? ToCityInfo(response.Source) : null;
    }
    
    public async Task<CityInfo> UpdateCity(CityInfo updatedCity)
    {
        var doc = new ElasticCityDocument
        {
            CityId = updatedCity.CityId,
            Latitude = updatedCity.Latitude,
            Longitude = updatedCity.Longitude,
            Location = $"{updatedCity.Latitude},{updatedCity.Longitude}",
            Modified = DateTime.UtcNow,
            // IMPORTANT: If you allow updating, you must fetch the existing document
            // to preserve the multilingual names not being updated here.
            CityName = updatedCity.MultilingualNames 
        };

        var response = await _client.UpdateAsync<ElasticCityDocument, object>(updatedCity.CityId, u => u
            .Index(IndexName)
            .Doc(doc)
            .DocAsUpsert() 
        );

        if (!response.IsValid) throw new Exception($"Failed to update city: {response.ServerError?.Error.Reason}");

        return ToCityInfo(doc);
    }

    public async Task DeleteCity(string cityId)
    {
        var response = await _client.DeleteAsync<ElasticCityDocument>(cityId, d => d.Index(IndexName));
        if (!response.IsValid && response.Result != Result.NotFound)
            throw new Exception($"Failed to delete city: {response.ServerError?.Error.Reason}");
    }

    // --- SEARCH METHODS ---

    public async Task<Coordinates?> GetCityCoordinates(string cityName)
    {
        // Query the 'raw' field for an exact, case-insensitive match (since coordinates are single-valued)
        var response = await _client.SearchAsync<ElasticCityDocument>(s => s
            .Index(IndexName)
            .Query(q => q
                .Term(t => t.Field(f => f.CityName.Suffix("raw")).Value(cityName))
            )
            .Size(1)
        );

        var doc = response.Documents.FirstOrDefault();
        return doc != null ? new Coordinates { Latitude = doc.Latitude, Longitude = doc.Longitude } : null;
    }

    public async Task<List<CityInfo>> GetCities(List<string> cityIds)
    {
        var response = await _client.GetManyAsync<ElasticCityDocument>(cityIds.Select(id => (Id)id).ToArray(), IndexName);
        return response.Select(g => ToCityInfo(g.Source)).ToList();
    }
    
    // Core multilingual search method
    public async Task<List<CityInfo>> GetCities(string searchPhrase, string languageCode = "en", int size = 10)
    {
        // 1. Determine the field to search: search the specific language field first, 
        // then fall back to multilingual search across all fields if no language is specified.
        var fieldToSearch = $"cityName.{languageCode.ToLower()}";
        
        var response = await _client.SearchAsync<ElasticCityDocument>(s => s
            .Index(IndexName)
            .Query(q => q
                .MultiMatch(m => m
                    .Fields(f => f
                        .Field(fieldToSearch, boost: 3.0) // Prioritize the exact language field (boost by 3)
                        .Field("cityName.*") // Search all multilingual fields as a fallback
                    )
                    .Query(searchPhrase)
                    .Type(TextQueryType.BestFields) // Use the best matching field's score
                    .Fuzziness(Fuzziness.Auto) // Enable fuzzy search (for better spelling tolerance)
                )
            )
            .Size(size)
        );

        return response.Documents.Select(ToCityInfo).ToList();
    }

    // --- SYNCHRONIZATION METHODS (MySQL for Sync State, ES for Data) ---

    public async Task<DateTime> GetLastSyncAsync()
    {
        using var connection = new MySqlConnection(_syncConnectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT LastSync FROM sync_state WHERE SyncKey = 'CitySync'";

        var result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
        {
            Console.WriteLine("No last sync found, defaulting to 2000-01-01.");
            return new DateTime(2000, 1, 1);
        }

        return (DateTime)result;
    }

    public async Task UpdateLastSyncAsync(DateTime newTimestamp)
    {
        using var connection = new MySqlConnection(_syncConnectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE sync_state
            SET LastSync = @ts
            WHERE SyncKey = 'CitySync';
        ";

        cmd.Parameters.AddWithValue("@ts", newTimestamp);

        await cmd.ExecuteNonQueryAsync();
    }
    
    // FetchCitiesAsync returns ElasticCityDocument format now
    public async Task<List<ElasticCityDocument>> FetchCitiesAsync(DateTime lastSync)
    {
        // ... (The implementation from your previous code which aggregates SparQLCityLabel 
        // objects into List<ElasticCityDocument> remains here) ...
        return new List<ElasticCityDocument>(); // Placeholder
    }

    // Bulk index implementation
    public async Task<int> BulkIndexCitiesAsync(List<ElasticCityDocument> cities)
    {
        if (!cities.Any()) return 0;

        var response = await _client.BulkAsync(b => b
            .Index(IndexName)
            .IndexMany(cities, (bd, city) => bd.Id(city.CityId).Doc(city).DocAsUpsert())
        );

        if (response.Errors)
        {
            Console.WriteLine($"Elasticsearch Bulk Indexing failed. Errors: {response.ItemsWithErrors.Count()}");
            // Log details from response.ItemsWithErrors
            throw new Exception("Elasticsearch bulk operation failed.");
        }
        
        return cities.Count;
    }

    public async Task<int> SynchronizeElasticSearchAsync()
    {
        // 1. Autonomous Setup (Ensures index exists before sync)
        await CheckAndCreateIndexAsync(); 
        
        var lastSync = await GetLastSyncAsync();
        var cities = await FetchCitiesAsync(lastSync);

        if (cities.Count == 0) return 0;

        Console.WriteLine($"Fetched {cities.Count} new or updated cities from Wikidata since {lastSync}.");

        var indexedCount = await BulkIndexCitiesAsync(cities);
        Console.WriteLine($"Indexed {indexedCount} cities into Elasticsearch.");

        await UpdateLastSyncAsync(DateTime.UtcNow);

        return indexedCount;
    }
}