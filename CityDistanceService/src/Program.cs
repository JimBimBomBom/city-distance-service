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
    
    // In development, use HTTP only to avoid SSL issues
    if (builder.Environment.IsDevelopment())
    {
        serverOptions.ListenAnyIP(5000);
        Console.WriteLine("Development mode: Listening on HTTP port 5000");
    }
});

// Add CORS to handle requests from GitHub Pages
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
var esSkipSsl  = builder.Configuration.GetValue<bool>("Elasticsearch:SkipSslVerification");

Console.WriteLine($"Configuring Elasticsearch client...");
Console.WriteLine($"  URL: {esUrl}");
Console.WriteLine($"  Skip SSL Verification: {esSkipSsl}");

var settings = new ElasticsearchClientSettings(new Uri(esUrl))
    .Authentication(new BasicAuthentication("elastic", esPassword))
    .DefaultIndex("cities")
    .RequestTimeout(TimeSpan.FromMinutes(5));

// Skip SSL certificate validation for development (e.g., self-signed certs)
if (esSkipSsl || builder.Environment.IsDevelopment())
{
    settings = settings.ServerCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true);
    Console.WriteLine("  SSL certificate validation disabled for development");
}

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

    // File data import service - loads CSV files at startup from /cities_data
    var dataFilesPath = configuration["DATA_FILES_PATH"] ?? "/cities_data";
    builder.Services.AddSingleton<FileDataImportService>(_ =>
        new FileDataImportService(dataFilesPath,
            _.GetRequiredService<ILogger<FileDataImportService>>()));

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

// Startup sequence
Console.WriteLine("=== Starting City Distance Service ===");
Console.WriteLine("Loading data from: {0}", dataFilesPath);
Console.WriteLine("Application starting. Version: " + Constants.Version);

// Start background initialization - don't block startup
_ = Task.Run(async () =>
{
    // Wait a bit for app to start accepting requests
    await Task.Delay(2000);
    
    try
    {
        using var scope = app.Services.CreateScope();
        var fileImporter = scope.ServiceProvider.GetRequiredService<FileDataImportService>();
        var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
        var esService = scope.ServiceProvider.GetRequiredService<IElasticSearchService>();

        // Phase 1: Load all CSV language variants (en_cities.csv is processed first)
        Console.WriteLine("[Background] Loading all CSV language variants...");
        var allCities = await fileImporter.LoadAllLanguageVariantsAsync();

        if (allCities.Count == 0)
        {
            Console.WriteLine("[Background] Warning: No cities loaded from CSV files - nothing to import");
            return;
        }

        Console.WriteLine($"[Background] Loaded {allCities.Count} city records across {fileImporter.LoadedLanguages.Count} languages");

        // Phase 2: Insert into MySQL with INSERT IGNORE semantics
        // en_cities.csv establishes the baseline; later duplicates for the same city_id are skipped
        Console.WriteLine("[Background] Inserting cities into MySQL (INSERT IGNORE)...");
        await dbService.BulkUpsertCitiesAsync(allCities);
        Console.WriteLine($"[Background] MySQL import completed");

        // Phase 3: Ensure ES index exists (with retries)
        Console.WriteLine("[Background] Ensuring Elasticsearch index exists...");
        try
        {
            await RetryHelper.RetryOnExceptionAsync(
                maxRetries:    10,
                delay:         TimeSpan.FromSeconds(10),
                operation:     async () =>
                {
                    await esService.EnsureIndexExistsAsync();
                },
                operationName: "Elasticsearch index creation"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Background] WARNING: Elasticsearch unavailable: {ex.Message}");
            return; // Skip ES indexing if ES is not available
        }

        // Phase 4: Build Elasticsearch index with all language variants
        Console.WriteLine("[Background] Indexing all language variants in Elasticsearch...");
        await esService.BulkUpsertCitiesAsync(allCities);
        Console.WriteLine($"[Background] Indexed {allCities.Count} city language variants in Elasticsearch");

        // Verify the index has documents
        var docCount = await esService.GetDocumentCountAsync();
        Console.WriteLine($"[Background] Elasticsearch index now contains {docCount} documents");
        
        Console.WriteLine("[Background] Data import completed successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Background] ERROR during data import: {ex.Message}");
        Console.WriteLine($"[Background] Stack trace: {ex.StackTrace}");
    }
});

