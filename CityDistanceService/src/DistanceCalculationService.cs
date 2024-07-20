using System.Text.Json;
public static class DistanceCalculationService
{
    public static async Task<double> CalculateDistanceAsync(string city1, string city2, IDatabaseManager dbManager)
    {
        var coordinatesCity1 = await dbManager.GetCityCoordinates(city1);
        var coordinatesCity2 = await dbManager.GetCityCoordinates(city2);
        if (coordinatesCity1 == null || coordinatesCity2 == null)
        {
            Console.WriteLine("One or both cities could not be found within our database.");
            return -1;
        }

        var distance = CalculateGreatCircleDistance(coordinatesCity1, coordinatesCity2);

        return distance;
    }

    private static readonly HttpClient httpClient = new HttpClient();

    public static double CalculateGreatCircleDistance(Coordinates coord1, Coordinates coord2)
    {
        const double EarthRadius = 6371.0;
        double lat_distance = ToRadians(coord2.Latitude - coord1.Latitude);
        double lon_distance = ToRadians(coord2.Longitude - coord1.Longitude);
        double a = (Math.Sin(lat_distance / 2) * Math.Sin(lat_distance / 2)) +
                (Math.Cos(ToRadians(coord1.Latitude)) * Math.Cos(ToRadians(coord2.Latitude)) *
                Math.Sin(lon_distance / 2) * Math.Sin(lon_distance / 2));
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        double distance = EarthRadius * c;

        return distance;
    }

    public static double ToRadians(double angle)
    {
        return Math.PI * angle / 180.0;
    }
}