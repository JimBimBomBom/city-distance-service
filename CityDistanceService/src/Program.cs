using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

using RequestHandler;


var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// builder.Services.AddScoped<DatabaseManager>(_ => new DatabaseManager(configuration)); // TEST
builder.Services.AddScoped<IDatabaseManager>(provider => new MySQLManager(configuration)); // TEST

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.MapGet("/city", async (HttpContext context, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndReturnCityInfoAsync(context, dbManager);
});
app.MapPost("/city", async (HttpContext context, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndPostCityInfoAsync(context, dbManager);
});
app.MapPut("/city", async (HttpContext context, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndUpdateCityInfoAsync(context, dbManager);
});
app.MapDelete("/city", async (HttpContext context, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndDeleteCityAsync(context, dbManager);
});

app.MapGet("/distance", async (HttpContext context, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndProcessCityDistanceAsync(context, dbManager);
});

app.MapGet("/search", async (HttpContext context, IDatabaseManager dbManager) =>
{
	return await RequestHandler.RequestHandlerClass.ValidateAndReturnCitiesCloseMatchAsync(context, dbManager);
});

app.Run();