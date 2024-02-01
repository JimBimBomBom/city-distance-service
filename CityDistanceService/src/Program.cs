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

app.MapMethods("/city", new[] { "GET", "POST", "PUT", "DELETE" }, async (HttpContext context, DatabaseManager dbManager) =>
{
	switch (context.Request.Method)
	{
		case "GET":
			var query = context.Request.Query;

			if (query.ContainsKey("city1") && query.ContainsKey("city2")) // GET distance between cities
			{
				string city1 = query["city1"].ToString();
				if (string.IsNullOrWhiteSpace(city1))
					return Results.BadRequest("city1 parameter was not specified correctly.");
				string city2 = query["city2"].ToString();
				if (string.IsNullOrWhiteSpace(city2))
					return Results.BadRequest("city2 parameter was not specified correctly.");

				var distance = await DistanceCalculationService.CalculateDistanceAsync(city1, city2, dbManager);
				if (distance == -1)
					return Results.BadRequest("One or both cities could not be found within our database.");
				else
					return Results.Ok("Distance between: " + city1 + " to " + city2 + " is: " + distance + ".");
			}
			else if (query.ContainsKey("cityName") || query.ContainsKey("cityId")) // GET city coordinates
			{
				if (int.TryParse(query["cityId"], out int cityId))
				{
					var cityInfo = await dbManager.GetCityInfo(cityId);
					if (cityInfo == null)
						return Results.NotFound("No city with given name was found");
					else
						return Results.Ok(new ApiResponse<CityInfo>(cityInfo, "Here is the info for the requested City"));
				}
				else
				{
					var cityName = query["cityName"].ToString();
					if (string.IsNullOrWhiteSpace(cityName))
						return Results.BadRequest("cityName parameter was not specified correctly.");

					var cityInfo = await dbManager.GetCityInfo(cityName);
					if (cityInfo == null)
						return Results.NotFound("No city with given name was found");
					else
						return Results.Ok(new ApiResponse<CityInfo>(cityInfo, "Here is the info for the requested City"));
				}
			}
			else if (query.ContainsKey("cityNameContains")) // GET cities that contain substring
			{
				//TODO validate input
				var cityNameContains = query["cityNameContains"];
				var cities = await dbManager.GetCitiesByNameAsync(cityNameContains);
				if (cities == null)
					return Results.NotFound("No city with given name was found");
				else
					return Results.Ok(new ApiResponse<List<CityInfo>>(cities, "Here is the full list of requested cities"));
			}
			else
				return Results.BadRequest("Invalid request parameters.\nValid request parameters are: 1) city1 && city2 => used for calculating distance between two cities.\n2) CityName || CityId => for latitude and longitude info about a city\n");
		case "POST":
			var cityDataForPost = await context.Request.ReadFromJsonAsync<CityInfo>();

			var addedCity = await dbManager.AddCityAsync(cityDataForPost);
			return Results.Ok(new ApiResponse<CityInfo>(addedCity, "Your city has successfully been added to our database."));
		case "PUT":
			var cityDataForPut = await context.Request.ReadFromJsonAsync<CityInfo>();

			var updatedCity = await dbManager.ModifyItemAsync(cityDataForPut);
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
					return Results.BadRequest("Invalid city ID.");
			}
			else
				return Results.BadRequest("City ID is required for delete operation.");
		default:
			return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
	}
});

app.Run();