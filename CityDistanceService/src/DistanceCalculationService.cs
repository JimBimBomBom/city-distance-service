using System.Text.Json;
public static class DistanceCalculationService
{
    public static async Task<double> CalculateDistanceAsync(string city1, string city2, DatabaseManager dbManager)
    {
        var coordinates_city1 = await dbManager.GetCityCoordinates(city1);
        if (coordinates_city1 == null)
        {
            var newCity = await GeocodeCity(city1);
            await dbManager.AddCityAsync(newCity);
            coordinates_city1 = await dbManager.GetCityCoordinates(city1);
        }

        var coordinates_city2 = await dbManager.GetCityCoordinates(city2);
        if (coordinates_city2 == null)
        {
            var newCity = await GeocodeCity(city2);
            await dbManager.AddCityAsync(newCity);
            coordinates_city2 = await dbManager.GetCityCoordinates(city2);
        }

        if (coordinates_city1 == null || coordinates_city2 == null)
        {
            return -1; // negative numbers represent a fail
        }

        var distance = CalculateGreatCircleDistance(coordinates_city1, coordinates_city2);

        return distance;
        // return Results.Ok("Distance from: " + city1 + " to " + city2 + " is: " + distance);
    }

    private static readonly HttpClient httpClient = new HttpClient();

    public static async Task<CityInfo> GeocodeCity(string cityName)
    {
        string apiKey = Environment.GetEnvironmentVariable("GEOCODE_API_KEY");
        string url = $"https://geocode.maps.co/search?q={Uri.EscapeDataString(cityName)}&api_key={apiKey}";

        try
        {
            var response = await httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();

            // Assuming the response is in a JSON format that you need to deserialize
            var result = await response.Content.ReadFromJsonAsync<GeocodeApiResponse[]>();

            if (result != null && result.Length > 0)
            {
                return new CityInfo
                {
                    CityName = cityName,
                    // Country = result[0].Country,
                    Latitude = result[0].Lat,
                    Longitude = result[0].Lon
                };
            }
        }
        catch (HttpRequestException ex)
        {
            // Handle any exceptions (network error, invalid response, etc.)
            Console.WriteLine($"Error fetching geocoding data: {ex.Message}");
        }

        return null; // Or handle the error as per your logic
    }

    private static double CalculateGreatCircleDistance(Coordinates coord1, Coordinates coord2)
    {
        const double EarthRadius = 6371.0; // Radius of the earth in kilometers

        double lat_distance = ToRadians(coord2.Latitude - coord1.Latitude);
        double lon_distance = ToRadians(coord2.Longitude - coord1.Longitude);
        double a = Math.Sin(lat_distance / 2) * Math.Sin(lat_distance / 2) +
                Math.Cos(ToRadians(coord1.Latitude)) * Math.Cos(ToRadians(coord2.Latitude)) *
                Math.Sin(lon_distance / 2) * Math.Sin(lon_distance / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        double distance = EarthRadius * c; // Distance in kilometers

        return distance;
    }

    private static double ToRadians(double angle)
    {
        return Math.PI * angle / 180.0;
    }
}