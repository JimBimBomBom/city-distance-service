public interface IDatabaseManager
{
    Task<IResult> TestConnection();

    Task<List<string>> GetCityNames();

    Task<CityInfo> AddCity(NewCityInfo newCity);

    Task<CityInfo?> GetCity(string cityId);

    Task<Coordinates?> GetCityCoordinates(string cityName);

    Task<List<CityInfo>> GetCities(string cityNameContains);

    Task<List<CityInfo>> GetCities(List<string> cityNames);

    Task<CityInfo> UpdateCity(CityInfo updatedCity);

    Task DeleteCity(string cityId);

    Task<DateTime> GetLastSyncAsync();

    Task UpdateLastSyncAsync(DateTime newTimestamp);

    Task<List<SparQLCityInfo>> FetchCitiesAsync(DateTime lastSync);

    Task<int> BulkUpsertCitiesAsync(List<SparQLCityInfo> cities);

    Task<int> UpdateCityDatabaseAsync();
}
