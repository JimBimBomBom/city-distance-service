// Program.cs
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

configuration.AddEnvironmentVariables();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 52428800; // 50 MB
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowGitHubPages", policy =>
        policy.WithOrigins("https://jimbimbombom.github.io")
              .AllowAnyMethod()
              .AllowAnyHeader());
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
        Version     = $"{Constants.Version}",
        Title       = "City Distance Service",
        Description = "A service to manage city information and calculate distances.",
    });
    options.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "basic",
        In          = ParameterLocation.Header,
        Description = "Basic Authorization header.",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "basic" }
            },
            Array.Empty<string>()
        },
    });
});

// Elasticsearch
var esUrl      = builder.Configuration["Elasticsearch:Url"]      ?? "http://cds-elasticsearch:9200";
var esPassword = builder.Configuration["Elasticsearch:Password"] ?? "testPassword123";

var settings = new ElasticsearchClientSettings(new Uri(esUrl))
    .Authentication(new BasicAuthentication("elastic", esPassword))
    .DefaultIndex("cities")
    .RequestTimeout(TimeSpan.FromMinutes(5));

builder.Services.AddSingleton(new ElasticsearchClient(settings));

// Database
if (string.IsNullOrEmpty(configuration["DATABASE_CONNECTION_STRING"]))
{
    Console.WriteLine("Database connection string not set.");
    return;
}

var connectionString = configuration["DATABASE_CONNECTION_STRING"];

// Services
builder.Services.AddScoped<IDatabaseService>(_ => new MySQLManager(connectionString));
builder.Services.AddSingleton<IWikidataService, WikidataService>();
builder.Services.AddScoped<ICityDataService, CityDataService>();
builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();

// Data generation and reload services - always enabled
builder.Services.AddSingleton<DataGenerationService>();
builder.Services.AddSingleton<IDataReloadService>(sp => 
{
    var logger = sp.GetRequiredService<ILogger<DataReloadService>>();
    var esService = sp.GetRequiredService<IElasticSearchService>();
    var dbManager = sp.GetRequiredService<IDatabaseService>() as MySQLManager 
        ?? throw new InvalidOperationException("Expected MySQLManager");
    return new DataReloadService(logger, connectionString, esService, dbManager);
});
builder.Services.AddHostedService(sp => sp.GetRequiredService<DataGenerationService>());
Console.WriteLine("DataGenerationService enabled - will fetch from Wikidata on startup, then monthly");

// Localization
var resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources");
builder.Services.AddSingleton<ILocalizationService>(
    new LocalizationService(resourcesPath, defaultLang: "en"));

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<NewCityInfo>();
builder.Services.AddValidatorsFromAssemblyContaining<CityInfo>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();
app.UseCors("AllowGitHubPages");      // must be before auth
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint($"/swagger/{Constants.Version}/swagger.json",
        "City Distance Service " + Constants.Version);
    c.RoutePrefix = "swagger";
});

app.UseMiddleware<ApplicationVersionMiddleware>();
app.UseMiddleware<LocaleMiddleware>();

// Startup sequence: ES index -> Load MySQL -> Check for SQL file -> Load if exists
try
{
    // Step 1: Ensure ES index exists
    await RetryHelper.RetryOnExceptionAsync(
        maxRetries:    10,
        delay:         TimeSpan.FromSeconds(10),
        operation:     async () =>
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IElasticSearchService>().EnsureIndexExistsAsync();
        },
        operationName: "Elasticsearch index creation"
    );
}
catch (Exception ex)
{
    Console.WriteLine($"WARNING: Elasticsearch unavailable: {ex.Message}");
}

// Step 2: Bulk-load MySQL cities into ES (seed data)
Console.WriteLine("Building Elasticsearch index from MySQL seed data...");
await RetryHelper.RetryOnExceptionAsync(
    maxRetries: 15,
    delay: TimeSpan.FromSeconds(10),
    operation: async () =>
    {
        using var scope = app.Services.CreateScope();
        var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
        var esService = scope.ServiceProvider.GetRequiredService<IElasticSearchService>();

        var allCities = await dbService.GetAllCitiesAsync();
        Console.WriteLine($"Found {allCities.Count} cities in MySQL (seed data) to index");

        if (allCities.Count > 0)
        {
            await esService.BulkIndexCitiesAsync(allCities);
            Console.WriteLine($"Successfully indexed {allCities.Count} seed cities in Elasticsearch");
        }
    },
    operationName: "ES index build from MySQL seed data"
);

// Step 3: Check if there's an existing SQL file from previous DataGenerationService runs
// If yes, load it immediately (so we have more than just seed cities at startup)
var sqlFilePath = "/app/data/cities.sql";
if (File.Exists(sqlFilePath))
{
    Console.WriteLine($"Found existing generated SQL file at {sqlFilePath}. Loading now...");
    try
    {
        using var scope = app.Services.CreateScope();
        var reloadService = scope.ServiceProvider.GetRequiredService<IDataReloadService>();
        await reloadService.ReloadFromSqlFileAsync(sqlFilePath);
        Console.WriteLine("✅ Existing SQL file loaded successfully. Data is available now.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Failed to load existing SQL file: {ex.Message}");
        Console.WriteLine("Application will continue with seed data. DataGenerationService will fetch fresh data.");
    }
}
else
{
    Console.WriteLine("No existing generated SQL file found. Running with seed data only.");
    Console.WriteLine("DataGenerationService will fetch from Wikidata and generate the SQL file.");
}

