using System.Reflection;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace CityDistanceService.Tests;

/// <summary>
/// Integration tests for the /suggestions search behaviour.
///
/// These tests stand up a real <see cref="ElasticSearchService"/> against a live
/// Elasticsearch cluster (whose URL is provided via the <c>TEST_ES_URL</c>
/// environment variable). They load a curated dataset of 100 cities in five
/// languages from <c>test_data/curated/*.csv</c> into a uniquely-named index,
/// run a battery of queries against it, and assert that well-known cities are
/// returned even under typos.
///
/// If <c>TEST_ES_URL</c> is not set, every test in the class is treated as
/// "inconclusive" (logged and skipped without failing the suite) so the
/// existing unit tests continue to pass in environments without ES.
///
/// Environment variables:
///   - <c>TEST_ES_URL</c>      (required, e.g. http://localhost:9200)
///   - <c>TEST_ES_PASSWORD</c> (optional, defaults to "testPassword123" to
///                             match the local docker-compose setup)
///   - <c>TEST_ES_USERNAME</c> (optional, defaults to "elastic")
/// </summary>
public class SuggestionsSearchIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string? _esUrl;
    private readonly string _esUser;
    private readonly string _esPassword;
    private readonly string _indexName;
    private ElasticsearchClient? _client;
    private ElasticSearchService? _service;
    private bool _skipped;

    public SuggestionsSearchIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _esUrl = Environment.GetEnvironmentVariable("TEST_ES_URL");
        _esUser = Environment.GetEnvironmentVariable("TEST_ES_USERNAME") ?? "elastic";
        _esPassword = Environment.GetEnvironmentVariable("TEST_ES_PASSWORD") ?? "testPassword123";
        _indexName = $"cities_test_{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_esUrl))
        {
            _skipped = true;
            return;
        }

        var settings = new ElasticsearchClientSettings(new Uri(_esUrl))
            .Authentication(new BasicAuthentication(_esUser, _esPassword))
            .ServerCertificateValidationCallback((_, _, _, _) => true)
            .RequestTimeout(TimeSpan.FromMinutes(2));

        _client = new ElasticsearchClient(settings);

        // Verify connectivity. If unreachable, mark as skipped rather than failing the suite.
        try
        {
            var ping = await _client.PingAsync();
            if (!ping.IsValidResponse)
            {
                _output.WriteLine($"ES ping failed: {ping.DebugInformation}. Skipping integration tests.");
                _skipped = true;
                return;
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"ES unreachable at {_esUrl}: {ex.Message}. Skipping integration tests.");
            _skipped = true;
            return;
        }

        _service = new ElasticSearchService(_client, _indexName);
        await _service.EnsureIndexExistsAsync();

        var dataDir = LocateCuratedDataDir();
        _output.WriteLine($"Loading curated CSVs from: {dataDir}");

        var importer = new FileDataImportService(dataDir, NullLogger<FileDataImportService>.Instance);
        var cities = await importer.LoadAllLanguageVariantsAsync();
        Assert.NotEmpty(cities);
        _output.WriteLine($"Loaded {cities.Count} language records across {importer.LoadedLanguages.Count} languages.");

        await _service.BulkUpsertCitiesAsync(cities);

        // BulkUpsert refreshes on bulk completion, but force an explicit refresh
        // to be safe before issuing search queries.
        await _client.Indices.RefreshAsync(_indexName);

        var count = await _service.GetDocumentCountAsync();
        _output.WriteLine($"Index '{_indexName}' contains {count} docs.");
        Assert.True(count >= 100, $"Expected at least 100 docs, got {count}");
    }

    public async Task DisposeAsync()
    {
        if (_client != null)
        {
            try
            {
                await _client.Indices.DeleteAsync(_indexName);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Failed to delete test index '{_indexName}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Walks up from the test assembly's directory until it finds the
    /// <c>test_data/curated</c> folder. This makes the test robust to the
    /// current working directory varying between <c>dotnet test</c>, the IDE,
    /// and CI runners.
    /// </summary>
    private static string LocateCuratedDataDir()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "test_data", "curated");
            if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "*_cities.csv").Length > 0)
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate test_data/curated directory containing *_cities.csv");
    }

    private void SkipIfNoEs()
    {
        if (_skipped)
        {
            _output.WriteLine("TEST_ES_URL not set or ES unreachable — test skipped.");
        }
    }

    private async Task AssertReturnsCityAsync(string query, string language, string expectedCityId, int maxRank = 10)
    {
        var results = await _service!.GetCitySuggestionsAsync(query, language);
        var ids = results.Select(r => r.Id).ToList();
        _output.WriteLine($"q='{query}' lang={language} -> [{string.Join(", ", ids)}]");

        var rank = ids.IndexOf(expectedCityId);
        Assert.True(
            rank >= 0 && rank < maxRank,
            $"Expected '{expectedCityId}' within top {maxRank} for q='{query}' lang={language}, got: [{string.Join(", ", ids)}]");
    }

    // ------------------------------------------------------------------------
    // Test cases. Each test exercises a different distortion pattern against
    // the curated dataset. They are Theory-driven so a failure clearly points
    // at the specific (query, language, expected) tuple that broke.
    // ------------------------------------------------------------------------

    [Theory]
    // Exact match (should rank #1)
    [InlineData("Tokyo",   "en", "QTEST_TOKYO")]
    [InlineData("London",  "en", "QTEST_LONDON")]
    [InlineData("Paris",   "en", "QTEST_PARIS")]
    [InlineData("Moscow",  "en", "QTEST_MOSCOW")]
    [InlineData("Cairo",   "en", "QTEST_CAIRO")]
    [InlineData("Berlin",  "en", "QTEST_BERLIN")]
    [InlineData("Madrid",  "en", "QTEST_MADRID")]
    [InlineData("Beijing", "en", "QTEST_BEIJING")]
    public async Task ExactMatch_Returns_City(string query, string lang, string expectedId)
    {
        if (_skipped) { SkipIfNoEs(); return; }
        await AssertReturnsCityAsync(query, lang, expectedId);
    }

    [Theory]
    // Proper prefixes — must always match
    [InlineData("Tok",    "en", "QTEST_TOKYO")]
    [InlineData("Toky",   "en", "QTEST_TOKYO")]
    [InlineData("Lond",   "en", "QTEST_LONDON")]
    [InlineData("Par",    "en", "QTEST_PARIS")]
    [InlineData("Mos",    "en", "QTEST_MOSCOW")]
    [InlineData("Cai",    "en", "QTEST_CAIRO")]
    [InlineData("Berl",   "en", "QTEST_BERLIN")]
    [InlineData("Beij",   "en", "QTEST_BEIJING")]
    public async Task Prefix_Returns_City(string query, string lang, string expectedId)
    {
        if (_skipped) { SkipIfNoEs(); return; }
        await AssertReturnsCityAsync(query, lang, expectedId);
    }

    [Theory]
    // Up-to-2-edit typos that previously broke (the regression that prompted this test).
    // "Toki"   -> Tokyo: substitute i->y and insert o (2 edits)
    // "Tkyo"   -> Tokyo: insert o (1 edit)
    // "Tokyon" -> Tokyo: delete trailing n (1 edit)
    // "Tokio"  -> Tokyo: substitute i->y, then insert/swap (handled by fuzzy)
    // "Mosko"  -> Moscow: substitute / transpose
    // "Londn"  -> London: insert o (1 edit)
    // "Pris"   -> Paris: insert a (1 edit)
    // "Bejing" -> Beijing: delete i (1 edit)
    [InlineData("Toki",   "en", "QTEST_TOKYO")]
    [InlineData("Tkyo",   "en", "QTEST_TOKYO")]
    [InlineData("Tokyon", "en", "QTEST_TOKYO")]
    [InlineData("Tokion", "en", "QTEST_TOKYO")]
    [InlineData("Tokio",  "en", "QTEST_TOKYO")]
    [InlineData("Mosko",  "en", "QTEST_MOSCOW")]
    [InlineData("Londn",  "en", "QTEST_LONDON")]
    [InlineData("Pris",   "en", "QTEST_PARIS")]
    [InlineData("Bejing", "en", "QTEST_BEIJING")]
    public async Task FuzzyTypo_Returns_City(string query, string lang, string expectedId)
    {
        if (_skipped) { SkipIfNoEs(); return; }
        await AssertReturnsCityAsync(query, lang, expectedId);
    }

    [Theory]
    // Non-Latin script queries against the matching localized name.
    [InlineData("東",       "ja", "QTEST_TOKYO")]      // prefix of 東京
    [InlineData("東京",     "ja", "QTEST_TOKYO")]      // exact
    [InlineData("Моск",     "ru", "QTEST_MOSCOW")]    // prefix of Москва
    [InlineData("Москва",   "ru", "QTEST_MOSCOW")]    // exact
    [InlineData("القاه",    "ar", "QTEST_CAIRO")]     // prefix of القاهرة
    [InlineData("Лондон",   "ru", "QTEST_LONDON")]    // exact
    [InlineData("ロンドン", "ja", "QTEST_LONDON")]    // exact
    public async Task NonLatinScript_Returns_City(string query, string lang, string expectedId)
    {
        if (_skipped) { SkipIfNoEs(); return; }
        await AssertReturnsCityAsync(query, lang, expectedId);
    }

    [Theory]
    // Querying in a language different from the document's primary script,
    // when an English alias exists in AllNames, should still resolve via the
    // common multilingual AllNames field.
    [InlineData("Tokyo", "ja", "QTEST_TOKYO")]
    [InlineData("Tokyo", "ru", "QTEST_TOKYO")]
    [InlineData("Tokyo", "ar", "QTEST_TOKYO")]
    [InlineData("Paris", "ja", "QTEST_PARIS")]
    public async Task CrossLanguage_Returns_City(string query, string lang, string expectedId)
    {
        if (_skipped) { SkipIfNoEs(); return; }
        await AssertReturnsCityAsync(query, lang, expectedId);
    }

    [Fact]
    public async Task ShortQuery_To_ReturnsTokyoAndOtherToCities()
    {
        // "To" is too short for any meaningful fuzzy expansion, but the prefix
        // clause should still surface Tokyo, Toronto, Tokat, Toledo, etc.
        // We don't assert positions because BM25 may favour shorter docs;
        // we assert presence within the top 10.
        if (_skipped) { SkipIfNoEs(); return; }

        var results = await _service!.GetCitySuggestionsAsync("To", "en");
        var ids = results.Select(r => r.Id).ToHashSet();
        _output.WriteLine($"q='To' lang=en -> [{string.Join(", ", ids)}]");

        Assert.Contains("QTEST_TOKYO", ids);

        // At least 3 of the To* cities should be visible (sanity check that
        // the prefix path isn't degenerating to a single result).
        var toCities = new[] { "QTEST_TOKYO", "QTEST_TORONTO", "QTEST_TOKAT", "QTEST_TOKMAK", "QTEST_TOLEDO", "QTEST_TOMSK", "QTEST_TOBA", "QTEST_TOBOLSK", "QTEST_TOPEKA", "QTEST_TONGEREN", "QTEST_TONGCHENG", "QTEST_TONGLING" };
        var hits = toCities.Count(id => ids.Contains(id));
        Assert.True(hits >= 3, $"Expected >=3 To* cities in top-10 for q='To', got {hits}. Ids: [{string.Join(", ", ids)}]");
    }

    [Fact]
    public async Task FarOffQuery_DoesNotReturnUnrelatedCity()
    {
        // "Tokyon" is 1 edit from Tokyo but >5 edits from any synthetic
        // noise city. Tokyo must rank above the noise.
        if (_skipped) { SkipIfNoEs(); return; }

        var results = await _service!.GetCitySuggestionsAsync("Tokyon", "en");
        var ids = results.Select(r => r.Id).ToList();
        _output.WriteLine($"q='Tokyon' -> [{string.Join(", ", ids)}]");

        Assert.Contains("QTEST_TOKYO", ids);

        // Synthetic cities QTEST_SYN* should NOT outrank Tokyo for this query.
        var tokyoRank = ids.IndexOf("QTEST_TOKYO");
        var anySyntheticAbove = ids
            .Take(tokyoRank >= 0 ? tokyoRank : ids.Count)
            .Any(id => id.StartsWith("QTEST_SYN", StringComparison.Ordinal));
        Assert.False(anySyntheticAbove, $"Synthetic noise outranked Tokyo for q='Tokyon'. Ids: [{string.Join(", ", ids)}]");
    }
}
