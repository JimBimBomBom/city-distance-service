// Program.cs - Updated dependency injection registration
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

configuration.AddEnvironmentVariables();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 52428800; // 50 MB
});

builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

if (string.IsNullOrEmpty(configuration["AUTH_USERNAME"]) || 
    string.IsNullOrEmpty(configuration["AUTH_PASSWORD"]))
{
    Console.WriteLine("Basic authentication username or password not set.");
    return;
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BasicAuthentication", policy =>
        policy.Requirements.Add(new BasicAuthorizationRequirement(
            configuration["AUTH_USERNAME"], 
            configuration["AUTH_PASSWORD"])));
});

builder.Services.AddSingleton<IAuthorizationHandler, BasicAuthorizationHandler>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc($"{Constants.Version}", new OpenApiInfo
    {
        Version = $"{Constants.Version}",
        Title = "City Distance Service",
        Description = "A service to manage city information and calculate distances.",
    });

    options.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Basic Authorization header.",
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

// ========== Elasticsearch Configuration ==========
var esUrl = builder.Configuration["Elasticsearch:Url"] ?? "http://elasticsearch:9200";
var esPassword = builder.Configuration["Elasticsearch:Password"] ?? "testPassword123";

var settings = new ElasticsearchClientSettings(new Uri(esUrl))
    .Authentication(new BasicAuthentication("elastic", esPassword))
    .DefaultIndex("cities")
    .RequestTimeout(TimeSpan.FromMinutes(5));

var esClient = new ElasticsearchClient(settings);
builder.Services.AddSingleton(esClient);

// ========== Database Configuration ==========
if (string.IsNullOrEmpty(configuration["DATABASE_CONNECTION_STRING"]))
{
    Console.WriteLine("Database connection string not set.");
    return;
}

var connectionString = configuration["DATABASE_CONNECTION_STRING"];

// ========== Service Registration ==========
builder.Services.AddScoped<IDatabaseService>(provider => 
    new MySQLManager(connectionString));

builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();

builder.Services.AddSingleton<IWikidataService, WikidataService>();

builder.Services.AddScoped<ICityDataService, CityDataService>();

// FluentMigrator
builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddMySql5()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

// Background sync service (if you have one)
builder.Services.AddHostedService<WikidataSyncService>();

// FluentValidation
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
    c.SwaggerEndpoint($"/swagger/{Constants.Version}/swagger.json", 
        "City Distance Service " + Constants.Version);
    c.RoutePrefix = "swagger";
});

app.UseMiddleware<ApplicationVersionMiddleware>();

// Run migrations
try
{
    RetryHelper.RetryOnException(10, TimeSpan.FromSeconds(10), () =>
    {
        using var scope = app.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    });
}
catch (Exception ex)
{
    Console.WriteLine("Error running migrations: " + ex.Message);
    return;
}

try
{
    await RetryHelper.RetryOnExceptionAsync(
        maxRetries: 10,
        delay: TimeSpan.FromSeconds(10),
        operation: async () =>
        {
            using var scope = app.Services.CreateScope();
            var esService = scope.ServiceProvider.GetRequiredService<IElasticSearchService>();
            await esService.EnsureIndexExistsAsync();
            Console.WriteLine("Elasticsearch index check completed successfully.");
        },
        operationName: "Elasticsearch index creation"
    );
}
catch (Exception ex)
{
    Console.WriteLine($"WARNING: Could not connect to Elasticsearch after multiple retries: {ex.Message}");
    Console.WriteLine("Application will continue, but search functionality may not work until Elasticsearch is available.");
    Console.WriteLine("You can manually trigger index creation by restarting the service once Elasticsearch is ready.");
}

Console.WriteLine("Application started successfully!");

Console.WriteLine("App version: " + Constants.Version);

app.MapControllers();

// ========== Endpoints ==========
app.MapGet("/health_check", () => 
{
    return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
}).AllowAnonymous();

// Version endpoint
app.MapGet("/version", () => 
{
    return Results.Ok(new { Version = Constants.Version, Timestamp = DateTime.UtcNow });
}).AllowAnonymous();

// Database health check - uses RequestHandler
app.MapGet("/db_health_check", async (IDatabaseService dbManager) =>
{
    return await RequestHandler.TestConnection(dbManager);
}).AllowAnonymous();

// Search for cities (autocomplete/suggestions) - uses RequestHandler
app.MapGet("/search/{name}", async ([FromRoute] string name, ICityDataService cityService) =>
{
    return await RequestHandler.ValidateAndReturnCitySuggestionsAsync(name, cityService);
}).AllowAnonymous();

// Find a specific city by name (returns full CityInfo) - uses RequestHandler
app.MapGet("/find/{name}", async ([FromRoute] string name, ICityDataService cityService) =>
{
    return await RequestHandler.FindCityByNameAsync(name, cityService);
}).AllowAnonymous();

// Get city by ID - uses RequestHandler
app.MapGet("/city/{id}", async ([FromRoute] string id, IDatabaseService dbManager) =>
{
    return await RequestHandler.ReturnCityInfoAsync(id, dbManager);
}).RequireAuthorization("BasicAuthentication");

// Trigger Wikidata sync - uses RequestHandler
app.MapPost("/wikidata/sync", async (ICityDataService cityService) =>
{
    return await RequestHandler.UpdateCityDatabaseAsync(cityService);
}).AllowAnonymous(); // Consider adding authorization for production

// Calculate distance between two cities - uses RequestHandler
app.MapPost("/distance", async (
    CitiesDistanceRequest request, 
    ICityDataService cityService,
    IValidator<CitiesDistanceRequest> validator) =>
{
    var validationResult = await validator.ValidateAsync(request);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(new { Errors = validationResult.Errors });
    }

    return await RequestHandler.ProcessCityDistanceAsync(request, cityService);
})
.AddFluentValidationAutoValidation()
.AllowAnonymous();

// Add a new city - uses RequestHandler
app.MapPost("/city", async (
    NewCityInfo city, 
    ICityDataService cityService,
    IValidator<NewCityInfo> validator) =>
{
    var validationResult = await validator.ValidateAsync(city);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(new { Errors = validationResult.Errors });
    }

    return await RequestHandler.PostCityInfoAsync(city, cityService);
})
.AddFluentValidationAutoValidation()
.RequireAuthorization("BasicAuthentication");

// Update an existing city - uses RequestHandler
app.MapPut("/city", async (
    CityInfo city, 
    ICityDataService cityService,
    IValidator<CityInfo> validator) =>
{
    var validationResult = await validator.ValidateAsync(city);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(new { Errors = validationResult.Errors });
    }

    return await RequestHandler.UpdateCityInfoAsync(city, cityService);
})
.AddFluentValidationAutoValidation()
.RequireAuthorization("BasicAuthentication");

// Delete a city - uses RequestHandler
app.MapDelete("/city/{id}", async (
    [FromRoute] string id, 
    IDatabaseService dbManager,
    ICityDataService cityService) =>
{
    return await RequestHandler.DeleteCityAsync(id, dbManager, cityService);
})
.RequireAuthorization("BasicAuthentication");

Console.WriteLine("Successfull startup. Now serving requests.");
app.Run();
