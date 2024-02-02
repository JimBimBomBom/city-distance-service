using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

using RequestHandler;


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
				return await RequestHandler.RequestHandlerClass.ValidateAndProcessCityDistanceAsync(context, dbManager);
			else if (query.ContainsKey("cityName") || query.ContainsKey("cityId")) // GET city coordinates
				return await RequestHandler.RequestHandlerClass.ValidateAndReturnCityInfoAsync(context, dbManager);
			else if (query.ContainsKey("cityNameContains")) // GET cities that contain substring
				return await RequestHandler.RequestHandlerClass.ValidateAndReturnCitiesCloseMatchAsync(context, dbManager);
			else
				return Results.BadRequest("Invalid request parameters.\nValid request parameters are: 1) city1 && city2 => used for calculating distance between two cities.\n2) CityName || CityId => for latitude and longitude info about a city\n");
		case "POST":
			return await RequestHandler.RequestHandlerClass.ValidateAndPostCityInfoAsync(context, dbManager);
		case "PUT":
			return await RequestHandler.RequestHandlerClass.ValidateAndUpdateCityInfoAsync(context, dbManager);
		case "DELETE":
			return await RequestHandler.RequestHandlerClass.ValidateAndDeleteCityAsync(context, dbManager);
		default:
			return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
	}
});

app.Run();