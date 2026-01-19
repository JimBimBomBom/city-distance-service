// RequestHandler.cs - Refactored to use ICityDataService
using System.Linq;
using System.Collections.Generic;

public static class RequestHandler
{
    // Database health check - still uses IDatabaseService directly
    public static async Task<IResult> TestConnection(IDatabaseService dbManager)
    {
        return await dbManager.TestConnection();
    }

    // Calculate distance between two cities using city names
    public static async Task<IResult> ProcessCityDistanceAsync(
        CitiesDistanceRequest request, 
        ICityDataService cityService)
    {
        try
        {
            var distance = await DistanceCalculationService.CalculateDistanceAsync(
                request.City1Name, 
                request.City2Name, 
                cityService);

            if (distance == -1)
            {
                return Results.BadRequest(new { 
                    Error = "One or both cities could not be found within our database." 
                });
            }

            return Results.Ok(new { 
                City1 = request.City1Name,
                City2 = request.City2Name,
                DistanceKm = Math.Round(distance, 2),
                Message = "Distance calculated successfully"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ProcessCityDistanceAsync: {ex.Message}");
            return Results.BadRequest(new { Error = $"Error calculating distance: {ex.Message}" });
        }
    }

    // Get city info by ID
    public static async Task<IResult> ReturnCityInfoAsync(string cityId, IDatabaseService dbManager)
    {
        try
        {
            var cityInfo = await dbManager.GetCity(cityId);
            
            if (cityInfo == null)
            {
                return Results.NotFound(new { Error = "No city with given ID was found" });
            }

            return Results.Ok(new ApiResponse<CityInfo>(
                cityInfo, 
                "Here is the info for the requested City"
            ));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ReturnCityInfoAsync: {ex.Message}");
            return Results.BadRequest(new { Error = $"Error retrieving city: {ex.Message}" });
        }
    }

    // Search for cities by name (uses Elasticsearch for suggestions)
    public static async Task<IResult> ValidateAndReturnCitySuggestionsAsync(
        string cityName, 
        ICityDataService cityService)
    {
        try
        {
            var validationResult = await new StringValidator().ValidateAsync(cityName);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(new { Error = "Invalid city name parameter." });
            }

            var suggestions = await cityService.GetCitySuggestionsAsync(cityName);
            var suggestionList = suggestions.ToList();

            if (!suggestionList.Any())
            {
                return Results.Ok(new ApiResponse<List<string>>(
                    suggestionList, 
                    "No cities found matching your search"
                ));
            }

            return Results.Ok(new ApiResponse<List<string>>(
                suggestionList, 
                "Here are city suggestions matching your search"
            ));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ValidateAndReturnCitySuggestionsAsync: {ex.Message}");
            return Results.BadRequest(new { Error = $"Error searching cities: {ex.Message}" });
        }
    }

    // Find a specific city by name (returns full CityInfo)
    public static async Task<IResult> FindCityByNameAsync(
        string cityName, 
        ICityDataService cityService)
    {
        try
        {
            var validationResult = await new StringValidator().ValidateAsync(cityName);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(new { Error = "Invalid city name parameter." });
            }

            var cityInfo = await cityService.FindCityByNameAsync(cityName);

            if (cityInfo == null)
            {
                return Results.NotFound(new { Error = $"No city found matching: {cityName}" });
            }

            return Results.Ok(new ApiResponse<CityInfo>(
                cityInfo, 
                "City found successfully"
            ));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in FindCityByNameAsync: {ex.Message}");
            return Results.BadRequest(new { Error = $"Error finding city: {ex.Message}" });
        }
    }

    // Add a new city (updates both DB and ES)
    public static async Task<IResult> PostCityInfoAsync(
        NewCityInfo city, 
        ICityDataService cityService)
    {
        try
        {
            var addedCity = await cityService.AddCityAsync(city);

            if (addedCity == null)
            {
                return Results.BadRequest(new { 
                    Error = "Failed to add city to database, or city already exists." 
                });
            }

            return Results.Created(
                $"/city/{addedCity.CityId}",
                new ApiResponse<CityInfo>(
                    addedCity, 
                    "Your city has successfully been added to our database."
                )
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in PostCityInfoAsync: {ex.Message}");
            return Results.BadRequest(new { Error = $"Error adding city: {ex.Message}" });
        }
    }

    // Update an existing city (updates both DB and ES)
    public static async Task<IResult> UpdateCityInfoAsync(
        CityInfo city, 
        ICityDataService cityService)
    {
        try
        {
            var updatedCity = await cityService.UpdateCityAsync(city);

            return Results.Ok(new ApiResponse<CityInfo>(
                updatedCity, 
                "City has been successfully updated."
            ));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in UpdateCityInfoAsync: {ex.Message}");
            return Results.BadRequest(new { Error = $"Error updating city: {ex.Message}" });
        }
    }

    // Delete a city
    public static async Task<IResult> DeleteCityAsync(
        string cityId, 
        IDatabaseService dbManager,
        ICityDataService cityService)
    {
        try
        {
            var cityInfo = await dbManager.GetCity(cityId);
            
            if (cityInfo == null)
            {
                return Results.NotFound(new { Error = "No city with given ID was found" });
            }

            await cityService.DeleteCityAsync(cityId);

            return Results.Ok(new { 
                Message = "City has been successfully deleted.",
                DeletedCity = cityInfo
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DeleteCityAsync: {ex.Message}");
            return Results.BadRequest(new { Error = $"Error deleting city: {ex.Message}" });
        }
    }

    // Trigger Wikidata sync
    public static async Task<IResult> UpdateCityDatabaseAsync(ICityDataService cityService)
    {
        try
        {
            Console.WriteLine("Starting Wikidata sync via RequestHandler...");
            int updatedCount = await cityService.SyncCitiesFromWikidataAsync();
            
            return Results.Ok(new { 
                Message = "Successfully completed Wikidata sync",
                RecordsAffected = updatedCount,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in UpdateCityDatabaseAsync: {ex.Message}");
            return Results.BadRequest(new { 
                Error = $"Error updating city database: {ex.Message}" 
            });
        }
    }
}
