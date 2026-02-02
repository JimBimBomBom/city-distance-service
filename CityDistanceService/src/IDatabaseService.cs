public interface IDatabaseService
{
    Task<IResult> TestConnection();

    Task<CityInfo?> GetCity(string cityId);

    Task<List<CityInfo>> GetCities(List<string> cityIds);

    Task<CityInfo> AddCity(NewCityInfo newCity);

    Task<CityInfo> UpdateCity(CityInfo updatedCity);

    Task DeleteCity(string cityId);

    Task<Coordinates?> GetCityCoordinates(string cityId);

    Task<int> BulkUpsertCitiesAsync(List<SparQLCityInfo> cities);

    Task<DateTime> GetLastSyncAsync();

    Task UpdateLastSyncAsync(DateTime newTimestamp);
}