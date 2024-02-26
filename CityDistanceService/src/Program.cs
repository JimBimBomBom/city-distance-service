using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

using RequestHandler;


var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// builder.Services.AddScoped<DatabaseManager>(_ => new DatabaseManager(configuration)); // TEST
builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(configuration)); // TEST

// builder.Services
// .AddControllers()
// .AddFluentValidation(fv=>fv.RegisterValidatorsFromAssemblyContaining<CityInfo>());

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CityInfo>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.MapGet("/health_check", () =>
{
	return Results.Ok();
});

// app.MapGet("/city", async (CityId cityId, IDatabaseManager dbManager) =>
// {
// 	return await RequestHandler.RequestHandlerClass.ValidateAndReturnCityInfoAsync(cityId, dbManager);
// }).AddFluentValidationAutoValidation();
app.MapGet("/city", async (HttpContext context, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndReturnCityInfoAsync(context, dbManager);
});
app.MapPost("/city", async (CityInfo city, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndPostCityInfoAsync(city, dbManager);
}).AddFluentValidationAutoValidation();
app.MapPut("/city", async (CityInfo city, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndUpdateCityInfoAsync(city, dbManager);
}).AddFluentValidationAutoValidation();
// app.MapDelete("/city", async (CityId cityId, IDatabaseManager dbManager) =>
// {
// 	return await RequestHandler.RequestHandlerClass.ValidateAndDeleteCityAsync(cityId, dbManager);
// }).AddFluentValidationAutoValidation();
app.MapDelete("/city", async (HttpContext context, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndDeleteCityAsync(context, dbManager);
});

app.MapPost("/distance", async (CitiesDistanceRequest cities, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndProcessCityDistanceAsync(cities, dbManager);
}).AddFluentValidationAutoValidation();

// app.MapGet("/search", async (CityName cityName, IDatabaseManager dbManager) =>
// {
// 	return await RequestHandler.RequestHandlerClass.ValidateAndReturnCitiesCloseMatchAsync(cityName, dbManager);
// }).AddFluentValidationAutoValidation();
app.MapGet("/search", async (HttpContext context, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndReturnCitiesCloseMatchAsync(context, dbManager);
});

app.Run();