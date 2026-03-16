using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

public interface IWikidataService
{
    Task<List<SparQLCityInfo>> FetchCitiesAsync(string language);
}

public class WikidataService : IWikidataService
{
    private const int PageSize = 20000;

    private const int MaxPages = 40;

    private static readonly TimeSpan PageDelay = TimeSpan.FromSeconds(10);

    private static readonly HttpClient _httpClient = new HttpClient
    {
        // Per-request timeout; shorter than before so a hung request fails fast.
        Timeout = TimeSpan.FromMinutes(3)
    };

    static WikidataService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "CityDistanceService/1.0 (https://yourdomain.example; your-email@example.com)"
        );
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/sparql-results+json");
    }

    public async Task<List<SparQLCityInfo>> FetchCitiesAsync(string language)
    {
        var allCities = new List<SparQLCityInfo>();
        var seenIds = new HashSet<string>();   // dedup guard – OFFSET can overlap on live data
        int offset = 0;
        int pageNumber = 0;

        Console.WriteLine($"[{language}] Starting paginated fetch (page size: {PageSize})...");

        while (pageNumber < MaxPages)
        {
            pageNumber++;
            Console.WriteLine($"[{language}] Fetching page {pageNumber} (offset {offset})...");

            List<SparQLCityInfo> page;

            try
            {
                var query = BuildQuery(language, PageSize, offset);
                var raw = await ExecuteSparqlAsync(query, language, pageNumber);
                page = ParseSparqlResponse(raw, language);
            }
            catch (Exception ex)
            {
                // A single page failure should not abort the whole language run.
                // Log and stop paginating – we keep whatever we collected so far.
                Console.WriteLine($"[{language}] Page {pageNumber} failed: {ex.Message}. " +
                                  $"Stopping pagination, keeping {allCities.Count} cities collected so far.");
                break;
            }

            // Deduplicate before adding (live Wikidata edits can cause boundary rows to shift)
            int newCount = 0;
            foreach (var city in page)
            {
                if (seenIds.Add(city.WikidataId))
                {
                    allCities.Add(city);
                    newCount++;
                }
            }

            int duplicates = page.Count - newCount;
            Console.WriteLine($"[{language}] Page {pageNumber}: {page.Count} rows returned, " +
                              $"{newCount} new, {duplicates} duplicates skipped. " +
                              $"Running total: {allCities.Count}");

            // Fewer rows than a full page → we have reached the last page
            if (page.Count < PageSize)
            {
                Console.WriteLine($"[{language}] Last page reached (got {page.Count} < {PageSize}). Done.");
                break;
            }

            offset += PageSize;

            // Be a good citizen – pause before the next request
            Console.WriteLine($"[{language}] Waiting {PageDelay.TotalSeconds}s before next page...");
            await Task.Delay(PageDelay);
        }

        if (pageNumber >= MaxPages)
        {
            Console.WriteLine($"[{language}] WARNING: hit MaxPages ({MaxPages}) safety cap. " +
                              $"There may be more cities in Wikidata that were not fetched.");
        }

        Console.WriteLine($"[{language}] Fetch complete. Total unique cities: {allCities.Count}");
        return allCities;
    }

    // -------------------------------------------------------------------------
    // Query builder
    // -------------------------------------------------------------------------

    private static string BuildQuery(string language, int limit, int offset)
    {
        // ORDER BY ?city is essential for stable pagination.
        // Without it, Wikidata may return different (or overlapping) rows on each page
        // because the result set has no guaranteed ordering.
        return $@"
    SELECT ?city ?label ?lat ?lon ?countryLabel ?iso2 ?adminLabel ?pop WHERE {{
        ?city wdt:P625 ?coord .
        ?city wdt:P31/wdt:P279* wd:Q515 .

        OPTIONAL {{
            ?city wdt:P17 ?country .
            OPTIONAL {{ ?country wdt:P297 ?iso2 . }}
        }}

        OPTIONAL {{
            ?city wdt:P131 ?admin .
        }}

        OPTIONAL {{ ?city wdt:P1082 ?pop . }}

        BIND(geof:latitude(?coord)  AS ?lat)
        BIND(geof:longitude(?coord) AS ?lon)

        SERVICE wikibase:label {{
            bd:serviceParam wikibase:language ""{language},en"" .
            ?city rdfs:label ?label .
            ?country rdfs:label ?countryLabel .
            ?admin rdfs:label ?adminLabel .
        }}
    }}
    ORDER BY ?city
    LIMIT {limit}
    OFFSET {offset}";
    }

    // -------------------------------------------------------------------------
    // HTTP layer
    // -------------------------------------------------------------------------

    private static async Task<string> ExecuteSparqlAsync(string query, string language, int pageNumber)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["query"] = query
        });

        var response = await _httpClient.PostAsync("https://query.wikidata.org/sparql", content);
        var raw = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"[{language}] Page {pageNumber} – HTTP {(int)response.StatusCode} {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
            throw new Exception(
                $"Wikidata returned HTTP {(int)response.StatusCode}. " +
                $"Body: {raw[..Math.Min(500, raw.Length)]}");

        // Wikidata can embed raw control characters (e.g. 0x0D carriage returns) inside
        // JSON string values, which System.Text.Json strictly rejects. Strip them out.
        raw = Regex.Replace(raw, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", " "); // keep \t (0x09) and \n (0x0A)
        raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");              // normalise CR / CRLF

        return raw;
    }

    // -------------------------------------------------------------------------
    // Response parser
    // -------------------------------------------------------------------------

    private static List<SparQLCityInfo> ParseSparqlResponse(string raw, string language)
    {
        using var json = JsonDocument.Parse(raw);
        var bindings = json.RootElement
                                 .GetProperty("results")
                                 .GetProperty("bindings");

        var cities = new List<SparQLCityInfo>();

        foreach (var row in bindings.EnumerateArray())
        {
            try
            {
                if (!row.TryGetProperty("city", out var cityProp) ||
                    !row.TryGetProperty("label", out var labelProp) ||
                    !row.TryGetProperty("lat", out var latProp) ||
                    !row.TryGetProperty("lon", out var lonProp))
                {
                    continue;
                }

                string id = cityProp.GetProperty("value").GetString()!.Split('/').Last();
                string cityName = labelProp.GetProperty("value").GetString()!;

                // Use InvariantCulture so "50.08" parses correctly regardless of server locale
                if (!double.TryParse(latProp.GetProperty("value").GetString(),
                                     NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                    !double.TryParse(lonProp.GetProperty("value").GetString(),
                                     NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                {
                    continue;
                }

                string? country = row.TryGetProperty("countryLabel", out var countryProp)
                    ? countryProp.GetProperty("value").GetString()
                    : null;

                string? countryCode = row.TryGetProperty("iso2", out var iso2Prop)
                    ? iso2Prop.GetProperty("value").GetString()
                    : null;

                string? adminRegion = row.TryGetProperty("adminLabel", out var adminProp)
                    ? adminProp.GetProperty("value").GetString()
                    : null;

                int? population = null;
                if (row.TryGetProperty("pop", out var popProp) &&
                    int.TryParse(popProp.GetProperty("value").GetString(), out int pop))
                {
                    population = pop;
                }

                cities.Add(new SparQLCityInfo
                {
                    WikidataId = id,
                    CityName = cityName,
                    Language = language,
                    Latitude = lat,
                    Longitude = lon,
                    Country = country,
                    CountryCode = countryCode,
                    AdminRegion = adminRegion,
                    Population = population
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Skipping row due to parse error: {ex.Message}");
            }
        }

        return cities;
    }
}
