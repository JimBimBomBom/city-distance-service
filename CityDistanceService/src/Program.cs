using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using System.IO;

using RequestHandler;
using System.Drawing.Printing;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// builder.Services.AddScoped<DatabaseManager>(_ => new DatabaseManager(configuration)); // TEST

configuration.AddEnvironmentVariables();
if (configuration["DATABASE_TYPE"] == "MYSQL-CLOUD_SQL")
{
    Console.WriteLine(configuration["DB_CONNECTION"]);
    var connectionString = configuration["DATABASE_CONNECTION_STRING"];
    if (string.IsNullOrEmpty(connectionString))
    {
        Console.WriteLine("No connection string found.");
        return;
    }
    Console.WriteLine("Connection string: " + connectionString);
    builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(connectionString));
}
else
{
    var connectionString = configuration["DATABASE_CONNECTION_STRING"];
    if (string.IsNullOrEmpty(connectionString))
    {
        Console.WriteLine("No connection string found.");
        return;
    }
    Console.WriteLine("Connection string: " + connectionString);
    builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(connectionString));
}

// var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
// builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(connectionString)); // TEST

// builder.Services
// .AddControllers()
// .AddFluentValidation(fv=>fv.RegisterValidatorsFromAssemblyContaining<CityInfo>());

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CityInfo>();

var app = builder.Build();

app.UseMiddleware<ApplicationVersionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
var endpointGroup = app.MapGroup("/city").AddFluentValidationAutoValidation();

var version = configuration["APP_VERSION"];
Console.WriteLine("App version: " + version);


if (string.IsNullOrEmpty(version))
{
    Console.WriteLine("No version found error.");
    return;
}

app.MapGet("/version", () =>
{
    return Results.Ok(configuration["APP_VERSION"]);
});

app.MapGet("/health_check", () =>
{
    Console.WriteLine("Health check endpoint called.");
    return Results.Ok();
});
app.MapGet("/db_health_check", async (IDatabaseManager dbManager) =>
{
    Console.WriteLine("DB Health check endpoint called.");
    return await RequestHandler.RequestHandlerClass.TestConnection(dbManager);
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