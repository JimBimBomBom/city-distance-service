using System.Text.Json;
public static class DistanceCalculationService
{
    static string API_KEY = "65aa8e928d962502323182strd1e6f9";

    public static async Task<double> CalculateDistanceAsync(string city1, string city2, DatabaseManager dbManager)
    {
        var coordinates_city1 = await dbManager.GetCityCoordinates(city1);
        if (coordinates_city1 == null)
        {
            var newCity = await GeocodeCity(city1, API_KEY);
            await dbManager.AddCityAsync(newCity);
            coordinates_city1 = await dbManager.GetCityCoordinates(city1);
        }

        var coordinates_city2 = await dbManager.GetCityCoordinates(city2);
        if (coordinates_city2 == null)
        {
            var newCity = await GeocodeCity(city2, API_KEY);
            await dbManager.AddCityAsync(newCity);
            coordinates_city2 = await dbManager.GetCityCoordinates(city2);
        }

        if (coordinates_city1 == null || coordinates_city2 == null)
        {
            return -1;
        }

        var distance = CalculateGreatCircleDistance(coordinates_city1, coordinates_city2);

        return distance;
    }

    private static readonly HttpClient httpClient = new HttpClient();

    public static async Task<CityInfo> GeocodeCity(string cityName, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return null;
        }

        string url = $"https://geocode.maps.co/search?q={Uri.EscapeDataString(cityName)}&api_key={apiKey}";

        try
        {
            var response = await httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GeocodeApiResponse[]>();

            if (result != null && result.Length > 0)
            {
                return new CityInfo
                {
                    CityName = cityName,
                    Latitude = result[0].Lat,
                    Longitude = result[0].Lon
                };
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching geocoding data: {ex.Message}");
        }

        return null;
    }

    private static double CalculateGreatCircleDistance(Coordinates coord1, Coordinates coord2)
    {
        const double EarthRadius = 6371.0;

        double lat_distance = ToRadians(coord2.Latitude - coord1.Latitude);
        double lon_distance = ToRadians(coord2.Longitude - coord1.Longitude);
        double a = Math.Sin(lat_distance / 2) * Math.Sin(lat_distance / 2) +
                Math.Cos(ToRadians(coord1.Latitude)) * Math.Cos(ToRadians(coord2.Latitude)) *
                Math.Sin(lon_distance / 2) * Math.Sin(lon_distance / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        double distance = EarthRadius * c;

        return distance;
    }

    private static double ToRadians(double angle)
    {
        return Math.PI * angle / 180.0;
    }
}