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
        CityInfo result = null;
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"SELECT CityId, CityName, Latitude, Longitude, 
                     CountryCode, Country, AdminRegion, Population 
                     FROM cities WHERE CityId = @CityId;";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityId", cityId);

            using var reader = await command.ExecuteReaderAsync();
            if (reader.Read())
            {
                result = new CityInfo
                {
                    CityId = reader.GetString(reader.GetOrdinal("CityId")),
                    CityName = reader.GetString(reader.GetOrdinal("CityName")),
                    Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                    Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                    CountryCode = reader.IsDBNull(reader.GetOrdinal("CountryCode"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("CountryCode")),
                    Country = reader.IsDBNull(reader.GetOrdinal("Country"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Country")),
                    AdminRegion = reader.IsDBNull(reader.GetOrdinal("AdminRegion"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("AdminRegion")),
                    Population = reader.IsDBNull(reader.GetOrdinal("Population"))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal("Population"))
                };
            }

            if (result != null)
            {
                Console.WriteLine($"City fetched: {result.CityName}, {result.Country}");
            }
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        return result;
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

                // INSERT IGNORE only adds new cities, doesn't update existing
                // Use ON DUPLICATE KEY UPDATE if you want to update metadata
                command.CommandText = @"
                    INSERT INTO cities 
                    (CityId, CityName, Latitude, Longitude, CountryCode, Country, AdminRegion, Population)
                    VALUES (@CityId, @CityName, @Latitude, @Longitude, @CountryCode, @Country, @AdminRegion, @Population)
                    ON DUPLICATE KEY UPDATE
                        CountryCode = VALUES(CountryCode),
                        Country = VALUES(Country),
                        AdminRegion = VALUES(AdminRegion),
                        Population = VALUES(Population);";

                var idParam = command.Parameters.Add("@CityId", MySqlDbType.VarChar);
                var nameParam = command.Parameters.Add("@CityName", MySqlDbType.VarChar);
                var latParam = command.Parameters.Add("@Latitude", MySqlDbType.Double);
                var lonParam = command.Parameters.Add("@Longitude", MySqlDbType.Double);
                var countryCodeParam = command.Parameters.Add("@CountryCode", MySqlDbType.VarChar);
                var countryParam = command.Parameters.Add("@Country", MySqlDbType.VarChar);
                var adminParam = command.Parameters.Add("@AdminRegion", MySqlDbType.VarChar);
                var popParam = command.Parameters.Add("@Population", MySqlDbType.Int32);

                foreach (var city in batch)
                {
                    idParam.Value = city.WikidataId;
                    nameParam.Value = city.CityName;
                    latParam.Value = city.Latitude;
                    lonParam.Value = city.Longitude;
                    countryCodeParam.Value = (object)city.CountryCode ?? DBNull.Value;
                    countryParam.Value = (object)city.Country ?? DBNull.Value;
                    adminParam.Value = (object)city.AdminRegion ?? DBNull.Value;
                    popParam.Value = city.Population.HasValue ? (object)city.Population.Value : DBNull.Value;

                    totalAffected += await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                Console.WriteLine($"MySQL Batch: {i + batch.Count}/{cities.Count} processed.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"MySQL batch error: {ex.Message}");
                throw;
            }
        }
        return totalAffected;
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
