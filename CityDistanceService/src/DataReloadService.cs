using Microsoft.Extensions.Logging;
using MySqlConnector;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service that handles reloading data from SQL file into MySQL and reindexing Elasticsearch.
/// </summary>
public interface IDataReloadService
{
    Task ReloadFromSqlFileAsync(string sqlFilePath, CancellationToken cancellationToken = default);
}

public class DataReloadService : IDataReloadService
{
    private readonly ILogger<DataReloadService> _logger;
    private readonly string _connectionString;
    private readonly IElasticSearchService _esService;
    private readonly MySQLManager _mySqlManager;

    public DataReloadService(
        ILogger<DataReloadService> logger,
        string connectionString,
        IElasticSearchService esService,
        MySQLManager mySqlManager)
    {
        _logger = logger;
        _connectionString = connectionString;
        _esService = esService;
        _mySqlManager = mySqlManager;
    }

    public async Task ReloadFromSqlFileAsync(string sqlFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sqlFilePath))
        {
            throw new FileNotFoundException("SQL file not found", sqlFilePath);
        }

        _logger.LogInformation("Starting data reload from SQL file: {Path}", sqlFilePath);

        try
        {
            // Step 1: Load SQL into MySQL
            await LoadSqlIntoMySqlAsync(sqlFilePath, cancellationToken);
            
            // Step 2: Reindex Elasticsearch from MySQL
            await ReindexElasticsearchAsync(cancellationToken);
            
            _logger.LogInformation("Data reload completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data reload failed.");
            throw;
        }
    }

    private async Task LoadSqlIntoMySqlAsync(string sqlFilePath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading SQL file into MySQL...");

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Read SQL file and execute statements
        var sqlContent = await File.ReadAllTextAsync(sqlFilePath, cancellationToken);
        
        // Split by semicolon to get individual statements
        var statements = sqlContent.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        int executedCount = 0;
        
        using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var statement in statements)
            {
                var trimmed = statement.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("--") || trimmed.StartsWith("/*"))
                    continue;

                using var command = new MySqlCommand(trimmed, connection, transaction);
                await command.ExecuteNonQueryAsync(cancellationToken);
                executedCount++;
            }
            
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("SQL loading complete. Executed {Count} statements.", executedCount);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to execute SQL script");
            throw;
        }
    }

    private async Task ReindexElasticsearchAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reindexing Elasticsearch from MySQL...");

        // Get all cities from MySQL
        var allCities = await _mySqlManager.GetAllCitiesAsync();
        
        if (allCities.Count == 0)
        {
            _logger.LogWarning("No cities found in MySQL to index.");
            return;
        }

        _logger.LogInformation("Found {Count} cities in MySQL to index in Elasticsearch.", allCities.Count);

        // Reindex in Elasticsearch
        await _esService.BulkIndexCitiesAsync(allCities);

        _logger.LogInformation("Elasticsearch reindexing complete.");
    }
}
