using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;


var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<DatabaseManager>(); // TEST

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

// app.MapPost("/distance", DistanceCalculationService.CalculateDistanceAsync);

// app.MapGet("/get_city", (string CityName, DatabaseManager dbManager) =>
// {
// 	var city = dbManager.GetCityCoordinates(CityName);
// 	if (city == null)
// 		return Results.BadRequest("No city with specified name exists");//TODO add city from someone's API
// 	else
// 		return Results.Ok(city);
// });

// app.MapGet("/get_cities_by_name", async (string CityName, DatabaseManager dbManager) =>
// {
// 	var cities = await dbManager.GetCitiesByNameAsync(CityName);
// 	if (cities == null)
// 		return Results.BadRequest("No cities with specified name exist");//TODO add city from someone's API
// 	else
// 		return Results.Ok(cities);
// });

// app.MapPost("/add_city", async (CityInfo City, DatabaseManager dbManager) =>
// {
// 	dbManager.AddItem(City);
// 	return Results.Ok(City.CityName + " can now be found within our catalog.");
// });

// app.MapPost("/modify_city", async (CityInfo City, DatabaseManager dbManager) =>
// {
// 	dbManager.ModifyItem(City);
// 	return Results.Ok(City.CityName + " has probably been updated.");
// });

// app.MapGet("/delete_city", async (int CityId, DatabaseManager dbManager) =>
// {
// 	dbManager.DeleteItem(CityId);
// 	return Results.Ok("City with specified Id deleted.");
// });

app.MapMethods("/city", new[] { "GET", "POST", "PUT", "DELETE" }, async (HttpContext context, DatabaseManager dbManager) =>
{
	switch (context.Request.Method)
	{
		case "GET":
			var query = context.Request.Query;

			if (query.ContainsKey("city1") && query.ContainsKey("city2")) // GET distance between cities
			{
				var city1 = query["city1"];
				var city2 = query["city2"];
				//TODO validate input
				var distance = await DistanceCalculationService.CalculateDistanceAsync(city1, city2, dbManager);
				//TODO validate distance
				return Results.Ok("Distance between: " + city1 + " to " + city2 + " is: " + distance + ".");
			}
			else if (query.ContainsKey("cityName") || query.ContainsKey("cityId")) // GET city coordinates
			{
				//TODO validate input

				var cityInfo = new CityInfo { CityName = null };
				if (int.TryParse(query["cityId"], out int cityId))
					cityInfo = await dbManager.GetCityInfo(cityId);
				else
				{
					var cityName = query["cityName"];
					cityInfo = await dbManager.GetCityInfo(cityName);
				}

				//TODO validate data
				return Results.Ok(cityInfo);
			}
			else if (query.ContainsKey("cityNameContains")) // GET cities that contain substring
			{
				//TODO validate input
				var cityName = query["cityNameContains"];
				var cities = await dbManager.GetCitiesByNameAsync(cityName);
				//TODO validate cities
				return Results.Ok(cities);
			}
			else
				return Results.BadRequest("Invalid request parameters.\nValid request parameters are: 1) city1 && city2 => used for calculating distance between two cities.\n2) CityName || CityId => for latitude and longitude info about a city\n");
		case "POST":
			var cityDataForPost = await context.Request.ReadFromJsonAsync<CityInfo>();
			//TODO validate input
			var addedCity = await dbManager.AddCityAsync(cityDataForPost);
			//TODO validate data
			return Results.Ok(new ApiResponse<CityInfo>(addedCity, "Your city has successfully been added to our database."));
		case "PUT":
			var cityDataForPut = await context.Request.ReadFromJsonAsync<CityInfo>();
			//TODO validate input
			var updatedCity = await dbManager.ModifyItemAsync(cityDataForPut);
			//TODO validate data
			return Results.Ok(new ApiResponse<CityInfo>(updatedCity, "Item has been successfully updated."));
		case "DELETE":
			var queryForDelete = context.Request.Query;
			if (queryForDelete.ContainsKey("cityId"))
			{
				if (int.TryParse(queryForDelete["cityId"], out int cityIdForDelete))
				{
					await dbManager.DeleteItem(cityIdForDelete);
					return Results.Ok("Item has been successfully deleted.");
				}
				else
				{
					return Results.BadRequest("Invalid city ID.");
				}
			}
			else
			{
				return Results.BadRequest("City ID is required for delete operation.");
			}
		default:
			return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
	}
});

app.Run();

/*
!!!!! GIT HUB - with branches

USER STORY 1: open API swagger documentation [M]

USER STORY 2: harmonize status code responses [M]
        - Post  - return 201 (CREATED)
                - alternatively returns 40X (CONFLICT)
        - GET   - return 200  OK
                - return 404 NOT FOUND
        - DELETE- return 200  OK
                - return 404 NOT FOUND
        - PUT   - return 200  OK
                - return 404 NOT FOUND
        - Also consider adding additional HTTP status codes where applicable

USER STORY 3: handling duplicates [SPIKE] [L]
        -how to work with duplicates in db
            and how to update data

USER STORY 4: unify endpoints into a singular endpoint -> /city [L]
        - POST - Create, returns item Id
        - GET - retrieve:
                    - by id
                    - by name search query
        - PUT - update
        - DELETE - delete

USER STORY 5: search endpoint [M]
        - move old get_city_by_name funcionality HERE?

USER STORY 6: postman integration tests [S]

USER STORY 7: support other coordinate notations [XXL]
        Examples:
            - Decimal degrees	N 50.91721° E 5.91775°
            - Degrees and decimal minutes	N 50° 55.032' E 5° 55.065'
            - Degrees, minutes and seconds	N 50° 55' 1.956'' E 5° 55' 3.9''
        Create unit tests (use either N-unit or X-unit, whichever is newer)

USER STORY 8: validate input [M]
            - for create:
                - make sure all mandatory fields are present (name , long lat,)
                - ensure lat/lon are valid and within range 

USER STORY 9: make all city endpoints async [S]
        - make sure async calls are awaited when appropriate

USER STORY 10: Case sensitivity [S]
        -investigate case-insensitivity when hitting the GET /city endpoint
        - ensure case sensitivity during creation
        - ensure case insensitivity during retrieval

USER STORY 11: Improve search endpoint with fuzzy searching [L]
        - use Levenstein distance or similar algorithm
        - alternatively consider indexing solutions (solr, elastic search, meili search)

USER STORY 12: performance/stress testing [L]
        - 

*/
