using System.Linq;
using System.Collections.Generic;

public static class RequestHandler
{
    public static async Task<IResult> TestConnection(IDatabaseManager dbManager)
    {
        return Results.Ok(await dbManager.TestConnection());
    }

    public static async Task<IResult> ValidateAndProcessCityDistanceAsync(CitiesDistanceRequest cities, IDatabaseManager dbManager, ElasticSearchService elSearch)
    {
        cities.City1 = await elSearch.GetLikeliestMatch(cities.City1);
        if (cities.City1 == null)
        {
            return Results.BadRequest($"Invalid city1 parameter. {cities.City1} Not found in database.");
        }

        cities.City2 = await elSearch.GetLikeliestMatch(cities.City2);
        if (cities.City2 == null)
        {
            return Results.BadRequest($"Invalid city2 parameter. {cities.City2} Not found in database.");
        }

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

    public static async Task<IResult> ValidateAndReturnCityInfoAsync(Guid cityId, IDatabaseManager dbManager)
    {
        var cityInfo = await dbManager.GetCity(cityId);
        if (cityInfo == null)
        {
            return Results.NotFound("No city with given name was found");
        }
        else
        {
            return Results.Ok(new ApiResponse<CityInfo>(cityInfo, "Here is the info for the requested City"));
        }
    }

    public static async Task<IResult> ValidateAndReturnCitiesCloseMatchAsync(string cityName, IDatabaseManager dbManager, ElasticSearchService elSearch)
    {
        var validationResult = await new StringValidator().ValidateAsync(cityName);
        if (!validationResult.IsValid)
        {
            return Results.BadRequest("Invalid cityNameContains parameter.");
        }

        var cities = await elSearch.FuzzySearchAsync(cityName);
        if (cities == null)
        {
            return Results.NotFound("No city with given name was found");
        }
        else
        {
            var citiesInfo = await dbManager.GetCities(cities);
            return Results.Ok(new ApiResponse<List<CityInfo>>(citiesInfo, "Here is the full list of requested cities"));
        }
    }

    public static async Task<IResult> ValidateAndPostCityInfoAsync(CityInfo city, IDatabaseManager dbManager)
    {
        var addedCity = await dbManager.AddCity(city);
        return Results.Ok(new ApiResponse<CityInfo>(addedCity, "Your city has successfully been added to our database."));
    }

    public static async Task<IResult> ValidateAndUpdateCityInfoAsync(CityInfo city, IDatabaseManager dbManager)
    {
        var updatedCity = await dbManager.UpdateCity(city);
        return Results.Ok(new ApiResponse<CityInfo>(updatedCity, "Item has been successfully updated."));
    }

    public static async Task<IResult> ValidateAndDeleteCityAsync(Guid cityId, IDatabaseManager dbManager)
    {
        var cityInfo = await dbManager.GetCity(cityId);
        if (cityInfo == null)
        {
            return Results.NotFound("No city with given name was found");
        }
        else
        {
            await dbManager.DeleteCity(cityId);
            return Results.Ok("Item has been successfully deleted.");
        }
    }
}
