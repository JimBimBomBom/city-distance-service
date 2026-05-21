// RequestHandler.cs
using FluentValidation;

public static class RequestHandler
{
    // ── Health ────────────────────────────────────────────────────────────────

    public static async Task<IResult> TestConnection(IDatabaseService dbManager)
    {
        return await dbManager.TestConnection();
    }

    // ── Suggestions ───────────────────────────────────────────────────────────

    public static async Task<IResult> GetCitySuggestionsAsync(
        string query,
        IElasticSearchService esService,
        string lang)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return Results.BadRequest(new
            {
                Error = "Query must be at least 2 characters."
            });

        var validationResult = await new StringValidator().ValidateAsync(query);
        if (!validationResult.IsValid)
            return Results.BadRequest(new
            {
                Error = "Invalid query."
            });

        try
        {
            var suggestions = await esService.GetCitySuggestionsAsync(query, lang);
            return Results.Ok(suggestions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetCitySuggestionsAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = "Internal server error.",
                statusCode = 500
            });
        }
    }

    // ── Distance ──────────────────────────────────────────────────────────────

    public static async Task<IResult> ProcessCityDistanceAsync(
        CitiesDistanceRequest request,
        ICityDataService cityService)
    {
        try
        {
            var distanceKm = await DistanceCalculationService.CalculateDistanceAsync(
                request.City1Id,
                request.City2Id,
                cityService);

            if (distanceKm == -1)
                return Results.NotFound(new
                {
                    Error = "City not found."
                });

            return Results.Ok(new { DistanceKm = distanceKm });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ProcessCityDistanceAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = "Internal server error.",
                statusCode = 500
            });
        }
    }

    // ── City CRUD ─────────────────────────────────────────────────────────────

    public static async Task<IResult> ReturnCityInfoAsync(
        string cityId,
        ICityDataService cityService,
        string lang)
    {
        try
        {
            var city = await cityService.FindCityByIdAsync(cityId, lang);
            if (city == null)
                return Results.NotFound(new
                {
                    Error = "City not found."
                });

            return Results.Ok(city);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ReturnCityInfoAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = "Internal server error.",
                statusCode = 500
            });
        }
    }

    public static async Task<IResult> PostCityInfoAsync(
        NewCityInfo city,
        ICityDataService cityService)
    {
        try
        {
            var added = await cityService.AddCityAsync(city);
            if (added == null)
                return Results.Conflict(new
                {
                    Error = "City already exists."
                });

            return Results.Created($"/city/{added.CityId}", added);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in PostCityInfoAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = "Internal server error.",
                statusCode = 500
            });
        }
    }

    public static async Task<IResult> UpdateCityInfoAsync(
        CityInfo city,
        ICityDataService cityService)
    {
        try
        {
            var updated = await cityService.UpdateCityAsync(city);
            return Results.Ok(updated);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in UpdateCityInfoAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = "Internal server error.",
                statusCode = 500
            });
        }
    }

    public static async Task<IResult> DeleteCityAsync(
        string cityId,
        IDatabaseService dbManager,
        ICityDataService cityService)
    {
        try
        {
            var city = await dbManager.GetCity(cityId);
            if (city == null)
                return Results.NotFound(new
                {
                    Error = "City not found."
                });

            await cityService.DeleteCityAsync(cityId);

            return Results.Ok(new
            {
                Message = "City deleted."
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DeleteCityAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = "Internal server error.",
                statusCode = 500
            });
        }
    }
}
