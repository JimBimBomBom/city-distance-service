using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

public class ElasticSearchService : IElasticSearchService
{
    private readonly ElasticsearchClient _client;
    private const string IndexName = "cities";

    public ElasticSearchService(ElasticsearchClient client)
    {
        _client = client;
    }

    // Call this on startup to create the index with proper settings
    public async Task EnsureIndexExistsAsync()
    {
        var existsResponse = await _client.Indices.ExistsAsync(IndexName);

        if (existsResponse.Exists)
        {
            Console.WriteLine($"Index '{IndexName}' already exists.");
            return;
        }

        var createResponse = await _client.Indices.CreateAsync(IndexName, c => c
            .Settings(s => s
                .Analysis(a => a
                    .Analyzers(an => an
                        .Custom("city_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filter(new[] { "lowercase", "asciifolding", "city_edge_ngram" })
                        )
                    )
                    .TokenFilters(tf => tf
                        .EdgeNGram("city_edge_ngram", en => en
                            .MinGram(2)
                            .MaxGram(15)
                        )
                    )
                )
            )
            .Mappings(m => m
                .Properties<CityDoc>(p => p
                    .Keyword(d => d.CityId)
                    .Text(d => d.CityNames, t => t
                        .Analyzer("city_analyzer")
                        .Fields(f => f
                            .Text("_2gram", t2 => t2.Analyzer("standard"))
                            .Text("_3gram", t3 => t3.Analyzer("standard"))
                        )
                    )
                    .GeoPoint(d => d.Location)
                )
            )
        );

        if (createResponse.IsValidResponse)
        {
            Console.WriteLine($"Index '{IndexName}' created successfully.");
        }
        else
        {
            Console.WriteLine($"Failed to create index: {createResponse.DebugInformation}");
            throw new Exception($"ES Index creation failed: {createResponse.DebugInformation}");
        }
    }

    public async Task<string?> GetBestCityIdAsync(string cityName)
    {
        var response = await _client.SearchAsync<CityDoc>(s => s
            .Index(IndexName)
            .Query(q => q
                .Match(m => m
                    .Field(f => f.CityNames)
                    .Query(cityName)
                )
            )
            .Size(1)
        );

        if (!response.IsValidResponse || !response.Documents.Any())
        {
            return null;
        }

        return response.Documents.FirstOrDefault()?.CityId;
    }

    public async Task<IEnumerable<string>> GetCitySuggestionsAsync(string partialName)
    {
        var response = await _client.SearchAsync<CityDoc>(s => s
            .Index(IndexName)
            .Query(q => q
                .MultiMatch(m => m
                    .Query(partialName)
                    .Type(TextQueryType.BoolPrefix)
                    .Fields(new[] { "cityNames", "cityNames._2gram", "cityNames._3gram" })
                )
            )
            .Size(10)
        );

        if (!response.IsValidResponse)
        {
            Console.WriteLine($"ES Search Error: {response.DebugInformation}");
            return Enumerable.Empty<string>();
        }

        return response.Documents
            .Select(d => d.CityNames.FirstOrDefault() ?? "Unknown")
            .Distinct();
    }

public async Task BulkUpsertCitiesAsync(List<SparQLCityInfo> cities)
{
    if (!cities.Any()) return;

    const int batchSize = 500;
    int totalBatches = (int)Math.Ceiling(cities.Count / (double)batchSize);
    int totalSuccessful = 0;
    int totalFailed = 0;
    
    Console.WriteLine($"Processing {cities.Count} cities in {totalBatches} batches of {batchSize}...");

    for (int i = 0; i < cities.Count; i += batchSize)
    {
        var batch = cities.Skip(i).Take(batchSize).ToList();
        int currentBatch = (i / batchSize) + 1;

        try
        {
            var cityDocs = batch.Select(city => new CityDoc
            {
                CityId = city.WikidataId,
                CityNames = city.AllNames,
                Location = GeoLocation.LatitudeLongitude(new LatLonGeoLocation
                {
                    Lat = city.Latitude,
                    Lon = city.Longitude,
                }),
            }).ToList();

            var response = await _client.BulkAsync(b => b
                .Index(IndexName)
                .UpdateMany(cityDocs, (descriptor, cityDoc) => descriptor
                    .Id(cityDoc.CityId)
                    .Script(s => s
                        .Source(@"
                            if (ctx._source.cityNames == null) {
                                ctx._source.cityNames = [];
                            }
                            for (name in params.newNames) {
                                if (!ctx._source.cityNames.contains(name)) {
                                    ctx._source.cityNames.add(name)
                                }
                            }
                            ctx._source.location = params.location
                        ")
                        .Params(p => p
                            .Add("newNames", cityDoc.CityNames)
                            .Add("location", cityDoc.Location)
                        )
                    )
                    .Upsert(cityDoc)
                )
            );

            if (!response.IsValidResponse)
            {
                // Check if there are partial failures
                if (response.ItemsWithErrors.Any())
                {
                    int batchFailed = response.ItemsWithErrors.Count();
                    int batchSuccessful = batch.Count - batchFailed;
                    
                    totalSuccessful += batchSuccessful;
                    totalFailed += batchFailed;
                    
                    Console.WriteLine($"⚠ ES Batch {currentBatch}/{totalBatches}: {batchSuccessful} succeeded, {batchFailed} failed");
                    
                    // Log first few errors
                    foreach (var item in response.ItemsWithErrors.Take(3))
                    {
                        Console.WriteLine($"  Error on city {item.Id}: {item.Error?.Reason}");
                    }
                }
                else
                {
                    // Complete batch failure
                    totalFailed += batch.Count;
                    Console.WriteLine($"✗ ES Batch {currentBatch}/{totalBatches} completely failed");
                    Console.WriteLine($"  Debug Info: {response.DebugInformation}");
                }
            }
            else
            {
                totalSuccessful += batch.Count;
                Console.WriteLine($"✓ ES Batch {currentBatch}/{totalBatches} completed ({batch.Count} cities)");
            }

            // Small delay between batches
            if (i + batchSize < cities.Count)
            {
                await Task.Delay(200);
            }
        }
        catch (Exception ex)
        {
            totalFailed += batch.Count;
            Console.WriteLine($"[ERROR] ES Batch {currentBatch}/{totalBatches} exception: {ex.Message}");
            // Continue with next batch instead of throwing
        }
    }

    Console.WriteLine($"\n=== Elasticsearch Summary ===");
    Console.WriteLine($"Total cities processed: {cities.Count}");
    Console.WriteLine($"✓ Successfully indexed: {totalSuccessful}");
    if (totalFailed > 0)
    {
        Console.WriteLine($"✗ Failed: {totalFailed}");
    }
}

    public async Task UpsertCityAsync(CityDoc city)
    {
        var response = await _client.UpdateAsync<CityDoc, CityDoc>(
            IndexName,
            city.CityId,
            u => u
                .Doc(city)
                .DocAsUpsert(true)
        );

        if (!response.IsValidResponse)
        {
            Console.WriteLine($"ES Upsert Error: {response.DebugInformation}");
            throw new Exception($"Failed to upsert city to Elasticsearch");
        }
    }
}