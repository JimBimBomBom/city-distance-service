// Program.cs
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
var esUrl      = builder.Configuration["Elasticsearch:Url"]      ?? "http://elasticsearch:9200";
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
builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();
builder.Services.AddSingleton<IWikidataService, WikidataService>();
builder.Services.AddScoped<ICityDataService, CityDataService>();

var resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources");
builder.Services.AddSingleton<ILocalizationService>(
    new LocalizationService(resourcesPath, defaultLang: "en"));

// FluentMigrator
builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddMySql5()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

builder.Services.AddHostedService<WikidataSyncService>();

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

// Migrations
try
{
    RetryHelper.RetryOnException(10, TimeSpan.FromSeconds(10), () =>
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
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
    Console.WriteLine($"Selected language: {ctx.GetLanguage()}");
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

app.MapPost("/wikidata/sync", async (ICityDataService cityService) =>
    await RequestHandler.UpdateCityDatabaseAsync(cityService)
).AllowAnonymous();

Console.WriteLine("Now serving requests.");
app.Run();