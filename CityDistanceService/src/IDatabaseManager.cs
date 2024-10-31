public interface IDatabaseManager
{
    Task<IResult> TestConnection();

    Task<List<string>> GetCityNames();

    Task<CityInfo> AddCity(NewCityInfo newCity);

    Task<CityInfo?> GetCity(Guid cityId);

    Task<CityInfo?> GetCity(string cityName);

    Task<Coordinates?> GetCityCoordinates(string cityName);

    Task<List<CityInfo>> GetCities(string cityNameContains);

    Task<List<CityInfo>> GetCities(List<string> cityNames);

    Task<CityInfo> UpdateCity(CityInfo updatedCity);

    Task DeleteCity(Guid cityId);

    Task<int> BulkInsertCitiesAsync(List<NewCityInfo> cities);
}
