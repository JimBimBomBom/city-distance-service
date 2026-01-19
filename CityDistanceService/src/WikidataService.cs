using System.Text.Json;

public interface IWikidataService
{
    Task<List<SparQLCityInfo>> FetchCitiesAsync(DateTime lastSync, string language);
}

public class WikidataService : IWikidataService
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    static WikidataService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "CityDistanceService/1.0 (https://yourdomain.example; your-email@example.com)"
        );
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/sparql-results+json");
    }

    public async Task<List<SparQLCityInfo>> FetchCitiesAsync(DateTime lastSync, string language)
    {
        string lastSyncIso = lastSync.ToString("yyyy-MM-ddTHH:mm:ssZ");

        string query = $@"
        SELECT ?city ?label ?lat ?lon ?modified WHERE {{
            ?city wdt:P625 ?coord .
            ?city wdt:P31/wdt:P279* wd:Q515 .
            ?city schema:dateModified ?modified .
            ?city rdfs:label ?label .
            
            FILTER(LANG(?label) = ""{language}"")
            FILTER(?modified > ""{lastSyncIso}""^^xsd:dateTime)
            
            BIND(geof:latitude(?coord) AS ?lat)
            BIND(geof:longitude(?coord) AS ?lon)
        }}";

        var raw = await ExecuteSparqlAsync(query);
        return ParseSparqlResponse(raw);
    }

    private static async Task<string> ExecuteSparqlAsync(string query)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["query"] = query
        });

        var response = await _httpClient.PostAsync("https://query.wikidata.org/sparql", content);
        var raw = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"Wikidata response status: {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Wikidata status {response.StatusCode}. Body:\n{raw}");

        return raw;
    }

    private static List<SparQLCityInfo> ParseSparqlResponse(string raw)
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

                if (!double.TryParse(latProp.GetProperty("value").GetString(), out double lat) ||
                    !double.TryParse(lonProp.GetProperty("value").GetString(), out double lon))
                {
                    continue;
                }

                cities.Add(new SparQLCityInfo
                {
                    WikidataId = id,
                    CityName = cityName,
                    AllNames = new List<string> { cityName },
                    Latitude = lat,
                    Longitude = lon,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Skipping row due to error: {ex.Message}");
            }
        }

        Console.WriteLine($"SPARQL returned {cities.Count} cities.");
        return cities;
    }
}