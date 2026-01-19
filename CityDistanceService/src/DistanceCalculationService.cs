// DistanceCalculationService.cs - Refactored to use ICityDataService
using System;
using System.Threading.Tasks;

public static class DistanceCalculationService
{
    /// <summary>
    /// Calculate distance between two cities using city names.
    /// Returns -1 if either city is not found.
    /// </summary>
    public static async Task<double> CalculateDistanceAsync(
        string city1Name, 
        string city2Name, 
        ICityDataService cityService)
    {
        try
        {
            Console.WriteLine($"Calculating distance between '{city1Name}' and '{city2Name}'");

            // Step 1: Find both cities using Elasticsearch search -> Database lookup
            var city1 = await cityService.FindCityByNameAsync(city1Name);
            var city2 = await cityService.FindCityByNameAsync(city2Name);

            // Step 2: Validate that both cities were found
            if (city1 == null)
            {
                Console.WriteLine($"City not found: {city1Name}");
                return -1;
            }

            if (city2 == null)
            {
                Console.WriteLine($"City not found: {city2Name}");
                return -1;
            }

            // Step 3: Calculate distance using Haversine formula
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
    /// Calculate distance between two cities using city IDs directly.
    /// Useful when you already have the city IDs.
    /// </summary>
    public static async Task<double> CalculateDistanceByIdAsync(
        string cityId1, 
        string cityId2, 
        IDatabaseService dbManager)
    {
        try
        {
            Console.WriteLine($"Calculating distance between city IDs '{cityId1}' and '{cityId2}'");

            // Get coordinates directly from database
            var coords1 = await dbManager.GetCityCoordinates(cityId1);
            var coords2 = await dbManager.GetCityCoordinates(cityId2);

            if (coords1 == null)
            {
                Console.WriteLine($"City ID not found: {cityId1}");
                return -1;
            }

            if (coords2 == null)
            {
                Console.WriteLine($"City ID not found: {cityId2}");
                return -1;
            }

            var distance = CalculateHaversineDistance(
                coords1.Latitude, coords1.Longitude,
                coords2.Latitude, coords2.Longitude
            );

            Console.WriteLine($"Distance: {distance:F2} km");
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

    /// <summary>
    /// Calculate bearing (direction) from city1 to city2 in degrees.
    /// 0° = North, 90° = East, 180° = South, 270° = West
    /// </summary>
    public static double CalculateBearing(
        double lat1, 
        double lon1, 
        double lat2, 
        double lon2)
    {
        var dLon = ToRadians(lon2 - lon1);
        var radLat1 = ToRadians(lat1);
        var radLat2 = ToRadians(lat2);

        var y = Math.Sin(dLon) * Math.Cos(radLat2);
        var x = Math.Cos(radLat1) * Math.Sin(radLat2) -
                Math.Sin(radLat1) * Math.Cos(radLat2) * Math.Cos(dLon);

        var bearing = Math.Atan2(y, x);
        var degrees = (ToDegrees(bearing) + 360) % 360;

        return degrees;
    }

    /// <summary>
    /// Convert radians to degrees
    /// </summary>
    private static double ToDegrees(double radians)
    {
        return radians * 180.0 / Math.PI;
    }

    /// <summary>
    /// Get compass direction from bearing
    /// </summary>
    public static string GetCompassDirection(double bearing)
    {
        var directions = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N" };
        var index = (int)Math.Round(bearing / 45.0) % 8;
        return directions[index];
    }
}