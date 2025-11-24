// IDatabaseManager.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IDatabaseManager
{
    Task<IResult> TestConnection();

    // Changed: City names are retrieved from search index (uses fuzzy/multilingual search)
    Task<List<string>> GetCityNames(string prefix = "", int size = 1000); 

    // CRUD operations: Use Wikidata ID as the primary key
    Task<CityInfo> AddCity(NewCityInfo newCity);
    Task<CityInfo?> GetCity(string cityId);
    Task<CityInfo> UpdateCity(CityInfo updatedCity);
    Task DeleteCity(string cityId);

    // Search methods: Leverage Elasticsearch's search capabilities
    Task<Coordinates?> GetCityCoordinates(string cityName);
    Task<List<CityInfo>> GetCities(string searchPhrase, string languageCode = "en", int size = 10);
    Task<List<CityInfo>> GetCities(List<string> cityIds); // Use IDs for exact lookups

    // Synchronization Methods (Updated to use ElasticCityDocument)
    Task<DateTime> GetLastSyncAsync();
    Task UpdateLastSyncAsync(DateTime newTimestamp);
    Task<List<ElasticCityDocument>> FetchCitiesAsync(DateTime lastSync);
    Task<int> BulkIndexCitiesAsync(List<ElasticCityDocument> cities); // Renamed: BulkUpsert -> BulkIndex
    Task<int> SynchronizeElasticSearchAsync(); // Renamed: UpdateCityDatabase -> SynchronizeElasticSearch
}
