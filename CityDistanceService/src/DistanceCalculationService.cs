// DistanceCalculationService.cs - Refactored to use ICityDataService
using System;
using System.Threading.Tasks;

public static class DistanceCalculationService
{
    /// <summary>
    /// Calculate distance between two cities using city IDs.
    /// Returns -1 if either city is not found.
    /// </summary>
    public static async Task<double> CalculateDistanceAsync(
        string city1Id,
        string city2Id,
        ICityDataService cityService)
    {
        try
        {
            var city1 = await cityService.FindCityByIdAsync(city1Id);
            var city2 = await cityService.FindCityByIdAsync(city2Id);

            // Validate that both cities were found
            if (city1 == null)
            {
                Console.WriteLine($"City not found: {city1Id}");
                return -1;
            }

            if (city2 == null)
            {
                Console.WriteLine($"City not found: {city2Id}");
                return -1;
            }

            Console.WriteLine($"Calculating distance between '{city1.CityName}' and '{city2.CityName}'");

            // Calculate distance using Haversine formula
            var distance = CalculateHaversineDistance(
                city1.Latitude, city1.Longitude,
                city2.Latitude, city2.Longitude
            );

            Console.WriteLine($"Distance between {city1.CityName} and {city2.CityName}: {distance:F2} km");
            return distance;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calculating distance: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Calculate distance using Haversine formula.
    /// Returns distance in kilometers.
    /// </summary>
    private static double CalculateHaversineDistance(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        const double R = 6371; // Earth's radius in kilometers

        // Convert degrees to radians
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var radLat1 = ToRadians(lat1);
        var radLat2 = ToRadians(lat2);

        // Haversine formula
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) *
                Math.Cos(radLat1) * Math.Cos(radLat2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    public static double CalculateHaversineDistance(
        Coordinates coord1,
        Coordinates coord2)
    {
        const double R = 6371; // Earth's radius in kilometers

        // Convert degrees to radians
        var dLat = ToRadians(coord2.Latitude - coord1.Latitude);
        var dLon = ToRadians(coord2.Longitude - coord1.Longitude);
        var radLat1 = ToRadians(coord1.Latitude);
        var radLat2 = ToRadians(coord2.Latitude);

        // Haversine formula
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) *
                Math.Cos(radLat1) * Math.Cos(radLat2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    /// <summary>
    /// Convert degrees to radians
    /// </summary>
    public static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}