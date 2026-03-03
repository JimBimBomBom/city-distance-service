using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using System.Security.AccessControl;

public interface IElasticSearchService
{
    Task EnsureIndexExistsAsync();

    Task<string?> GetBestCityIdAsync(string cityName);

    Task<List<CitySuggestion>> GetCitySuggestionsAsync(string partialName, string language);

    Task<CityDoc?> GetCityDocByIdAsync(string cityId);

    Task BulkUpsertCitiesAsync(List<SparQLCityInfo> cities);

    Task UpsertCityAsync(CityDoc city);
}
