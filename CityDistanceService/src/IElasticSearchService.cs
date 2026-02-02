using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

// public class CityDoc
// {
//     public string CityId { get; set; }

//     public List<string> CityNames { get; set; } = new();

//     public GeoLocation Location { get; set; }
// }

public interface IElasticSearchService
{
    Task EnsureIndexExistsAsync();

    Task<string?> GetBestCityIdAsync(string cityName);

    Task<IEnumerable<string>> GetCitySuggestionsAsync(string partialName);

    Task BulkUpsertCitiesAsync(List<SparQLCityInfo> cities);

    Task UpsertCityAsync(CityDoc city);
}
