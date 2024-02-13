public interface IDatabaseManager
{
    Task<CityInfo> AddCity(CityInfo newCity);
    Task<CityInfo> GetCity(int cityId);
    Task<CityInfo> GetCity(string cityName);
    Task<Coordinates> GetCityCoordinates(string cityName);
    Task<List<CityInfo>> GetCities(string cityNameContains);
    Task<CityInfo> UpdateCity(CityInfo updatedCity);
    Task DeleteCity(int cityId);
}