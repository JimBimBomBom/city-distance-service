using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.Core.Search;

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

    public async Task<List<CitySuggestion>> GetCitySuggestionsAsync(string partialName, string language)
    {
        try
        {
            // First try a simple match query to ensure basic search works
            var simpleResponse = await _client.SearchAsync<CityDoc>(s => s
                .Index(IndexName)
                .Query(q => q
                    .Match(m => m
                        .Field(f => f.AllNames)
                        .Query(partialName)
                    )
                )
                .Size(10)
            );

            if (simpleResponse.IsValidResponse && simpleResponse.Documents.Count > 0)
            {
                Console.WriteLine($"Simple search found {simpleResponse.Documents.Count} results for '{partialName}'");
            }
            else if (!simpleResponse.IsValidResponse)
            {
                Console.WriteLine($"Simple search error: {simpleResponse.DebugInformation}");
            }

            // Now do the advanced search with proper sorting
            var searchRequest = new SearchRequestDescriptor<CityDoc>()
                .Index(IndexName)
                .Query(q => q
                    .Bool(b => b
                        .Should(
                            // Exact match on city name
                            sh => sh.MatchPhrase(m => m
                                .Field(f => f.AllNames)
                                .Query(partialName)
                                .Boost(100)
                            ),
                            // Prefix match — good for autocomplete
                            sh => sh.MultiMatch(m => m
                                .Query(partialName)
                                .Type(TextQueryType.BoolPrefix)
                                .Fields(new[] { "allNames", "allNames._2gram", "allNames._3gram" })
                                .Boost(10)
                            ),
                            // Standard match
                            sh => sh.Match(m => m
                                .Field(f => f.AllNames)
                                .Query(partialName)
                                .Boost(5)
                            ),
                            // Fuzzy match — handles typos
                            sh => sh.Match(m => m
                                .Field(f => f.AllNames)
                                .Query(partialName)
                                .Fuzziness(new Fuzziness("AUTO"))
                                .Boost(1)
                            )
                        )
                        .MinimumShouldMatch(1)
                    )
                )
                .Size(50);

            // Apply multi-level sorting using SortOptions
            var sortOptions = new List<SortOptions>
            {
                // Primary: Exact match gets a massive boost
                SortOptions.Script(new ScriptSort
                {
                    Script = new Script
                    {
                        Source = @"
                            // Check if any name is an exact match (case-insensitive)
                            String query = params.query.toLowerCase();
                            for (name in doc['allNames']) {
                                if (name.toLowerCase() == query) {
                                    return 1000; // Massive bonus for exact match
                                }
                            }
                            return 0;
                        ",
                        Params = new Dictionary<string, object> { { "query", partialName } }
                    },
                    Type = ScriptSortType.Number,
                    Order = SortOrder.Desc
                }),
                // Secondary: Text relevance score
                SortOptions.Score(new ScoreSort { Order = SortOrder.Desc }),
                // Tertiary: Population boost (cities with population rank higher, null/0 gets penalty)
                SortOptions.Script(new ScriptSort
                {
                    Script = new Script
                    {
                        Source = @"
                            // Boost by population, penalize missing population data
                            if (doc['population'].size() == 0) {
                                return -1000; // Heavy penalty for missing population
                            }
                            def pop = doc['population'].value;
                            if (pop == null || pop == 0) {
                                return -1000; // Heavy penalty for null/0 population
                            }
                            // Log scale for population to prevent megacities from dominating entirely
                            return Math.log10(pop) * 10;
                        ",
                    },
                    Type = ScriptSortType.Number,
                    Order = SortOrder.Desc
                })
            };

            searchRequest.Sort(sortOptions);

            var response = await _client.SearchAsync<CityDoc>(searchRequest);

            if (!response.IsValidResponse)
            {
                Console.WriteLine($"ES Search Error: {response.DebugInformation}");
                
                // Fallback to simple search if advanced search fails
                if (simpleResponse.IsValidResponse)
                {
                    Console.WriteLine("Using fallback simple search results");
                    return simpleResponse.Documents
                        .Select(d => new CitySuggestion
                        {
                            Id = d.CityId,
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
                        .Take(10)
                        .ToList();
                }
                
                return new List<CitySuggestion>();
            }

            Console.WriteLine($"Advanced search found {response.Documents.Count} results for '{partialName}'");

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
            .Take(10)
            .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in GetCitySuggestionsAsync: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return new List<CitySuggestion>();
        }
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

    public async Task<long> GetDocumentCountAsync()
    {
        try
        {
            var response = await _client.CountAsync<CityDoc>(c => c.Indices(IndexName));
            if (response.IsValidResponse)
            {
                return response.Count;
            }
            Console.WriteLine($"Count error: {response.DebugInformation}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception getting document count: {ex.Message}");
            return 0;
        }
    }

    public async Task BulkUpsertCitiesAsync(List<SparQLCityInfo> cities)
    {
        if (!cities.Any()) return;

        // Group by WikidataId and merge all language variants into a single CityDoc.
        // This avoids relying on Painless scripts to merge dictionaries at index time.
        var cityDocs = cities
            .GroupBy(c => c.WikidataId)
            .Select(g =>
            {
                var first = g.First();
                var doc = new CityDoc
                {
                    CityId = first.WikidataId,
                    CityNames = new Dictionary<string, string>(),
                    AllNames = new List<string>(),
                    Location = GeoLocation.LatitudeLongitude(new LatLonGeoLocation
                    {
                        Lat = first.Latitude,
                        Lon = first.Longitude,
                    }),
                    CountryCode = first.CountryCode,
                    Country = new Dictionary<string, string>(),
                    AdminRegion = new Dictionary<string, string>(),
                    Population = first.Population
                };

                foreach (var city in g)
                {
                    if (!string.IsNullOrEmpty(city.Language) && !string.IsNullOrEmpty(city.CityName))
                    {
                        doc.CityNames[city.Language] = city.CityName;
                        if (!doc.AllNames.Contains(city.CityName))
                            doc.AllNames.Add(city.CityName);
                    }

                    if (!string.IsNullOrEmpty(city.Language) && !string.IsNullOrEmpty(city.Country))
                        doc.Country[city.Language] = city.Country;

                    if (!string.IsNullOrEmpty(city.Language) && !string.IsNullOrEmpty(city.AdminRegion))
                        doc.AdminRegion[city.Language] = city.AdminRegion;
                }

                return doc;
            })
            .ToList();

        const int batchSize = 500;
        int totalBatches = (int)Math.Ceiling(cityDocs.Count / (double)batchSize);
        int totalSuccessful = 0;
        int totalFailed = 0;

        Console.WriteLine($"Processing {cityDocs.Count} unique cities in {totalBatches} batches of {batchSize}...");

        for (int i = 0; i < cityDocs.Count; i += batchSize)
        {
            var batch = cityDocs.Skip(i).Take(batchSize).ToList();
            int currentBatch = (i / batchSize) + 1;

            try
            {
                var response = await _client.BulkAsync(b => b
                    .Index(IndexName)
                    .IndexMany(batch, (descriptor, doc) => descriptor.Id(doc.CityId))
                );

                if (response.Errors)
                {
                    int batchFailed = response.ItemsWithErrors.Count();
                    int batchSuccessful = batch.Count - batchFailed;

                    totalSuccessful += batchSuccessful;
                    totalFailed += batchFailed;

                    Console.WriteLine($"⚠ ES Batch {currentBatch}/{totalBatches}: {batchSuccessful} succeeded, {batchFailed} failed");

                    foreach (var item in response.ItemsWithErrors.Take(5))
                    {
                        Console.WriteLine($"  Error on city {item.Id}: {item.Error?.Reason}");
                    }
                }
                else if (!response.IsValidResponse)
                {
                    totalFailed += batch.Count;
                    Console.WriteLine($"✗ ES Batch {currentBatch}/{totalBatches} completely failed");
                    Console.WriteLine($"  Debug Info: {response.DebugInformation}");
                }
                else
                {
                    totalSuccessful += batch.Count;
                    Console.WriteLine($"✓ ES Batch {currentBatch}/{totalBatches} completed ({batch.Count} cities)");
                }

                if (i + batchSize < cityDocs.Count)
                {
                    await Task.Delay(200);
                }
            }
            catch (Exception ex)
            {
                totalFailed += batch.Count;
                Console.WriteLine($"[ERROR] ES Batch {currentBatch}/{totalBatches} exception: {ex.Message}");
            }
        }

        Console.WriteLine($"\n=== Elasticsearch Summary ===");
        Console.WriteLine($"Total unique cities processed: {cityDocs.Count}");
        Console.WriteLine($"✓ Successfully indexed: {totalSuccessful}");
        if (totalFailed > 0)
        {
            Console.WriteLine($"✗ Failed: {totalFailed}");
        }

        var count = await GetDocumentCountAsync();
        Console.WriteLine($"Total documents in index '{IndexName}': {count}");
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