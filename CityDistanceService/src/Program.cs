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