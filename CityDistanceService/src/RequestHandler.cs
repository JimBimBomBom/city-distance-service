namespace RequestHandler
{
    public static class RequestHandlerClass
    {
        public static async Task<IResult> ValidateAndProcessCityDistanceAsync(CitiesDistanceRequest cities, IDatabaseManager dbManager)
        {
            var distance = await DistanceCalculationService.CalculateDistanceAsync(cities.City1, cities.City2, dbManager);
            if (distance == -1)
                return Results.BadRequest("One or both cities could not be found within our database.");
            else
                return Results.Ok("Distance between: " + cities.City1 + " to " + cities.City2 + " is: " + distance + ".");
        }

        // public static async Task<IResult> ValidateAndReturnCityInfoAsync(CityId cityId, IDatabaseManager dbManager)
        // {
        //     var cityInfo = await dbManager.GetCity(cityId.id);
        //     if (cityInfo == null)
        //         return Results.NotFound("No city with given name was found");
        //     else
        //         return Results.Ok(new ApiResponse<CityInfo>(cityInfo, "Here is the info for the requested City"));
        // }
        public static async Task<IResult> ValidateAndReturnCityInfoAsync(HttpContext context, IDatabaseManager dbManager)
        {
            var query = context.Request.Query;
            var cityId = Int32.Parse(query["cityId"]);
            var validationResult = await new IdValidator().ValidateAsync(cityId);
            if (!validationResult.IsValid)
                return Results.BadRequest("Invalid city ID.");

            var cityInfo = await dbManager.GetCity(cityId);
            if (cityInfo == null)
                return Results.NotFound("No city with given name was found");
            else
                return Results.Ok(new ApiResponse<CityInfo>(cityInfo, "Here is the info for the requested City"));
        }

        // public static async Task<IResult> ValidateAndReturnCitiesCloseMatchAsync(CityName cityName, IDatabaseManager dbManager)
        // {
        //     var cities = await dbManager.GetCities(cityName.name);
        //     if (cities == null)
        //         return Results.NotFound("No city with given name was found");
        //     else
        //         return Results.Ok(new ApiResponse<List<CityInfo>>(cities, "Here is the full list of requested cities"));
        // }
        public static async Task<IResult> ValidateAndReturnCitiesCloseMatchAsync(HttpContext context, IDatabaseManager dbManager)
        {
            var query = context.Request.Query;
            var cityNameContains = query["cityNameContains"];
            var validationResult = await new StringValidator().ValidateAsync(cityNameContains);
            if (!validationResult.IsValid)
                return Results.BadRequest("Invalid cityNameContains parameter.");

            var cities = await dbManager.GetCities(cityNameContains);
            if (cities == null)
                return Results.NotFound("No city with given name was found");
            else
                return Results.Ok(new ApiResponse<List<CityInfo>>(cities, "Here is the full list of requested cities"));
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

        // public static async Task<IResult> ValidateAndDeleteCityAsync(CityId cityId, IDatabaseManager dbManager)
        // {
        //     await dbManager.DeleteCity(cityId.id);
        //     return Results.Ok("Item has been successfully deleted.");
        // }
        public static async Task<IResult> ValidateAndDeleteCityAsync(HttpContext context, IDatabaseManager dbManager)
        {
            var queryForDelete = context.Request.Query;
            var cityIdForDelete = Int32.Parse(queryForDelete["cityId"]);
            var validationResult = await new IdValidator().ValidateAsync(cityIdForDelete);
            if (!validationResult.IsValid)
                return Results.BadRequest("Invalid city ID -> ID must be a positive integer.");
            else
            {
                await dbManager.DeleteCity(cityIdForDelete);
                return Results.Ok("Item has been successfully deleted.");
            }
        }
    }
}