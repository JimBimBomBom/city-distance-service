using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.VisualBasic;
using System.Security.AccessControl;

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
                    // cityNames is an object with dynamic keys — ES handles this as "object" type
                    .Object(d => d.CityNames, o => o.Enabled(true))
                    // allNames is what we actually search against
                    .Text(d => d.AllNames, t => t
                        .Analyzer("city_analyzer")
                        .Fields(f => f
                            .Text("_2gram", t2 => t2.Analyzer("standard"))
                            .Text("_3gram", t3 => t3.Analyzer("standard"))
                        )
                    )
                    .GeoPoint(d => d.Location)
                    .Keyword(d => d.CountryCode)
                    .Object(d => d.Country, o => o.Enabled(true))
                    .Object(d => d.AdminRegion, o => o.Enabled(true))
                    .IntegerNumber(d => d.Population)
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

    public async Task<List<CitySuggestion>> GetCitySuggestionsAsync(string partialName, string language)
    {
        var response = await _client.SearchAsync<CityDoc>(s => s
            .Index(IndexName)
            .Query(q => q
                .Bool(b => b
                    .Should(
                        // Exact match - highest priority
                        sh => sh.Match(m => m
                            .Field(f => f.AllNames)
                            .Query(partialName)
                            .Boost(10)
                        ),
                        // Prefix match - good for autocomplete
                        sh => sh.MultiMatch(m => m
                            .Query(partialName)
                            .Type(TextQueryType.BoolPrefix)
                            .Fields(new[] { "allNames", "allNames._2gram", "allNames._3gram" })
                            .Boost(2)
                        ),
                        // Fuzzy match - handles typos
                        sh => sh.Match(m => m
                            .Field(f => f.AllNames)
                            .Query(partialName)
                            .Fuzziness(new Fuzziness("AUTO"))
                            .Boost(1)
                        )
                    )
                )
            )
            .Sort(sort => sort
                .Score(new ScoreSort { Order = SortOrder.Desc })
                .Field(f => f.Population, new FieldSort { Order = SortOrder.Desc, Missing = 0 })
            )
            .Size(10)
        );

        if (!response.IsValidResponse)
        {
            Console.WriteLine($"ES Search Error: {response.DebugInformation}");
            return new List<CitySuggestion>();
        }

        return response.Documents
        .Select(d => new CitySuggestion
        {
            Id = d.CityId,
            // Return the name in the requested language, fall back to English, then any name
            Name = d.CityNames.GetValueOrDefault(language)
                ?? d.CityNames.GetValueOrDefault(Constants.DefaultLanguage)
                ?? d.AllNames.FirstOrDefault()
                ?? "Unknown",
            CountryCode = d.CountryCode,
            Country = d.Country.GetValueOrDefault(language)
                ?? d.Country.GetValueOrDefault(Constants.DefaultLanguage)
                ?? "",
            AdminRegion = d.AdminRegion.GetValueOrDefault(language)
                ?? d.AdminRegion.GetValueOrDefault(Constants.DefaultLanguage)
                ?? "",
            Population = d.Population
        })
        .ToList();
    }

    public async Task<CityDoc?> GetCityDocByIdAsync(string cityId)
    {
        var response = await _client.GetAsync<CityDoc>(IndexName, cityId);

        if (!response.IsValidResponse || response.Source == null)
        {
            Console.WriteLine($"ES Get by ID not found: {cityId}");
            return null;
        }

        return response.Source;
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
                var cityDocs = batch.Select(city =>
                {
                    var doc = new CityDoc
                    {
                        CityId = city.WikidataId,
                        CityNames = new Dictionary<string, string>(),    // Build the dictionary
                        AllNames = new(), 
                        Location = GeoLocation.LatitudeLongitude(new LatLonGeoLocation
                        {
                            Lat = city.Latitude,
                            Lon = city.Longitude,
                        }),
                        CountryCode = city.CountryCode,
                        Country = new Dictionary<string, string>(),
                        AdminRegion = new Dictionary<string, string>(),
                        Population = city.Population
                    };

                    // Populate language-keyed dictionaries
                    if (!string.IsNullOrEmpty(city.Language) && !string.IsNullOrEmpty(city.CityName))
                        doc.CityNames[city.Language] = city.CityName;

                    if (!string.IsNullOrEmpty(city.Language) && !string.IsNullOrEmpty(city.Country))
                        doc.Country[city.Language] = city.Country;

                    if (!string.IsNullOrEmpty(city.Language) && !string.IsNullOrEmpty(city.AdminRegion))
                        doc.AdminRegion[city.Language] = city.AdminRegion;

                    return doc;
                }).ToList();

                var response = await _client.BulkAsync(b => b
                    .Index(IndexName)
                    .UpdateMany(cityDocs, (descriptor, cityDoc) => descriptor
                        .Id(cityDoc.CityId)
                        .Script(s => s
                            .Source(@"
                                // Merge cityNames dictionary
                                if (ctx._source.cityNames == null) {
                                    ctx._source.cityNames = params.cityNames;
                                } else {
                                    for (entry in params.cityNames.entrySet()) {
                                        ctx._source.cityNames[entry.getKey()] = entry.getValue();
                                    }
                                }

                                // Rebuild allNames from cityNames values
                                def names = new HashSet();
                                for (entry in ctx._source.cityNames.entrySet()) {
                                    names.add(entry.getValue());
                                }
                                ctx._source.allNames = new ArrayList(names);

                                // Update other metadata
                                ctx._source.location = params.location;
                                ctx._source.countryCode = params.countryCode;

                                if (ctx._source.country == null) {
                                    ctx._source.country = params.country;
                                } else {
                                    for (entry in params.country.entrySet()) {
                                        ctx._source.country[entry.getKey()] = entry.getValue();
                                    }
                                }

                                if (ctx._source.adminRegion == null) {
                                    ctx._source.adminRegion = params.adminRegion;
                                } else {
                                    for (entry in params.adminRegion.entrySet()) {
                                        ctx._source.adminRegion[entry.getKey()] = entry.getValue();
                                    }
                                }

                                ctx._source.population = params.population;
                            ")
                            .Params(p => p
                                .Add("cityNames", cityDoc.CityNames)           // was "newNames"
                                .Add("location", cityDoc.Location)
                                .Add("countryCode", cityDoc.CountryCode ?? "")
                                .Add("country", cityDoc.Country)                // pass dict, not string
                                .Add("adminRegion", cityDoc.AdminRegion)        // pass dict, not string
                                .Add("population", cityDoc.Population ?? 0)
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