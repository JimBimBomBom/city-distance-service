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

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddAuthorization();

// builder.Services.AddAuthentication("BasicAuthentication").AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
builder.Environment.WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "/wwwroot");

builder.Services.AddSwaggerGen();
// builder.Services.AddSwaggerGen(options =>
// {
//     options.SwaggerDoc($"{Constants.Version}", new OpenApiInfo
//     {
//         Version = $"{Constants.Version}",
//         Title = "City Distance Service",
//         Description = "A simple service to manage city information and calculate distances between cities.",
//     });
// });

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

    connectionString = "Server=db;Database=CityDistanceService;Uid=root;Pwd=changeme";
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

// Configure FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CityInfo>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "/wwwroot")),
    RequestPath = string.Empty,
});
app.UseRouting();

var exemptedPaths = new List<string> { "/health_check", "/db_health_check", "/version", "/swagger", "/swagger/index.html", "/swagger/0.1.15/swagger.json", "/wwwroot/index.html", "/favicon.ico" };
app.UseWhen(
    context =>
    !exemptedPaths.Any(p => context.Request.Path.StartsWithSegments(new PathString(p))),
    appBuilder =>
    {
        // appBuilder.UseAuthentication();
        appBuilder.UseAuthorization();
        appBuilder.UseMiddleware<BasicAuthMiddleware>();
    });

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

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

Console.WriteLine("App version: " + Constants.Version);

if (string.IsNullOrEmpty(Constants.Version))
{
    Console.WriteLine("No version found error.");
    return;
}

app.MapControllers().AllowAnonymous();

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
}).RequireAuthorization();
app.MapPost("/distance", async (CitiesDistanceRequest cities, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndProcessCityDistanceAsync(cities, dbManager);
}).AddFluentValidationAutoValidation();

app.MapPut("/city", async (CityInfo city, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndUpdateCityInfoAsync(city, dbManager);
}).RequireAuthorization();

app.MapDelete("/city/{id}", async ([FromRoute] Guid id, IDatabaseManager dbManager) =>
{
    return await RequestHandler.ValidateAndDeleteCityAsync(id, dbManager);
}).RequireAuthorization();

app.Run();
