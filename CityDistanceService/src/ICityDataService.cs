// CityDataService.cs - Complete orchestration service
using Elastic.Clients.Elasticsearch;
using Microsoft.VisualBasic;

public interface ICityDataService
{
    Task<int> SyncCitiesFromWikidataAsync();

    Task<CityInfo?> FindCityByIdAsync(string cityName);

    Task<CityInfo?> FindCityByIdAsync(string cityName, string language);

    Task<List<CitySuggestion>> GetCitySuggestionsAsync(string partialName, string language);

    Task<CityInfo> AddCityAsync(NewCityInfo newCity);

    Task<CityInfo> UpdateCityAsync(CityInfo updatedCity);

    Task DeleteCityAsync(string cityId);
}
