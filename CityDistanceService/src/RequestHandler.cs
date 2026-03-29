// RequestHandler.cs
using System.Security.Cryptography.X509Certificates;
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
        ILocalizationService localization,
        string lang)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return Results.BadRequest(new
            {
                Error = localization.Get(MsgKey.InvalidQuery, lang)
            });

        var validationResult = await new StringValidator().ValidateAsync(query);
        if (!validationResult.IsValid)
            return Results.BadRequest(new
            {
                Error = localization.Get(MsgKey.InvalidQuery, lang)
            });

        try
        {
            var suggestions = await esService.GetCitySuggestionsAsync(query, lang);
            var message = suggestions.Count > 0
                ? localization.Get(MsgKey.SuggestionsFound, lang)
                : localization.Get(MsgKey.NoSuggestions, lang);

            return Results.Ok(new
            {
                Data    = suggestions,
                Message = message
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetCitySuggestionsAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = localization.Get(MsgKey.InternalError, lang),
                statusCode = 500
            });
        }
    }

    // ── Distance ──────────────────────────────────────────────────────────────

    public static async Task<IResult> ProcessCityDistanceAsync(
        CitiesDistanceRequest request,
        ICityDataService cityService,
        ILocalizationService localization,
        string lang)
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
                    Error = localization.Get(MsgKey.CityNotFound, lang)
                });

            var formatted = localization.FormatDistance(distanceKm, lang);

            return Results.Ok(new
            {
                Distance = formatted.Distance,
                Unit     = formatted.Unit,
                Message  = localization.Get(MsgKey.DistanceCalculated, lang)
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ProcessCityDistanceAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = localization.Get(MsgKey.InternalError, lang),
                statusCode = 500
            });
        }
    }

    // ── City CRUD ─────────────────────────────────────────────────────────────

    public static async Task<IResult> ReturnCityInfoAsync(
        string cityId,
        ICityDataService cityService,
        ILocalizationService localization,
        string lang)
    {
        try
        {
            var city = await cityService.FindCityByIdAsync(cityId, lang);
            if (city == null)
                return Results.NotFound(new
                {
                    Error = localization.Get(MsgKey.CityNotFound, lang)
                });

            return Results.Ok(new
            {
                Data    = city,
                Message = localization.Get(MsgKey.CityFound, lang)
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ReturnCityInfoAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = localization.Get(MsgKey.InternalError, lang),
                statusCode = 500
            });
        }
    }

    public static async Task<IResult> PostCityInfoAsync(
        NewCityInfo city,
        ICityDataService cityService,
        ILocalizationService localization,
        string lang)
    {
        try
        {
            var added = await cityService.AddCityAsync(city);
            if (added == null)
                return Results.Conflict(new
                {
                    Error = localization.Get(MsgKey.CityAlreadyExists, lang)
                });

            return Results.Created($"/city/{added.CityId}", new
            {
                Data    = added,
                Message = localization.Get(MsgKey.CityAdded, lang)
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in PostCityInfoAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = localization.Get(MsgKey.InternalError, lang),
                statusCode = 500
            });
        }
    }

    public static async Task<IResult> UpdateCityInfoAsync(
        CityInfo city,
        ICityDataService cityService,
        ILocalizationService localization,
        string lang)
    {
        try
        {
            var updated = await cityService.UpdateCityAsync(city);
            return Results.Ok(new
            {
                Data    = updated,
                Message = localization.Get(MsgKey.CityUpdated, lang)
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in UpdateCityInfoAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = localization.Get(MsgKey.InternalError, lang),
                statusCode = 500
            });
        }
    }

    public static async Task<IResult> DeleteCityAsync(
        string cityId,
        IDatabaseService dbManager,
        ICityDataService cityService,
        ILocalizationService localization,
        string lang)
    {
        try
        {
            var city = await dbManager.GetCity(cityId);
            if (city == null)
                return Results.NotFound(new
                {
                    Error = localization.Get(MsgKey.CityNotFound, lang)
                });

            await cityService.DeleteCityAsync(cityId);

            return Results.Ok(new
            {
                Message = localization.Get(MsgKey.CityDeleted, lang)
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DeleteCityAsync: {ex.Message}");
            return Results.Json(new
            {
                Error = localization.Get(MsgKey.InternalError, lang),
                statusCode = 500
            });
        }
    }
}