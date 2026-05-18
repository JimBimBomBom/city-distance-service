using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

public interface IElasticSearchService
{
    Task EnsureIndexExistsAsync();

    Task<List<CitySuggestion>> GetCitySuggestionsAsync(string partialName, string language);

    Task<CityDoc?> GetCityDocByIdAsync(string cityId);

    Task BulkUpsertCitiesAsync(List<SparQLCityInfo> cities);

    Task UpsertCityAsync(CityDoc city);
}