app.MapControllers();

// Endpoints

app.MapGet("/health_check", () =>
    Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow })
).AllowAnonymous();

app.MapGet("/version", () =>
    Results.Ok(new { Version = Constants.Version })
).AllowAnonymous();

app.MapGet("/languages", (FileDataImportService fileImporter) =>
{
    var languages = fileImporter.GetAvailableLanguages();
    return Results.Ok(languages.Select(code => new { code }));
}).AllowAnonymous();

app.MapGet("/db_health_check", async (IDatabaseService dbManager) =>
    await RequestHandler.TestConnection(dbManager)
).AllowAnonymous();

app.MapGet("/es_health_check", async (IElasticSearchService esService) =>
{
    try
    {
        var count = await esService.GetDocumentCountAsync();
        return Results.Ok(new
        {
            Status = "Healthy",
            DocumentCount = count,
            IndexName = "cities",
            Timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Elasticsearch health check failed: {ex.Message}");
    }
}).AllowAnonymous();

app.MapGet("/suggestions", async (
    HttpContext ctx,
    [FromQuery] string q,
    IElasticSearchService esService) =>
{
    var lang = ctx.Request.Cookies["lang"] ?? "en";
    return await RequestHandler.GetCitySuggestionsAsync(q, esService, lang);
}).AllowAnonymous();

app.MapPost("/distance", async (
    CitiesDistanceRequest request,
    ICityDataService cityService,
    IValidator<CitiesDistanceRequest> validator) =>
{
    var validationResult = await validator.ValidateAsync(request);
    if (!validationResult.IsValid)
        return Results.BadRequest(new { Errors = validationResult.Errors });

    return await RequestHandler.ProcessCityDistanceAsync(request, cityService);
})
.AddFluentValidationAutoValidation()
.AllowAnonymous();

app.MapGet("/city/{id}", async (
    HttpContext ctx,
    [FromRoute] string id,
    ICityDataService cityService) =>
{
    var lang = ctx.Request.Cookies["lang"] ?? "en";
    return await RequestHandler.ReturnCityInfoAsync(id, cityService, lang);
}).RequireAuthorization("BasicAuthentication");

app.MapPost("/city", async (
    NewCityInfo city,
    ICityDataService cityService,
    IValidator<NewCityInfo> validator) =>
{
    var validationResult = await validator.ValidateAsync(city);
    if (!validationResult.IsValid)
        return Results.BadRequest(new { Errors = validationResult.Errors });

    return await RequestHandler.PostCityInfoAsync(city, cityService);
})
.AddFluentValidationAutoValidation()
.RequireAuthorization("BasicAuthentication");

app.MapPut("/city", async (
    CityInfo city,
    ICityDataService cityService,
    IValidator<CityInfo> validator) =>
{
    var validationResult = await validator.ValidateAsync(city);
    if (!validationResult.IsValid)
        return Results.BadRequest(new { Errors = validationResult.Errors });

    return await RequestHandler.UpdateCityInfoAsync(city, cityService);
})
.AddFluentValidationAutoValidation()
.RequireAuthorization("BasicAuthentication");

app.MapDelete("/city/{id}", async (
    [FromRoute] string id,
    IDatabaseService dbManager,
    ICityDataService cityService) =>
{
    return await RequestHandler.DeleteCityAsync(id, dbManager, cityService);
}).RequireAuthorization("BasicAuthentication");

Console.WriteLine("Now serving requests.");
app.Run();