// Step 4: DataGenerationService is already registered as IHostedService
// It will start automatically and fetch from Wikidata (takes ~30-60 min for 20 languages)
// Then it will generate the SQL file and reload MySQL + ES

Console.WriteLine("Application started. Version: " + Constants.Version);

app.MapControllers();

// Endpoints

app.MapGet("/health_check", () =>
    Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow })
).AllowAnonymous();

app.MapGet("/version", () =>
    Results.Ok(new { Version = Constants.Version })
).AllowAnonymous();

app.MapGet("/languages", (ILocalizationService localization) =>
    Results.Ok(localization.AvailableLanguages)
).AllowAnonymous();

app.MapGet("/db_health_check", async (IDatabaseService dbManager) =>
    await RequestHandler.TestConnection(dbManager)
).AllowAnonymous();

app.MapGet("/suggestions", async (
    HttpContext ctx,
    [FromQuery] string q,
    IElasticSearchService esService,
    ILocalizationService localization) =>
{
    var lang = ctx.GetLanguage();
    return await RequestHandler.GetCitySuggestionsAsync(q, esService, localization, lang);
}).AllowAnonymous();

app.MapPost("/distance", async (
    HttpContext ctx,
    CitiesDistanceRequest request,
    ICityDataService cityService,
    ILocalizationService localization,
    IValidator<CitiesDistanceRequest> validator) =>
{
    Console.WriteLine($"Selected language: {ctx.GetLanguage()}");
    var validationResult = await validator.ValidateAsync(request);
    if (!validationResult.IsValid)
        return Results.BadRequest(new { Errors = validationResult.Errors });

    var lang = ctx.GetLanguage();
    return await RequestHandler.ProcessCityDistanceAsync(request, cityService, localization, lang);
})
.AddFluentValidationAutoValidation()
.AllowAnonymous();

app.MapGet("/city/{id}", async (
    HttpContext ctx,
    [FromRoute] string id,
    ICityDataService cityService,
    ILocalizationService localization) =>
{
    var lang = ctx.GetLanguage();
    return await RequestHandler.ReturnCityInfoAsync(id, cityService, localization, lang);
}).RequireAuthorization("BasicAuthentication");

app.MapPost("/city", async (
    HttpContext ctx,
    NewCityInfo city,
    ICityDataService cityService,
    ILocalizationService localization,
    IValidator<NewCityInfo> validator) =>
{
    var validationResult = await validator.ValidateAsync(city);
    if (!validationResult.IsValid)
        return Results.BadRequest(new { Errors = validationResult.Errors });

    var lang = ctx.GetLanguage();
    return await RequestHandler.PostCityInfoAsync(city, cityService, localization, lang);
})
.AddFluentValidationAutoValidation()
.RequireAuthorization("BasicAuthentication");

app.MapPut("/city", async (
    HttpContext ctx,
    CityInfo city,
    ICityDataService cityService,
    ILocalizationService localization,
    IValidator<CityInfo> validator) =>
{
    var validationResult = await validator.ValidateAsync(city);
    if (!validationResult.IsValid)
        return Results.BadRequest(new { Errors = validationResult.Errors });

    var lang = ctx.GetLanguage();
    return await RequestHandler.UpdateCityInfoAsync(city, cityService, localization, lang);
})
.AddFluentValidationAutoValidation()
.RequireAuthorization("BasicAuthentication");

app.MapDelete("/city/{id}", async (
    HttpContext ctx,
    [FromRoute] string id,
    IDatabaseService dbManager,
    ICityDataService cityService,
    ILocalizationService localization) =>
{
    var lang = ctx.GetLanguage();
    return await RequestHandler.DeleteCityAsync(id, dbManager, cityService, localization, lang);
}).RequireAuthorization("BasicAuthentication");

// Data endpoint - returns the generated SQL file
app.MapGet("/data", (DataGenerationService dataService) =>
{
    var sqlPath = dataService.GetSqlFilePath();
    if (sqlPath == null || !File.Exists(sqlPath))
    {
        return Results.NotFound(new { Error = "SQL data file not yet generated. Please wait for the next sync cycle." });
    }
    
    var lastUpdated = dataService.GetLastGeneratedAt();
    var fileName = $"cities_{lastUpdated:yyyyMMdd_HHmmss}.sql";
    
    return Results.File(sqlPath, "application/sql", fileName);
}).AllowAnonymous();

// Admin reload endpoint - private, only accessible from localhost
app.MapPost("/admin/reload", async (
    HttpContext context,
    IDataReloadService reloadService,
    DataGenerationService dataService) =>
{
    // Check if request is from localhost or private network
    var remoteIp = context.Connection.RemoteIpAddress;
    if (remoteIp == null)
    {
        return Results.StatusCode(403);
    }
    
    // Allow only loopback addresses
    if (!IPAddress.IsLoopback(remoteIp))
    {
        return Results.StatusCode(403);
    }
    
    var sqlPath = dataService.GetSqlFilePath();
    if (sqlPath == null || !File.Exists(sqlPath))
    {
        return Results.BadRequest(new { Error = "No SQL data file available. Please wait for the DataGenerationService to complete." });
    }
    
    try
    {
        await reloadService.ReloadFromSqlFileAsync(sqlPath);
        return Results.Ok(new { 
            Message = "Data reload initiated successfully",
            SqlFile = sqlPath,
            Timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Data reload failed: {ex.Message}");
    }
}).AllowAnonymous();

Console.WriteLine("Now serving requests.");
app.Run();
