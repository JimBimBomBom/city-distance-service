using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.Core.Search;

public class ElasticSearchService : IElasticSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly string IndexName;

    public ElasticSearchService(ElasticsearchClient client, string indexName = "cities")
    {
        _client = client;
        IndexName = indexName;
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
                .MaxNgramDiff(2)
                .Analysis(a => a
                    .Analyzers(an => an
                        .Custom("city_ngram_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filter(new[] { "lowercase", "asciifolding", "city_ngram_filter" })
                        )
                        .Custom("city_plain_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filter(new[] { "lowercase", "asciifolding" })
                        )
                    )
                    .TokenFilters(tf => tf
                        .NGram("city_ngram_filter", n => n
                            .MinGram(2)
                            .MaxGram(4)
                        )
                    )
                    .Normalizers(n => n
                        .Custom("city_keyword_normalizer", cn => cn
                            .Filter(new[] { "lowercase", "asciifolding" })
                        )
                    )
                )
            )
            .Mappings(m => m
                .Properties<CityDoc>(p => p
                    .Keyword(d => d.CityId)
                    // cityNames is an object with dynamic keys — ES handles this as "object" type
                    .Object(d => d.CityNames, o => o.Enabled(true))
                    .Text(d => d.AllNames, t => t
                        .Analyzer("city_ngram_analyzer")
                        .SearchAnalyzer("city_ngram_analyzer")
                        .Norms(false)
                        .Fields(f => f
                            // Normalized keyword subfield for exact full-name matches.
                            .Keyword("keyword", k => k.Normalizer("city_keyword_normalizer"))
                            // Plain-analyzed (no n-grams) subfield used for fuzzy
                            // matching against the full token. This is the only place
                            // ES's Fuzziness can correct typos in the leading chars
                            // (e.g. "tkyo" -> "tokyo") without being drowned by the
                            // n-gram term space.
                            .Text("plain", ft => ft
                                .Analyzer("city_plain_analyzer")
                                .SearchAnalyzer("city_plain_analyzer")
                            )
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
            // Normalize the query for the keyword-based clauses (term/prefix). The
            // AllNames.keyword subfield's normalizer lowercases + asciifolds at index
            // time, but term/prefix queries do NOT run the normalizer on the input.
            var normalizedQuery = partialName.Trim().ToLowerInvariant();

            // Search strategy (clauses are OR-ed; minimum_should_match: 1):
            //   1. term  on AllNames.keyword               -> exact full-name match  boost 1000
            //                                                 (pins exact hits to the top)
            //   2. match on AllNames (1- and 2-grams)      -> character n-gram       boost 100
            //                                                 overlap with MinimumShouldMatch
            //                                                 "70%" so weakly-related docs
            //                                                 are filtered out.
            //   3. match on AllNames.plain with Fuzziness  -> typo correction        boost 30
            //                                                 (e.g. "tkyo" -> "tokyo",
            //                                                  "pris" -> "paris" — the
            //                                                  cases the n-gram clause
            //                                                  cannot rank highly because
            //                                                  the typo destroys n-gram
            //                                                  overlap).
            //
            // Wrapped in a function_score that multiplies the text score by
            // log1p(population) so among equally-good text matches the more
            // populous city wins decisively (Tokyo 14M -> ~16x, Tokod 4k -> ~8x).
            var searchRequest = new SearchRequestDescriptor<CityDoc>()
                .Index(IndexName)
                .Query(q => q
                    .FunctionScore(fs => fs
                        .Query(qq => qq
                            .Bool(b => b
                                .Should(
                                    sh => sh.Term(t => t
                                        .Field("allNames.keyword")
                                        .Value(normalizedQuery)
                                        .Boost(1000)
                                    ),
                                    sh => sh.Match(m => m
                                        .Field(f => f.AllNames)
                                        .Query(partialName)
                                        .Operator(Operator.Or)
                                        .MinimumShouldMatch("70%")
                                        .Boost(100)
                                    ),
                                    sh => sh.Match(m => m
                                        .Field("allNames.plain")
                                        .Query(partialName)
                                        .Fuzziness(new Fuzziness(2))
                                        .PrefixLength(1)
                                        .MaxExpansions(500)
                                        .Boost(30)
                                    )
                                )
                                .MinimumShouldMatch(1)
                            )
                        )
                        .Functions(fn => fn
                            .FieldValueFactor(fvf => fvf
                                .Field(f => f.Population)
                                .Modifier(FieldValueFactorModifier.Log1p)
                                .Factor(1.0)
                                .Missing(1)
                            )
                        )
                        .ScoreMode(FunctionScoreMode.Sum)
                        // Multiply so the population log scales the text-relevance score.
                        // For two docs that match the query equally in the edge-ngram
                        // field, the more-populous one wins by a clear multiplicative
                        // margin (e.g. Tokyo's 14M -> ~16x vs Tokod's 4k -> ~8x).
                        .BoostMode(FunctionBoostMode.Multiply)
                    )
                )
                .Size(50);

            searchRequest.Sort(new List<SortOptions>
            {
                SortOptions.Score(new ScoreSort { Order = SortOrder.Desc }),
            });

            var response = await _client.SearchAsync<CityDoc>(searchRequest);

            if (!response.IsValidResponse)
            {
                Console.WriteLine($"ES Search Error: {response.DebugInformation}");
                return new List<CitySuggestion>();
            }

            Console.WriteLine($"Search found {response.Documents.Count} results for '{partialName}'");

            return response.Documents
            .Select(d => new CitySuggestion
            {
                Id = d.CityId,
                // Localization fallback chain: requested language -> country's primary language -> English -> any name
                Name = CountryLanguageMap.ResolveLocalized(d.CityNames, language, d.CountryCode, Constants.DefaultLanguage)
                    ?? d.AllNames.FirstOrDefault()
                    ?? "Unknown",
                CountryCode = d.CountryCode,
                Country = CountryLanguageMap.ResolveLocalized(d.Country, language, d.CountryCode, Constants.DefaultLanguage)
                    ?? "",
                AdminRegion = CountryLanguageMap.ResolveLocalized(d.AdminRegion, language, d.CountryCode, Constants.DefaultLanguage)
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