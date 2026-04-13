// Program.cs - City Distance Service Main Application
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

// Add CORS to handle requests from GitHub Pages (backup for CDN misconfiguration)
builder.Services.AddCors(options =>
{
    options.AddPolicy("GitHubPages", policy =>
    {
        policy.WithOrigins("https://jimbimbombom.github.io")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
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
builder.Services.AddScoped<ICityDataService, CityDataService>();
builder.Services.AddSingleton<IElasticSearchService, ElasticSearchService>();

// File data import service
var dataFilesPath = configuration["DATA_FILES_PATH"] ?? "/app/output";
builder.Services.AddSingleton<FileDataImportService>(_ => 
    new FileDataImportService(dataFilesPath, 
        _.GetRequiredService<ILogger<FileDataImportService>>()));

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

app.UseCors("GitHubPages");
app.UseRouting();
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

// Startup sequence
Console.WriteLine("=== Starting City Distance Service ===");
Console.WriteLine("Loading data from: {0}", dataFilesPath);

// Step 1: Ensure ES index exists
try
{
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

// Step 2: Load data from JSON files and import to databases
try
{
    using var scope = app.Services.CreateScope();
    var fileImporter = scope.ServiceProvider.GetRequiredService<FileDataImportService>();
    var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
    var esService = scope.ServiceProvider.GetRequiredService<IElasticSearchService>();

    // Phase 1: Import English cities to MySQL (source of truth)
    Console.WriteLine("Importing English cities to MySQL...");
    var englishCities = await fileImporter.LoadEnglishCitiesAsync();
    
    if (englishCities.Count > 0)
    {
        await dbService.BulkUpsertCitiesAsync(englishCities);
        Console.WriteLine($"Imported {englishCities.Count} English cities to MySQL");
    }
    else
    {
        Console.WriteLine("Warning: No English cities loaded - using existing MySQL data");
    }

    // Phase 2: Build Elasticsearch index with all language variants
    Console.WriteLine("Loading all language variants for Elasticsearch...");
    var allCities = await fileImporter.LoadAllLanguageVariantsAsync();

    if (allCities.Count > 0)
    {
        await esService.BulkUpsertCitiesAsync(allCities);
        Console.WriteLine($"Indexed {allCities.Count} city language variants in Elasticsearch");
    }
    else
    {
        Console.WriteLine("Warning: No cities loaded from files - will use existing database data");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR during data import: {ex.Message}");
    Console.WriteLine("Application will continue with existing database data");
}

Console.WriteLine("Application started. Version: " + Constants.Version);

app.MapControllers();

// Endpoints

app.MapGet("/health_check", () =>
    Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow })
).AllowAnonymous();

app.MapGet("/version", () =>
    Results.Ok(new { Version = Constants.Version })
).AllowAnonymous();

app.MapGet("/languages", (FileDataImportService fileImporter) =>
    Results.Ok(fileImporter.LoadedLanguages.Select(code => new { code }))
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

Console.WriteLine("Now serving requests.");
app.Run();
