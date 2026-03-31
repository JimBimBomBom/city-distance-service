// Program.cs - Unified entry point for both Main App and Data Generator
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using System.Net;

var cliArgs = Environment.GetCommandLineArgs();

// Check if we should run in generator mode
if (cliArgs.Contains("--generator") || Environment.GetEnvironmentVariable("GENERATOR_MODE") == "true")
{
    await RunGeneratorModeAsync();
}
else
{
    await RunMainAppModeAsync();
}

// ============================================
// GENERATOR MODE
// ============================================
async Task RunGeneratorModeAsync()
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddEnvironmentVariables();
    
    // Configure Kestrel to listen on port 5000
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(5000);
    });
    
    // Required services
    builder.Services.AddSingleton<IWikidataService, WikidataService>();
    
    // Register the DataGenerator service
    builder.Services.AddSingleton<DataGenerator>();
    
    // Add logging
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Information);
    
    var app = builder.Build();
    
    app.UseRouting();
    
    // Health check
    app.MapGet("/health_check", () =>
        Results.Ok(new { Status = "Healthy", Service = "DataGenerator", Timestamp = DateTime.UtcNow })
    ).AllowAnonymous();
    
    // Status endpoint - returns whether data is ready
    app.MapGet("/status", (DataGenerator generator) =>
    {
        var status = generator.GetStatus();
        return Results.Ok(status);
    }).AllowAnonymous();
    
    // Data endpoint - serves the generated SQL file (for app to download after receiving signal)
    app.MapGet("/data", (DataGenerator generator) =>
    {
        var sqlPath = generator.GetSqlFilePath();
        if (sqlPath == null || !File.Exists(sqlPath))
        {
            return Results.NotFound(new { 
                Error = "SQL data file not yet generated", 
                IsGenerating = generator.GetStatus().IsGenerating,
                Message = "Please wait for data generation to complete. Check /status for progress."
            });
        }
        
        var status = generator.GetStatus();
        var fileName = $"cities_{status.LastGeneratedAt:yyyyMMdd_HHmmss}.sql";
        
        return Results.File(sqlPath, "application/sql", fileName);
    }).AllowAnonymous();
    
    // Startup: Begin continuous data generation loop
    Console.WriteLine("==========================================");
    Console.WriteLine("  City Distance Service - Data Generator");
    Console.WriteLine("==========================================");
    Console.WriteLine();
    Console.WriteLine("Endpoints:");
    Console.WriteLine("  GET /health_check  - Health check");
    Console.WriteLine("  GET /status        - Check generation status");
    Console.WriteLine("  GET /data          - Download SQL file (when ready)");
    Console.WriteLine("  POST /admin/start  - Start generation immediately (localhost only)");
    Console.WriteLine("  POST /admin/cancel - Cancel generation (localhost only)");
    Console.WriteLine();
    
    // Get the generator and start the continuous generation loop in background
    var dataGenerator = app.Services.GetRequiredService<DataGenerator>();
    
    Console.WriteLine("Starting continuous data generation loop...");
    Console.WriteLine("Generator will fetch from Wikidata, then periodically refresh.");
    Console.WriteLine("This will take several hours due to rate limiting.");
    Console.WriteLine();
    
    // Start the generation loop in background (it handles periodic refreshes)
    _ = dataGenerator.StartGenerationLoopAsync();
    
    Console.WriteLine("✅ Generation loop started.");
    Console.WriteLine("   Generator is running on http://0.0.0.0:5000");
    Console.WriteLine();
    
    app.Run();
}

