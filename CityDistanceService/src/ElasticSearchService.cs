using System.Text;
using Elastic.Clients.Elasticsearch;
using Elasticsearch.Net;
using Nest;

public class ElasticSearchService
{
    private readonly IElasticClient _client;
    private readonly string clusterIndex;

    public ElasticSearchService()
    {
        var clusterUrl = Environment.GetEnvironmentVariable("ELASTICSEARCH_URL");
        var clusterApiKey = Environment.GetEnvironmentVariable("ELASTICSEARCH_API_KEY");
        clusterIndex = Environment.GetEnvironmentVariable("ELASTICSEARCH_INDEX");

        var settings = new ConnectionSettings(new Uri(clusterUrl))
            .ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(clusterApiKey))
            .DefaultIndex(clusterIndex) // Optional: Set a default index if you have one
            .EnableDebugMode()
            .OnRequestCompleted(callDetails =>
            {
                Console.WriteLine($"Request: {callDetails.HttpMethod} {callDetails.Uri}");
                if (callDetails.RequestBodyInBytes != null)
                {
                    Console.WriteLine($"Request Body: {Encoding.UTF8.GetString(callDetails.RequestBodyInBytes)}");
                }

                Console.WriteLine($"Response: {callDetails.HttpStatusCode}");
                if (callDetails.ResponseBodyInBytes != null)
                {
                    Console.WriteLine($"Response Body: {Encoding.UTF8.GetString(callDetails.ResponseBodyInBytes)}");
                }
            });

        _client = new ElasticClient(settings);
    }

    public async Task CreateIndexAsync()
    {
        var createIndexResponse = await _client.Indices.CreateAsync(clusterIndex, c => c
            .Map<CityName>(m => m
                .AutoMap()));

        if (createIndexResponse.IsValid)
        {
            Console.WriteLine("Index created successfully.");
        }
        else
        {
            Console.WriteLine($"Failed to create index: {createIndexResponse.OriginalException.Message}");
        }
    }

    public async Task UploadCitiesAsync(List<CityName> cities)
    {
        var bulkDescriptor = new BulkDescriptor();

        foreach (var city in cities)
        {
            if (_client.DocumentExists<CityName>(city.Name, d => d.Index(this.clusterIndex)).IsValid)
            {
                Console.WriteLine($"City {city.Name} already exists.");
                continue;
            }

            var insertResponse = await _client.IndexAsync(city, i => i
                .Index(clusterIndex)
                .Id(city.Name));

            if (insertResponse.IsValid)
            {
                Console.WriteLine("Cities uploaded successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to upload cities: {insertResponse.OriginalException.Message}");
            }
        }
    }

    public async Task CreateIndexWithAnalyzerAsync()
    {
        var createIndexResponse = await _client.Indices.CreateAsync(clusterIndex, c => c
            .Settings(s => s
                .Analysis(a => a
                    .Analyzers(an => an
                        .Custom("custom_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filters("lowercase", "asciifolding"))))) // Converts to lowercase and handles accented characters
            .Map<CityName>(m => m
                .Properties(p => p
                    .Text(t => t
                        .Name(n => n.Name)
                        .Analyzer("custom_analyzer")))));

        if (createIndexResponse.IsValid)
        {
            Console.WriteLine("Index with custom analyzer created successfully.");
        }
        else
        {
            Console.WriteLine($"Failed to create index with custom analyzer: {createIndexResponse.OriginalException.Message}");
        }
    }

    public async Task DeleteIndexAsync()
    {
        var deleteIndexResponse = await _client.Indices.DeleteAsync(clusterIndex);

        if (deleteIndexResponse.IsValid)
        {
            Console.WriteLine("Index deleted successfully.");
        }
        else
        {
            Console.WriteLine($"Failed to delete index: {deleteIndexResponse.OriginalException?.Message}");
        }
    }

    // public async Task<IReadOnlyCollection<IHit<CityName>>> FuzzySearchAsync(string searchTerm)
    public async Task<List<string>> FuzzySearchAsync(string searchTerm)
    {
        var searchResponse = await _client.SearchAsync<CityName>(s => s
            .Index(clusterIndex)
            .Query(q => q
                .Fuzzy(f => f
                    .Field(ff => ff.Name)
                    .Value(searchTerm)
                    .Fuzziness(Nest.Fuzziness.EditDistance(2))))); // Automatically determines the number of allowed edits

        if (searchResponse.IsValid)
        {
            Console.WriteLine($"Found {searchResponse.Total} documents.");
            foreach (var hit in searchResponse.Hits)
            {
                Console.WriteLine($"City: {hit.Source.Name}");
            }

            return searchResponse.Hits.Select(hit => hit.Source.Name).ToList();
        }
        else
        {
            Console.WriteLine($"Failed to perform fuzzy search: {searchResponse.OriginalException.Message}");
            return null;
        }
    }

    public async Task<string> GetLikeliestMatch(string searchTerm)
    {
        var searchResponse = await _client.SearchAsync<CityName>(s => s
            .Index(clusterIndex)
            .Query(q => q
                .Match(m => m
                    .Field(f => f.Name)
                    .Query(searchTerm)
                    .Fuzziness(Nest.Fuzziness.EditDistance(2))))); // Automatically determines the number of allowed edits

        if (searchResponse.IsValid)
        {
            Console.WriteLine($"Found {searchResponse.Total} documents.");
            if (searchResponse.Total > 0)
            {
                return searchResponse.Hits.First().Source.Name;
            }
            else
            {
                return null;
            }
        }
        else
        {
            Console.WriteLine($"Failed to perform fuzzy search: {searchResponse.OriginalException.Message}");
            return null;
        }
    }
}
