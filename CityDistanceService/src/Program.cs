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
Console.WriteLine($"Basic authentication username: {configuration["AUTH_USERNAME"]}, password: {configuration["AUTH_PASSWORD"]}");

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

builder.Services.AddSingleton(provider => new ElasticSearchService(configuration));

if (string.IsNullOrEmpty(configuration["DATABASE_CONNECTION_STRING"]))
{
    Console.WriteLine("Database connection string not set.");
    return;
}

var connectionString = configuration["DATABASE_CONNECTION_STRING"];

builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(connectionString));

builder.Services.AddSingleton<IHostedService, CityNameSynchronizationService>(provider =>
{
    using (var scope = provider.CreateScope())
    {
        var elSearch = provider.GetRequiredService<ElasticSearchService>();
        var dbManager = scope.ServiceProvider.GetRequiredService<IDatabaseManager>();
        return new CityNameSynchronizationService(dbManager, elSearch, 5);
    }
});

// Configure FluentMigrator
builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddMySql5()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

// Configure FluentValidation
builder.Services.AddFluentValidationAutoValidation();
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
app.MapGet("/search/{name}", async ([FromRoute] string name, IDatabaseManager dbManager, ElasticSearchService elSearch) =>
{
    return await RequestHandler.ValidateAndReturnCitiesCloseMatchAsync(name, dbManager, elSearch);
}).AllowAnonymous();
app.MapGet("/city/{id}", async ([FromRoute] Guid id, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndReturnCityInfoAsync(id, dbManager);
}).RequireAuthorization("BasicAuthentication");

app.MapPost("/distance", async (CitiesDistanceRequest cities, IDatabaseManager dbManager, ElasticSearchService elSearch) =>
{
    return await RequestHandler.ValidateAndProcessCityDistanceAsync(cities, dbManager, elSearch);
}).AddFluentValidationAutoValidation().AllowAnonymous();
app.MapPost("/city", async (CityInfo city, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndPostCityInfoAsync(city, dbManager);
}).RequireAuthorization("BasicAuthentication");

app.MapPut("/city", async (CityInfo city, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndUpdateCityInfoAsync(city, dbManager);
}).RequireAuthorization().RequireAuthorization("BasicAuthentication");

app.MapDelete("/city/{id}", async ([FromRoute] Guid id, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndDeleteCityAsync(id, dbManager);
}).RequireAuthorization().RequireAuthorization("BasicAuthentication");

app.Run();
