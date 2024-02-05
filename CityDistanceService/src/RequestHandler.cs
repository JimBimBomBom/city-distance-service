namespace RequestHandler
{
    public static class RequestHandlerClass
    {
        public static async Task<IResult> ValidateAndProcessCityDistanceAsync(HttpContext context, DatabaseManager dbManager)
        {
            var query = context.Request.Query;
            string city1 = query["city1"].ToString();
            string city2 = query["city2"].ToString();
            var validationResult = await new CitiesDistanceRequestValidator().ValidateAsync(new CitiesDistanceRequest { City1 = city1, City2 = city2 });
            if (!validationResult.IsValid)
                return Results.BadRequest(validationResult.Errors.Select(x => x.ErrorMessage).ToList());

            var distance = await DistanceCalculationService.CalculateDistanceAsync(city1, city2, dbManager);
            if (distance == -1)
                return Results.BadRequest("One or both cities could not be found within our database.");
            else
                return Results.Ok("Distance between: " + city1 + " to " + city2 + " is: " + distance + ".");
        }

        public static async Task<IResult> ValidateAndReturnCityInfoAsync(HttpContext context, DatabaseManager dbManager)
        {
            var query = context.Request.Query;
            if (int.TryParse(query["cityId"], out int cityId))
            {
                var validationResult = await new IdValidator().ValidateAsync(cityId);
                if (!validationResult.IsValid)
                    return Results.BadRequest("Invalid city ID.");

                var cityInfo = await dbManager.GetCityInfo(cityId);
                if (cityInfo == null)
                    return Results.NotFound("No city with given name was found");
                else
                    return Results.Ok(new ApiResponse<CityInfo>(cityInfo, "Here is the info for the requested City"));
            }
            else
            {
                var cityName = query["cityName"].ToString();
                var validationResult = await new StringValidator().ValidateAsync(cityName);
                if (!validationResult.IsValid)
                    return Results.BadRequest("Invalid city name.");

                var cityInfo = await dbManager.GetCityInfo(cityName);
                if (cityInfo == null)
                    return Results.NotFound("No city with given name was found");
                else
                    return Results.Ok(new ApiResponse<CityInfo>(cityInfo, "Here is the info for the requested City"));
            }
        }

        public static async Task<IResult> ValidateAndReturnCitiesCloseMatchAsync(HttpContext context, DatabaseManager dbManager)
        {
            var query = context.Request.Query;
            var cityNameContains = query["cityNameContains"];
            var validationResult = await new StringValidator().ValidateAsync(cityNameContains);
            if (!validationResult.IsValid)
                return Results.BadRequest("Invalid cityNameContains parameter.");

            var cities = await dbManager.GetCitiesByNameAsync(cityNameContains);
            if (cities == null)
                return Results.NotFound("No city with given name was found");
            else
                return Results.Ok(new ApiResponse<List<CityInfo>>(cities, "Here is the full list of requested cities"));
        }

        public static async Task<IResult> ValidateAndPostCityInfoAsync(HttpContext context, DatabaseManager dbManager)
        {
            var cityDataForPost = await context.Request.ReadFromJsonAsync<CityInfo>();
            var validationResult = await new NewCityInfoValidator().ValidateAsync(cityDataForPost);
            if (!validationResult.IsValid)
                return Results.BadRequest(validationResult.Errors.Select(x => x.ErrorMessage).ToList());

            var addedCity = await dbManager.AddCityAsync(cityDataForPost);
            return Results.Ok(new ApiResponse<CityInfo>(addedCity, "Your city has successfully been added to our database."));
        }

        public static async Task<IResult> ValidateAndUpdateCityInfoAsync(HttpContext context, DatabaseManager dbManager)
        {
            var cityDataForPut = await context.Request.ReadFromJsonAsync<CityInfo>();
            var validationResult = await new CityInfoValidator().ValidateAsync(cityDataForPut);
            if (!validationResult.IsValid)
                return Results.BadRequest(validationResult.Errors.Select(x => x.ErrorMessage).ToList());

            var updatedCity = await dbManager.ModifyItemAsync(cityDataForPut);
            return Results.Ok(new ApiResponse<CityInfo>(updatedCity, "Item has been successfully updated."));
        }

        public static async Task<IResult> ValidateAndDeleteCityAsync(HttpContext context, DatabaseManager dbManager)
        {
            var queryForDelete = context.Request.Query;
            if (int.TryParse(queryForDelete["cityId"], out int cityIdForDelete))
            {
                var validationResult = await new IdValidator().ValidateAsync(cityIdForDelete);
                if (!validationResult.IsValid)
                    return Results.BadRequest("Invalid city ID -> ID must be a positive integer.");
                else
                {
                    await dbManager.DeleteItem(cityIdForDelete);
                    return Results.Ok("Item has been successfully deleted.");
                }
            }
            else
                return Results.BadRequest("Invalid city ID -> ID must be a positive integer.");
        }
    }
}