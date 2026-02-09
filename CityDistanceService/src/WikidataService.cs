using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

public interface IWikidataService
{
    Task<List<SparQLCityInfo>> FetchCitiesAsync(string language);
}

public class WikidataService : IWikidataService
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(10)
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
        string query = $@"
            SELECT ?city ?label ?lat ?lon ?modified ?countryLabel ?iso2 ?adminLabel ?pop WHERE {{
                ?city wdt:P625 ?coord .
                ?city wdt:P31/wdt:P279* wd:Q515 .
                ?city schema:dateModified ?modified .
                ?city rdfs:label ?label .
                
                # Country and ISO code
                OPTIONAL {{ 
                    ?city wdt:P17 ?country . 
                    ?country rdfs:label ?countryLabel .
                    ?country wdt:P297 ?iso2 .
                    FILTER(LANG(?countryLabel) = ""en"")
                }}
                
                # Administrative region (state/province)
                OPTIONAL {{ 
                    ?city wdt:P131 ?admin . 
                    ?admin rdfs:label ?adminLabel .
                    FILTER(LANG(?adminLabel) = ""{language}"")
                }}
                
                # Population
                OPTIONAL {{ ?city wdt:P1082 ?pop . }}
                
                FILTER(LANG(?label) = ""{language}"")
                
                BIND(geof:latitude(?coord) AS ?lat)
                BIND(geof:longitude(?coord) AS ?lon)
            }}
            LIMIT 10000";

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

                // Extract optional metadata fields
                string country = row.TryGetProperty("countryLabel", out var countryProp)
                    ? countryProp.GetProperty("value").GetString()
                    : null;

                string countryCode = row.TryGetProperty("iso2", out var iso2Prop)
                    ? iso2Prop.GetProperty("value").GetString()
                    : null;

                string adminRegion = row.TryGetProperty("adminLabel", out var adminProp)
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
                    AllNames = new List<string> { cityName },
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
                Console.WriteLine($"Skipping row due to error: {ex.Message}");
            }
        }

        Console.WriteLine($"SPARQL returned {cities.Count} cities.");
        return cities;
    }
}