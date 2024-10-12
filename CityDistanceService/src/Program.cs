using FluentMigrator.Runner;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using System.Configuration;
using Microsoft.AspNetCore.Authorization;
// using System.Text.Json;
// using System.Text.Json.Serialization;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

configuration.AddEnvironmentVariables();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();

builder.Services.AddAuthentication("BasicAuthentication").AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

if (string.IsNullOrEmpty(configuration["AUTH_USERNAME"]) || string.IsNullOrEmpty(configuration["AUTH_PASSWORD"]))
{
    Console.WriteLine("Basic authentication username or password not set.");
    return;
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BasicAuthentication", policy =>
        policy.Requirements.Add(new BasicAuthorizationRequirement(configuration["AUTH_USERNAME"], configuration["AUTH_PASSWORD"])));
});

builder.Services.AddSingleton<IAuthorizationHandler, BasicAuthorizationHandler>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc($"{Constants.Version}", new OpenApiInfo
    {
        Version = $"{Constants.Version}",
        Title = "City Distance Service",
        Description = "A simple service to manage city information and calculate distances between cities.",
    });

    // Add Basic Authentication option to SwaggerGen
    options.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Basic Authorization header using the Bearer scheme.",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "basic",
                },
            },
            Array.Empty<string>()
        },
    });
});

if (string.IsNullOrEmpty(configuration["DATABASE_CONNECTION_STRING"]))
{
    Console.WriteLine("Database connection string not set.");
    return;
}

var connectionString = configuration["DATABASE_CONNECTION_STRING"];

builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(connectionString));

// Configure FluentMigrator
builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddMySql5()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

// Configure FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<NewCityInfo>();
builder.Services.AddValidatorsFromAssemblyContaining<CityInfo>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseCors(builder =>
    builder.WithOrigins("https://jimbimbombom.github.io")
            .AllowAnyMethod()
            .AllowAnyHeader());

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint($"/swagger/{Constants.Version}/swagger.json", "City Distance Service " + Constants.Version);
    c.RoutePrefix = "swagger";
});

app.UseMiddleware<ApplicationVersionMiddleware>();

// Run migrations with retry logic at startup
try
{
    RetryHelper.RetryOnException(10, TimeSpan.FromSeconds(10), () =>
    {
        // Run migrations at startup
        using (var scope = app.Services.CreateScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            runner.MigrateUp();
        }
    });
}
catch (Exception ex)
{
    Console.WriteLine("Error running migrations: " + ex.Message);
    return;
}

// Add validation to endpoints in the /city group
var endpointGroup = app.MapGroup("/city").AddFluentValidationAutoValidation();

if (string.IsNullOrEmpty(Constants.Version))
{
    Console.WriteLine("No version found error.");
    return;
}
else
{
    Console.WriteLine("App version: " + Constants.Version);
}

app.MapControllers();

app.MapGet("/health_check", () =>
{
    return Results.Ok();
}).AllowAnonymous();
app.MapGet("/get_city_names", async (IDatabaseManager dbManager) =>
{
    return await RequestHandler.GetCityNames(dbManager);
}).AllowAnonymous();
app.MapGet("/db_health_check", async (IDatabaseManager dbManager) =>
{
    return await RequestHandler.TestConnection(dbManager);
}).AllowAnonymous();
app.MapGet("/version", () =>
{
    return Results.Ok(Constants.Version);
}).AllowAnonymous();
app.MapGet("/search/{name}", async ([FromRoute] string name, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndReturnCitiesCloseMatchAsync(name, dbManager);
}).AllowAnonymous();
app.MapGet("/city/{id}", async ([FromRoute] Guid id, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ReturnCityInfoAsync(id, dbManager);
}).RequireAuthorization("BasicAuthentication");

app.MapPost("/distance", async (CitiesDistanceRequest cities, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ProcessCityDistanceAsync(cities, dbManager);
}).AddFluentValidationAutoValidation().AllowAnonymous();
app.MapPost("/city", async (NewCityInfo city, IDatabaseManager dbManager, IValidator<NewCityInfo> validator) =>
{
    var validationResult = await validator.ValidateAsync(city);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(validationResult.Errors);
    }
    return await RequestHandler.PostCityInfoAsync(city, dbManager);
})
.AddFluentValidationAutoValidation()
.RequireAuthorization("BasicAuthentication");

app.MapPut("/city", async (CityInfo city, IDatabaseManager dbManager) =>
{
    return await RequestHandler.UpdateCityInfoAsync(city, dbManager);
}).RequireAuthorization().RequireAuthorization("BasicAuthentication");

app.MapDelete("/city/{id}", async ([FromRoute] Guid id, IDatabaseManager dbManager) =>
{
    return await RequestHandler.DeleteCityAsync(id, dbManager);
}).RequireAuthorization().RequireAuthorization("BasicAuthentication");

// app.MapPost("/cities/bulk", async (List<NewCityInfoJSON> cities, IDatabaseManager dbManager) =>
// {
//     return await RequestHandler.BulkInsertCitiesAsync(cities, dbManager);
// })
// .RequireAuthorization("BasicAuthentication");

app.MapPost("/cities-json/bulk", async (HttpRequest request, IDatabaseManager dbManager) =>
{
    if (!request.HasFormContentType || request.Form.Files.Count == 0)
    {
        return Results.BadRequest("Please upload a JSON file.");
    }

    var file = request.Form.Files[0];
    using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream);
    var jsonString = await reader.ReadToEndAsync();
    
    List<NewCityInfo> cities;
    try
    {
        cities = JsonConvert.DeserializeObject<List<NewCityInfo>>(jsonString);
        if (cities == null || cities.Count == 0)
        {
            return Results.BadRequest("The JSON file is empty or not in the correct format.");
        }
        Console.WriteLine(cities.Count);
    }
    catch (JsonException ex)
    {
        return Results.BadRequest($"Error parsing JSON: {ex.Message}");
    }

    var successCount = 0;
    foreach (var city in cities)
    {
        var result = await dbManager.AddCity(city);
        if (result == null)
        {
            Console.WriteLine($"Failed to add city {city.CityName} to database, or city already exists.");
            return Results.BadRequest("Failed to add city to database, or city already exists.");
        }
        successCount++;
    }

    return Results.Ok($"Successfully inserted {successCount} cities.");

    // return await RequestHandler.BulkInsertCitiesAsync(cities, dbManager);

})
.RequireAuthorization("BasicAuthentication");

app.Run();