// ============================================
// MAIN APP MODE
// ============================================
async Task RunMainAppModeAsync()
{
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
    
    // Data reload service - for loading SQL from generator
    builder.Services.AddScoped<IDataReloadService>(sp => 
    {
        var logger = sp.GetRequiredService<ILogger<DataReloadService>>();
        var esService = sp.GetRequiredService<IElasticSearchService>();
        var dbManager = sp.GetRequiredService<IDatabaseService>() as MySQLManager 
            ?? throw new InvalidOperationException("Expected MySQLManager");
        return new DataReloadService(logger, connectionString, esService, dbManager);
    });
    
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
    
    // Startup sequence: ES index -> Load MySQL -> Check for data from generator
    Console.WriteLine("=== Starting City Distance Service ===");
    
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
    
    // Ensure data directory exists for SQL file storage
    var dataDirectory = "/app/data";
    Directory.CreateDirectory(dataDirectory);
    var localSqlPath = Path.Combine(dataDirectory, "cities.sql");
    
    // Startup sequence: ES index -> Load MySQL seed data (app starts immediately, generator runs separately)
    Console.WriteLine("=== Starting City Distance Service ===");
    
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
    
    Console.WriteLine("Application started. Version: " + Constants.Version);
    Console.WriteLine("Note: Generator service runs independently. App will be signaled when new data is available.");
    
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
    
    // Generator reload endpoint - downloads SQL from generator and reloads (called by generator when data is ready)
    app.MapPost("/admin/reload-from-generator", async (
        HttpContext context,
        IDataReloadService reloadService,
        IConfiguration config) =>
    {
        // Only accept from internal network (generator service)
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null)
        {
            return Results.StatusCode(403);
        }
        
        // In Docker, the generator will appear as a non-loopback internal IP
        // We check that it's not from the public internet
        // Allow private IP ranges: 10.x.x.x, 172.16-31.x.x, 192.168.x.x, 127.x.x.x
        var remoteIpString = remoteIp.ToString();
        bool isPrivate = remoteIpString.StartsWith("127.") || 
                         remoteIpString.StartsWith("10.") || 
                         remoteIpString.StartsWith("192.168.") ||
                         (remoteIpString.StartsWith("172.") && 
                          int.TryParse(remoteIpString.Split('.')[1], out int secondOctet) && 
                          secondOctet >= 16 && secondOctet <= 31);
        
        if (!isPrivate)
        {
            return Results.StatusCode(403);
        }
        
        try
        {
            var generatorEndpoint = config["DATA_GENERATOR_ENDPOINT"] ?? "http://cds-datagenerator:5000";
            var dataUrl = $"{generatorEndpoint}/data";
            
            Console.WriteLine($"Generator signaled new data available. Downloading from {dataUrl}...");
            
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var response = await httpClient.GetAsync(dataUrl);
            response.EnsureSuccessStatusCode();
            
            var sqlContent = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(localSqlPath, sqlContent);
            
            Console.WriteLine($"✅ Downloaded SQL file ({sqlContent.Length / 1024 / 1024} MB)");
            
            // Reload databases
            Console.WriteLine("Loading SQL data into databases...");
            await reloadService.ReloadFromSqlFileAsync(localSqlPath);
            
            Console.WriteLine("✅ Data reload completed successfully");
            
            return Results.Ok(new { 
                Message = "Data downloaded and reloaded successfully",
                FileSizeBytes = sqlContent.Length,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to reload from generator: {ex.Message}");
            return Results.Problem($"Failed to reload data: {ex.Message}");
        }
    }).AllowAnonymous();
    
    // Admin reload endpoint - triggers reload from locally stored SQL file
    app.MapPost("/admin/reload", async (
        HttpContext context,
        IDataReloadService reloadService) =>
    {
        // Check if request is from localhost
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null || !IPAddress.IsLoopback(remoteIp))
        {
            return Results.StatusCode(403);
        }
        
        if (!File.Exists(localSqlPath))
        {
            return Results.BadRequest(new { Error = "No SQL data file available locally." });
        }
        
        try
        {
            await reloadService.ReloadFromSqlFileAsync(localSqlPath);
            return Results.Ok(new { 
                Message = "Data reload initiated successfully from local file",
                SqlFile = localSqlPath,
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
}
