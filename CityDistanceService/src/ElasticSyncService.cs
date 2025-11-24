using Nest;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ElasticSyncService
{
    private readonly ElasticClient _client;
    private const string IndexName = "cities";

    public ElasticSyncService(string elasticsearchUri)
    {
        var settings = new ConnectionSettings(new Uri(elasticsearchUri))
            // This is crucial for autonomous operation:
            .MaxRetries(5) 
            .RequestTimeout(TimeSpan.FromSeconds(30)); 

        _client = new ElasticClient(settings);
    }

    // New method for autonomous index creation
    public async Task CheckAndCreateIndexAsync()
    {
        // 1. Check if the index already exists
        var existsResponse = await _client.Indices.ExistsAsync(IndexName);

        if (existsResponse.Exists)
        {
            Console.WriteLine($"Elasticsearch index '{IndexName}' already exists. Skipping creation.");
            return;
        }

        Console.WriteLine($"Elasticsearch index '{IndexName}' not found. Creating index with multilingual mapping...");

        // 2. Define the multilingual mapping using NEST's fluent API
        var createIndexResponse = await _client.Indices.CreateAsync(IndexName, c => c
            .Map<ElasticCityDocument>(m => m
                .Properties(p => p
                    // Standard fields
                    .Keyword(k => k.Name(n => n.CityId))
                    .GeoPoint(g => g.Name(n => n.Location))
                    .Date(d => d.Name(n => n.Modified))
                    .Number(n => n.Name(n => n.Latitude).Type(NumberType.Float))
                    .Number(n => n.Name(n => n.Longitude).Type(NumberType.Float))

                    // Multilingual Field (CityName)
                    .Object<Dictionary<string, string>>(o => o
                        .Name(n => n.CityName)
                        .Properties(pp => pp
                            // Dynamic mapping for languages: If we index CityName.zh, ES will create a text field.
                            // We explicitly define the most common ones with their analyzers.
                            // This ensures maximum performance and linguistic accuracy for the main languages.
                            .Text(t => t.Name("en").Analyzer("english"))
                            .Text(t => t.Name("de").Analyzer("german"))
                            .Text(t => t.Name("fr").Analyzer("french"))
                            // Add a raw keyword field for exact matching (e.g., filtering or aggregation)
                            .Keyword(k => k.Name("raw")) 
                        )
                    )
                )
            )
        );

        // 3. Handle the response
        if (createIndexResponse.IsValid)
        {
            Console.WriteLine($"Elasticsearch index '{IndexName}' created successfully.");
        }
        else
        {
            Console.WriteLine($"Error creating index: {createIndexResponse.ServerError?.Error.Reason}");
            // Log response.DebugInformation
            throw new Exception($"Failed to autonomously create Elasticsearch index: {createIndexResponse.OriginalException?.Message}");
        }
    }

    public async Task SyncCitiesToElasticsearchAsync()
    {
        // 1. Autonomous Setup
        await CheckAndCreateIndexAsync(); 
        
        // 2. Synchronization Logic
        var lastSync = await GetLastSyncAsync();
        var cities = await FetchCitiesAsync(lastSync); // Fetch cities from Wikidata

        if (cities.Count > 0)
        {
            // New method to handle indexing
            var indexedCount = await BulkIndexCitiesAsync(cities); 
            Console.WriteLine($"Indexed {indexedCount} new/updated cities into Elasticsearch.");

            await UpdateLastSyncAsync(DateTime.UtcNow);
        }
        else
        {
            Console.WriteLine("No new or updated cities found from Wikidata.");
        }
    }
}