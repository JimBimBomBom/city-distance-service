using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

configuration.AddEnvironmentVariables();
var connectionString = configuration["DATABASE_CONNECTION_STRING"];
if (string.IsNullOrEmpty(connectionString))
{
    // configuration["DATABASE_CONNECTION_STRING"] = "Server=db;Database=city_distance;Uid=root;Pwd=changeme";
    Environment.SetEnvironmentVariable("DATABASE_CONNECTION_STRING", "Server=db;Database=city_distance;Uid=root;Pwd=changeme");
    Console.WriteLine("DATABASE_CONNECTION_STRING environment variable not set.");
}

builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(connectionString));

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CityInfo>();

var app = builder.Build();

app.UseMiddleware<ApplicationVersionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.UseCors("AllowAll");
var endpointGroup = app.MapGroup("/city").AddFluentValidationAutoValidation();

Console.WriteLine("App version: " + Constants.Version);

if (string.IsNullOrEmpty(Constants.Version))
{
    Console.WriteLine("No version found error.");
    return;
}

app.MapGet("/health_check", () =>
{
    return Results.Ok();
});
app.MapGet("/db_health_check", async (IDatabaseManager dbManager) =>
{
    return await RequestHandler.TestConnection(dbManager);
});
app.MapGet("/version", () =>
{
    return Results.Ok(Constants.Version);
});

app.MapGet("/city/{id}", async ([FromRoute] int id, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndReturnCityInfoAsync(id, dbManager);
});
app.MapGet("/search/{name}", async ([FromRoute] string name, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndReturnCitiesCloseMatchAsync(name, dbManager);
});

app.MapPost("/city", async (CityInfo city, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndPostCityInfoAsync(city, dbManager);
});
app.MapPost("/distance", async (CitiesDistanceRequest cities, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndProcessCityDistanceAsync(cities, dbManager);
}).AddFluentValidationAutoValidation();

app.MapPut("/city", async (CityInfo city, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndUpdateCityInfoAsync(city, dbManager);
});

app.MapDelete("/city/{id}", async ([FromRoute] int id, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndDeleteCityAsync(id, dbManager);
});

app.Run();
