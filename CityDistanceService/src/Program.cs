using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
// builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(configuration)); // TEST
// var connectionString = configuration.GetConnectionString("database-connection-string");
var connectionString = Environment.GetEnvironmentVariable("DatabaseConnectionString");
builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(connectionString)); // TEST

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
var endpointGroup = app.MapGroup("/city").AddFluentValidationAutoValidation();

app.MapGet("/health_check", () =>
{
	return Results.Ok();
});

app.MapGet("/city/{id}", async ([FromRoute] int id, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndReturnCityInfoAsync(id, dbManager);
});
app.MapPost("/city", async (CityInfo city, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndPostCityInfoAsync(city, dbManager);
});
app.MapPut("/city", async (CityInfo city, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndUpdateCityInfoAsync(city, dbManager);
});
app.MapDelete("/city/{id}", async ([FromRoute] int id, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndDeleteCityAsync(id, dbManager);
});

app.MapPost("/distance", async (CitiesDistanceRequest cities, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndProcessCityDistanceAsync(cities, dbManager);
}).AddFluentValidationAutoValidation();

app.MapGet("/search/{name}", async ([FromRoute] string name, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndReturnCitiesCloseMatchAsync(name, dbManager);
});

app.Run();