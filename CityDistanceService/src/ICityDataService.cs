// ICityDataService.cs - Complete orchestration service
public interface ICityDataService
{
    Task<CityInfo?> FindCityByIdAsync(string cityName);

    Task<CityInfo?> FindCityByIdAsync(string cityName, string language);

    Task<List<CitySuggestion>> GetCitySuggestionsAsync(string partialName, string language);

    Task<CityInfo> AddCityAsync(NewCityInfo newCity);

    Task<CityInfo> UpdateCityAsync(CityInfo updatedCity);

    Task DeleteCityAsync(string cityId);
}
