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
    private const int PageSize = 2000;
    private const int MaxPages = 50;
    private const int MaxRetries = 5;
    
    // Delay between pages within the same language (increased to 15s)
    private static readonly TimeSpan PageDelay = TimeSpan.FromSeconds(15);
    
    // Delay between different languages (increased to 2-5 minutes to avoid rate limiting)
    // Using Random to vary between 2-5 minutes so requests don't look automated
    private static readonly Random _random = new Random();
    
    // Base delay for exponential backoff on retries (increased to 5s)
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(5);
    
    // Global semaphore to ensure only 1 concurrent request to Wikidata at a time
    // This is critical to avoid being rate-limited by Wikidata's API
    private static readonly SemaphoreSlim _globalWikidataSemaphore = new SemaphoreSlim(1, 1);

    private static readonly HttpClient _httpClient = new HttpClient
    {
        // Longer timeout for large SPARQL queries - Wikidata can be slow for 20k cities
        Timeout = TimeSpan.FromSeconds(300)
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
        // Wait for global semaphore to ensure only 1 Wikidata request at a time
        // This prevents overwhelming Wikidata's API and getting rate limited
        await _globalWikidataSemaphore.WaitAsync();
        
        try
        {
            return await FetchCitiesInternalAsync(language);
        }
        finally
        {
            _globalWikidataSemaphore.Release();
        }
    }
    
    private async Task<List<SparQLCityInfo>> FetchCitiesInternalAsync(string language)
    {
        var allCities = new List<SparQLCityInfo>();
        var seenIds = new HashSet<string>();   // dedup guard – OFFSET can overlap on live data
        int offset = 0;
        int pageNumber = 0;
        int consecutiveFailures = 0;

        Console.WriteLine($"[{language}] Starting paginated fetch (page size: {PageSize})...");

        while (pageNumber < MaxPages)
        {
            pageNumber++;
            Console.WriteLine($"[{language}] Fetching page {pageNumber} (offset {offset})...");

            List<SparQLCityInfo>? page = null;
            bool pageSuccess = false;

            // Retry loop for this page
            for (int retry = 0; retry < MaxRetries && !pageSuccess; retry++)
            {
                if (retry > 0)
                {
                    // Exponential backoff with jitter: delay = base * 2^retry + random(0-1000ms)
                    var jitter = new Random().Next(0, 1000);
                    var delayMs = (int)(RetryBaseDelay.TotalMilliseconds * Math.Pow(2, retry - 1)) + jitter;
                    Console.WriteLine($"[{language}] Page {pageNumber} retry {retry}/{MaxRetries} - waiting {delayMs}ms before retry...");
                    await Task.Delay(delayMs);
                }

                try
                {
                    var query = BuildQuery(language, PageSize, offset);
                    var raw = await ExecuteSparqlAsync(query, language, pageNumber);
                    page = ParseSparqlResponse(raw, language);
                    pageSuccess = true;
                    consecutiveFailures = 0; // Reset on success
                }
                catch (Exception ex) when (retry < MaxRetries - 1)
                {
                    Console.WriteLine($"[{language}] Page {pageNumber} attempt {retry + 1} failed: {ex.Message}");
                    // Will retry
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{language}] Page {pageNumber} failed after {MaxRetries} attempts: {ex.Message}");
                    consecutiveFailures++;
                    
                    // If we've had too many consecutive page failures, stop this language
                    if (consecutiveFailures >= 3)
                    {
                        Console.WriteLine($"[{language}] Too many consecutive failures ({consecutiveFailures}). Stopping pagination.");
                        return allCities;
                    }
                    
                    // Move to next page offset even after failure (don't get stuck)
                    offset += PageSize;
                    break;
                }
            }

            if (!pageSuccess || page == null)
            {
                // Page failed even after retries - continue to next page
                offset += PageSize;
                continue;
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

            // If we got zero results, we've reached the end
            if (page.Count == 0)
            {
                Console.WriteLine($"[{language}] No results returned. End of data reached.");
                break;
            }

            offset += PageSize;

            // Be a good citizen – pause before the next page
            Console.WriteLine($"[{language}] Waiting {PageDelay.TotalSeconds}s before next page...");
            await Task.Delay(PageDelay);
        }

        if (pageNumber >= MaxPages)
        {
            Console.WriteLine($"[{language}] INFO: Reached MaxPages ({MaxPages}). " +
                              $"Pagination complete with {allCities.Count} total cities.");
        }
        else
        {
            Console.WriteLine($"[{language}] Pagination complete after {pageNumber} pages. " +
                              $"Total unique cities: {allCities.Count}");
        }
        
        // Wait between languages with random delay (2-5 minutes) to avoid rate limiting
        // Random delay makes requests look less automated to Wikidata's anti-bot systems
        var languageDelayMinutes = 2 + _random.NextDouble() * 3; // Random between 2-5 minutes
        var languageDelay = TimeSpan.FromMinutes(languageDelayMinutes);
        Console.WriteLine($"[{language}] Waiting {languageDelay.TotalMinutes:F1} minutes before next language...");
        await Task.Delay(languageDelay);
        
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
        {
            // If rate limited or gateway timeout, throw with specific message for retry logic
            if ((int)response.StatusCode == 429)
            {
                throw new Exception($"Rate limited (HTTP 429). Body: {raw[..Math.Min(200, raw.Length)]}");
            }
            else if ((int)response.StatusCode == 504)
            {
                throw new Exception($"Gateway timeout (HTTP 504). Body: {raw[..Math.Min(200, raw.Length)]}");
            }
            
            throw new Exception(
                $"Wikidata returned HTTP {(int)response.StatusCode}. " +
                $"Body: {raw[..Math.Min(500, raw.Length)]}");
        }

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
