using FluentMigrator.Runner;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Drawing.Printing;

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
    Environment.SetEnvironmentVariable("DATABASE_CONNECTION_STRING", "Server=db;Database=city_distance;Uid=root;Pwd=changeme");
    connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
    Console.WriteLine(connectionString);

    Console.WriteLine("DATABASE_CONNECTION_STRING environment variable not set.");
}

builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(connectionString));

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AUTH_USERNAME")) || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AUTH_PASSWORD")))
{
    Environment.SetEnvironmentVariable("AUTH_USERNAME", "admin");
    Environment.SetEnvironmentVariable("AUTH_PASSWORD", "password");

    Console.WriteLine("AUTH_USERNAME or AUTH_PASSWORD environment variable not set.");
}

// Configure FluentMigrator
builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddMySql5()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CityInfo>();

var app = builder.Build();

// Run migrations with retry logic at startup
RetryHelper.RetryOnException(10, TimeSpan.FromSeconds(10), () =>
{
    // Run migrations at startup
    using (var scope = app.Services.CreateScope())
    {
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }
});

app.UseMiddleware<ApplicationVersionMiddleware>();
app.UseMiddleware<BasicAuthMiddleware>();

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

app.MapGet("/city/{id}", async ([FromRoute] Guid id, IDatabaseManager dbManager) =>
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

app.MapDelete("/city/{id}", async ([FromRoute] Guid id, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndDeleteCityAsync(id, dbManager);
});

app.Run();
