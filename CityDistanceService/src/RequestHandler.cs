using System.Configuration;
using Microsoft.VisualBasic;

namespace RequestHandler
{
    public static class RequestHandler
    {
        public static async Task<IResult> TestConnection(IDatabaseManager dbManager)
        {
            return Results.Ok(await dbManager.TestConnection());
        }

        public static async Task<IResult> ValidateAndProcessCityDistanceAsync(CitiesDistanceRequest cities, IDatabaseManager dbManager)
        {
            var distance = await DistanceCalculationService.CalculateDistanceAsync(cities.City1, cities.City2, dbManager);
            if (distance == -1)
            {
                return Results.BadRequest("One or both cities could not be found within our database.");
            }
            else
            {
                return Results.Ok("Distance between: " + cities.City1 + " to " + cities.City2 + " is: " + distance + ".");
            }
        }

        public static async Task<IResult> ValidateAndReturnCityInfoAsync(int cityId, IDatabaseManager dbManager)
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

        public static async Task<IResult> ValidateAndReturnCitiesCloseMatchAsync(string cityName, IDatabaseManager dbManager)
        {
            var validationResult = await new StringValidator().ValidateAsync(cityName);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest("Invalid cityNameContains parameter.");
            }

            var cities = await dbManager.GetCities(cityName);
            if (cities == null)
            {
                return Results.NotFound("No city with given name was found");
            }
            else
            {
                return Results.Ok(new ApiResponse<List<CityInfo>>(cities, "Here is the full list of requested cities"));
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

        public static async Task<IResult> ValidateAndDeleteCityAsync(int cityId, IDatabaseManager dbManager)
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
}