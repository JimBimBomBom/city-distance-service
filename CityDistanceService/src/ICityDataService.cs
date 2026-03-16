// CityDataService.cs - Complete orchestration service
using Elastic.Clients.Elasticsearch;
using Microsoft.VisualBasic;

public interface ICityDataService
{
    Task<int> SyncCitiesFromWikidataAsync(
        TimeSpan? maxDuration = null,
        int? maxConsecutivePageFailures = null,
        Action<int>? onPageProcessed = null,
        CancellationToken cancellationToken = default);

    Task<CityInfo?> FindCityByIdAsync(string cityName);

    Task<CityInfo?> FindCityByIdAsync(string cityName, string language);

    Task<List<CitySuggestion>> GetCitySuggestionsAsync(string partialName, string language);

    Task<CityInfo> AddCityAsync(NewCityInfo newCity);

    Task<CityInfo> UpdateCityAsync(CityInfo updatedCity);

    Task DeleteCityAsync(string cityId);
}
