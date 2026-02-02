using MySqlConnector;
using Microsoft.AspNetCore.Http;

public class MySQLManager : IDatabaseService
{
    private readonly string _connectionString;

    public MySQLManager(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IResult> TestConnection()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            Console.WriteLine("Connection successful.");
            return Results.Ok(new { Message = "Connection successful", ConnectionString = _connectionString });
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return Results.BadRequest(new { Error = ex.Message });
        }
    }

    public async Task<CityInfo?> GetCity(string cityId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT * FROM cities WHERE CityId = @CityId;";
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityId", cityId);

            using var reader = await command.ExecuteReaderAsync();
            if (reader.Read())
            {
                return new CityInfo
                {
                    CityId = reader.GetString(reader.GetOrdinal("CityId")),
                    CityName = reader.GetString(reader.GetOrdinal("CityName")),
                    Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                    Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                };
            }

            Console.WriteLine($"City with ID {cityId} not found.");
            return null;
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }

    public async Task<List<CityInfo>> GetCities(List<string> cityIds)
    {
        var cities = new List<CityInfo>();

        if (!cityIds.Any())
            return cities;

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var parameters = cityIds.Select((id, index) => $"@CityId{index}").ToList();
            var query = $"SELECT * FROM cities WHERE CityId IN ({string.Join(",", parameters)});";
            using var command = new MySqlCommand(query, connection);

            for (int i = 0; i < cityIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@CityId{i}", cityIds[i]);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                cities.Add(new CityInfo
                {
                    CityId = reader.GetString(reader.GetOrdinal("CityId")),
                    CityName = reader.GetString(reader.GetOrdinal("CityName")),
                    Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                    Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                });
            }

            Console.WriteLine($"Retrieved {cities.Count} cities.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }

        return cities;
    }

    public async Task<Coordinates?> GetCityCoordinates(string cityId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT Latitude, Longitude FROM cities WHERE CityId = @CityId;";
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityId", cityId);

            using var reader = await command.ExecuteReaderAsync();
            if (reader.Read())
            {
                return new Coordinates
                {
                    Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                    Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                };
            }

            Console.WriteLine($"Coordinates for city {cityId} not found.");
            return null;
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }

    public async Task<CityInfo> AddCity(NewCityInfo city)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Check if city already exists by name
            var existingCity = await GetCityByName(city.CityName, connection);
            if (existingCity != null)
            {
                Console.WriteLine($"City {city.CityName} already exists.");
                return existingCity;
            }

            // Insert new city
            var query = @"
                INSERT INTO cities (CityName, Longitude, Latitude) 
                VALUES (@CityName, @Longitude, @Latitude);
                SELECT LAST_INSERT_ID();";
            
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityName", city.CityName);
            command.Parameters.AddWithValue("@Latitude", city.Latitude);
            command.Parameters.AddWithValue("@Longitude", city.Longitude);

            var newId = await command.ExecuteScalarAsync();
            var addedCity = await GetCity(newId.ToString());

            if (addedCity == null)
                throw new Exception("Internal error: City not found after adding.");

            Console.WriteLine($"City {city.CityName} added successfully with ID {newId}.");
            return addedCity;
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }

    public async Task<CityInfo> UpdateCity(CityInfo updatedCity)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                UPDATE cities 
                SET CityName = @CityName, Latitude = @Latitude, Longitude = @Longitude 
                WHERE CityId = @CityId;";
            
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityId", updatedCity.CityId);
            command.Parameters.AddWithValue("@CityName", updatedCity.CityName);
            command.Parameters.AddWithValue("@Latitude", updatedCity.Latitude);
            command.Parameters.AddWithValue("@Longitude", updatedCity.Longitude);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                throw new Exception($"City with ID {updatedCity.CityId} not found.");
            }

            Console.WriteLine($"City {updatedCity.CityId} updated successfully.");
            return await GetCity(updatedCity.CityId);
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteCity(string cityId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "DELETE FROM cities WHERE CityId = @CityId";
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityId", cityId);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                Console.WriteLine($"City with ID {cityId} not found.");
            }
            else
            {
                Console.WriteLine($"City {cityId} deleted successfully.");
            }
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }

    public async Task<int> BulkUpsertCitiesAsync(List<SparQLCityInfo> cities)
    {
        int totalAffected = 0;
        const int batchSize = 1000;

        for (int i = 0; i < cities.Count; i += batchSize)
        {
            var batch = cities.Skip(i).Take(batchSize).ToList();
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                
                // INSERT IGNORE will skip duplicates
                command.CommandText = @"
                    INSERT IGNORE INTO cities (CityId, CityName, Latitude, Longitude)
                    VALUES (@CityId, @CityName, @Latitude, @Longitude);";

                var idParam = command.Parameters.Add("@CityId", MySqlDbType.VarChar);
                var nameParam = command.Parameters.Add("@CityName", MySqlDbType.VarChar);
                var latParam = command.Parameters.Add("@Latitude", MySqlDbType.Double);
                var lonParam = command.Parameters.Add("@Longitude", MySqlDbType.Double);

                foreach (var city in batch)
                {
                    idParam.Value = city.WikidataId;
                    nameParam.Value = city.CityName;
                    latParam.Value = city.Latitude;
                    lonParam.Value = city.Longitude;
                    totalAffected += await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                Console.WriteLine($"Batch complete: {i + batch.Count}/{cities.Count} processed. {totalAffected} inserted.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error in batch: {ex.Message}");
                throw;
            }
        }

        Console.WriteLine($"Bulk upsert complete. Total {totalAffected} new cities inserted.");
        return totalAffected;
    }

    public async Task<DateTime> GetLastSyncAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT LastSync FROM sync_state WHERE SyncKey = 'CitySync'";

            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
            {
                Console.WriteLine("No last sync found, defaulting to 2000-01-01.");
                return new DateTime(2000, 1, 1);
            }

            return (DateTime)result;
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
            // Return default if table doesn't exist yet
            return new DateTime(2000, 1, 1);
        }
    }

    public async Task UpdateLastSyncAsync(DateTime newTimestamp)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO sync_state (SyncKey, LastSync)
                VALUES ('CitySync', @ts)
                ON DUPLICATE KEY UPDATE LastSync = @ts;";

            cmd.Parameters.AddWithValue("@ts", newTimestamp);

            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"Last sync updated to {newTimestamp:yyyy-MM-dd HH:mm:ss}");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
            throw;
        }
    }

    // Private helper method
    private async Task<CityInfo?> GetCityByName(string cityName, MySqlConnection connection)
    {
        try
        {
            var query = "SELECT * FROM cities WHERE LOWER(CityName) = @CityName LIMIT 1;";
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityName", cityName.ToLower());

            using var reader = await command.ExecuteReaderAsync();
            if (reader.Read())
            {
                return new CityInfo
                {
                    CityId = reader.GetString(reader.GetOrdinal("CityId")),
                    CityName = reader.GetString(reader.GetOrdinal("CityName")),
                    Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                    Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetCityByName: {ex.Message}");
            return null;
        }
    }
}
