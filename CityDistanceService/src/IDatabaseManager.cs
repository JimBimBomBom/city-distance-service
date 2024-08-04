public interface IDatabaseManager
{
    Task<string> TestConnection();

    Task<CityInfo> AddCity(CityInfo newCity);

    Task<CityInfo?> GetCity(Guid cityId);

    Task<CityInfo?> GetCity(string cityName);

    Task<Coordinates?> GetCityCoordinates(string cityName);

    Task<List<CityInfo>> GetCities(string cityNameContains);

    Task<CityInfo> UpdateCity(CityInfo updatedCity);

    Task DeleteCity(Guid cityId);
}
