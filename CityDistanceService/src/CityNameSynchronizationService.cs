using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

public class CityNameSynchronizationService : BackgroundService
{
    private readonly IDatabaseManager _dbManager;
    private readonly ElasticSearchService _elSearch;
    private readonly TimeSpan _syncInterval;

    public CityNameSynchronizationService(IDatabaseManager dbManager, ElasticSearchService elSearch, int syncInterval)
    {
        _dbManager = dbManager;
        _elSearch = elSearch;
        _syncInterval = TimeSpan.FromMinutes(syncInterval);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SynchronizeCityNames();
            await Task.Delay(_syncInterval, stoppingToken);
        }
    }

    public async Task<IResult> SynchronizeCityNames()
    {
        var cityNames = await _dbManager.GetCityNames();
        var elasticSearchCityNames = await _elSearch.GetAllCityNamesFromElasticsearchAsync();

        var newCityNames = cityNames.Except(elasticSearchCityNames).Select(cityName => new CityName { Name = cityName }).ToList();

        if (newCityNames.Count > 0)
        {
            await _elSearch.UploadCitiesAsync(newCityNames);
        }

        return Results.Ok("City names have been synchronized.");
    }
}
