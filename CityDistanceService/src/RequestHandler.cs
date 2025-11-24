using System.Linq;
using System.Collections.Generic;

// You may need to define StringValidator if it was removed in previous steps.
// For simplicity here, we assume StringValidator is available or we use a basic check.
// public class StringValidator : AbstractValidator<string> { ... } 

public static class RequestHandler
{
    // Uses the new TestConnection (Elasticsearch Ping)
    public static async Task<IResult> TestConnection(IDatabaseManager dbManager)
    {
        return await dbManager.TestConnection(); // Returns IResult directly
    }

    // Uses the new GetCityNames (Elasticsearch Aggregation or Completion)
    public static async Task<IResult> GetCityNames(IDatabaseManager dbManager)
    {
        return Results.Ok(await dbManager.GetCityNames());
    }

    public static async Task<IResult> ProcessCityDistanceAsync(CitiesDistanceRequest cities, IDatabaseManager dbManager)
    {
        // This method depends on GetCityCoordinates, which uses the 'raw' keyword field for exact name lookup.
        var distance = await DistanceCalculationService.CalculateDistanceAsync(cities.City1, cities.City2, dbManager);
        if (distance == -1)
        {
            return Results.BadRequest("One or both cities could not be found within our database.");
        }
        else
        {
            return Results.Ok(distance);
        }
    }

    // Uses the new GetCity (Elasticsearch Get by ID)
    public static async Task<IResult> ReturnCityInfoAsync(string cityId, IDatabaseManager dbManager)
    {
        var cityInfo = await dbManager.GetCity(cityId);
        if (cityInfo == null)
        {
            return Results.NotFound($"No city found with ID: {cityId}");
        }
        else
        {
            // The CityInfo now includes MultilingualNames property
            return Results.Ok(new ApiResponse<CityInfo>(cityInfo, "Here is the info for the requested City"));
        }
    }

    // --- ENHANCED: Multilingual Search Endpoint ---
    // We update this method to accept an optional language code parameter to leverage Elasticsearch's features.
    public static async Task<IResult> ValidateAndReturnCitiesCloseMatchAsync(
        string cityName, 
        IDatabaseManager dbManager, 
        string languageCode = "en", // Default to English for search
        int size = 10)
    {
        // Assuming StringValidator is simple/placeholder, we enforce a check.
        if (string.IsNullOrWhiteSpace(cityName))
        {
             return Results.BadRequest("City name search term cannot be empty.");
        }

        // Call the new multilingual search method
        var citiesInfo = await dbManager.GetCities(cityName, languageCode, size);
        
        return Results.Ok(new ApiResponse<List<CityInfo>>(citiesInfo, "Here is the full list of requested cities"));
    }

    // CRUD methods rely on the updated IDatabaseManager methods, mostly unchanged in signature
    public static async Task<IResult> PostCityInfoAsync(NewCityInfo city, IDatabaseManager dbManager)
    {
        var addedCity = await dbManager.AddCity(city);
        if (addedCity == null)
        {
            return Results.BadRequest("Failed to add city to database, or city already exists.");
        }
        else
        {
            return Results.Ok(new ApiResponse<CityInfo>(addedCity, "Your city has successfully been added to our database."));
        }
    }

    public static async Task<IResult> UpdateCityInfoAsync(CityInfo city, IDatabaseManager dbManager)
    {
        var updatedCity = await dbManager.UpdateCity(city);
        return Results.Ok(new ApiResponse<CityInfo>(updatedCity, "Item has been successfully updated."));
    }

    public static async Task<IResult> DeleteCityAsync(string cityId, IDatabaseManager dbManager)
    {
        var cityInfo = await dbManager.GetCity(cityId);
        if (cityInfo == null)
        {
            return Results.NotFound($"No city found with ID: {cityId}");
        }
        else
        {
            await dbManager.DeleteCity(cityId);
            return Results.Ok("Item has been successfully deleted.");
        }
    }

    // --- ENHANCED: Synchronization Endpoint ---
    public static async Task<IResult> UpdateCityDatabaseAsync(IDatabaseManager dbManager)
    {
        try
        {
            // Call the correct, unified method name
            int updatedCount = await dbManager.SynchronizeElasticSearchAsync(); 
            return Results.Ok(new { Message = $"Successfully updated or inserted {updatedCount} cities." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API-SYNC] Error: {ex.Message}");
            return Results.StatusCode(500, new { Message = $"Error updating city database: {ex.Message}" });
        }
    }
}