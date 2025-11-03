using Microsoft.Data.SqlClient;
using MySqlConnector;

public class MySQLManager : IDatabaseManager
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
        }
        catch (SqlException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return Results.BadRequest($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return Results.BadRequest($"Error: {ex.Message}");
        }

        return Results.Ok(_connectionString);
    }

    public async Task<List<string>> GetCityNames()
    {
        var cityNames = new List<string>();

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT CityName FROM cities;";
            using var command = new MySqlCommand(query, connection);

            using var reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                cityNames.Add(reader.GetString(reader.GetOrdinal("CityName")));
            }

            Console.WriteLine("Product fetched successfully.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        return cityNames;
    }

    public async Task<bool> AddCityNoReturn(NewCityInfo city)
    {
        bool success = false;
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                INSERT IGNORE INTO cities (CityName, Longitude, Latitude) 
                VALUES (@CityName, @Longitude, @Latitude);";
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityName", city.CityName);
            command.Parameters.AddWithValue("@Latitude", city.Latitude);
            command.Parameters.AddWithValue("@Longitude", city.Longitude);
            
            int rowsAffected = await command.ExecuteNonQueryAsync();
            success = rowsAffected > 0;
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"MySQL Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General Error: {ex.Message}");
        }

        return success;
    }

    public async Task<CityInfo?> AddCity(NewCityInfo city)
    {
        CityInfo? addedCity = null;
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            if (await GetCity(city.CityName) == null) // If city already exists we still want to return it from the database
            {
                var query = "INSERT INTO cities (CityName, Longitude, Latitude) VALUES (@CityName, @Longitude, @Latitude);";
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@CityName", city.CityName);
                command.Parameters.AddWithValue("@Latitude", city.Latitude);
                command.Parameters.AddWithValue("@Longitude", city.Longitude);
                await command.ExecuteScalarAsync();
            }

            addedCity = await GetCity(city.CityName);
            if (addedCity == null)
            {
                throw new Exception("Internal error: City not found after adding.");
            }

            Console.WriteLine("Product added successfully.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"Error add1: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error add2: {ex.Message}");
        }

        return addedCity;
    }

    public async Task<CityInfo?> GetCity(Guid cityId)
    {
        CityInfo result = null;
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = "SELECT * FROM cities WHERE CityId = @CityId;";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CityId", cityId);

                    using var reader = await command.ExecuteReaderAsync();
                    if (reader.Read())
                    {
                        result = new CityInfo
                        {
                            CityId = reader.GetGuid(reader.GetOrdinal("CityId")),
                            CityName = reader.GetString(reader.GetOrdinal("CityName")),
                            Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                            Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                        };
                    }
                }
            }

            if (result == null)
            {
                Console.WriteLine("City not found.");   
            }
            Console.WriteLine("Product fetched successfully.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"Error here: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error there: {ex.Message}");
        }

        return result;
    }

    public async Task<CityInfo?> GetCity(string cityName)
    {
        CityInfo result = null;
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT * FROM cities WHERE LOWER(CityName) = @CityName;";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityName", cityName.ToLower());
            using var reader = await command.ExecuteReaderAsync();
            if (reader.Read())
            {
                result = new CityInfo
                {
                    CityId = reader.GetGuid(reader.GetOrdinal("CityId")),
                    CityName = reader.GetString(reader.GetOrdinal("CityName")),
                    Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                    Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                };
            }

            Console.WriteLine("Product fetched successfully.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"Error here: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error there: {ex.Message}");
        }

        return result;
    }

    public async Task<Coordinates?> GetCityCoordinates(string cityName)
    {
        Coordinates result = null;
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT * FROM cities WHERE LOWER(CityName) = @CityName;";
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityName", cityName.ToLower());

            using var reader = await command.ExecuteReaderAsync();
            if (reader.Read())
            {
                result = new Coordinates
                {
                    Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                    Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                };
            }

            Console.WriteLine("Product fetched successfully.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"Error here: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error there: {ex.Message}");
        }

        return result;
    }

    public async Task<List<CityInfo>> GetCities(List<string> cityNames)
    {
        var cities = new List<CityInfo>();

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var parameters = cityNames.Select((name, index) => $"@CityName{index}").ToList();
            var query = $"SELECT * FROM cities WHERE CityName IN ({string.Join(",", parameters)});";
            using var command = new MySqlCommand(query, connection);

            for (int i = 0; i < cityNames.Count; i++)
            {
                command.Parameters.AddWithValue($"@CityName{i}", cityNames[i]);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                var city = new CityInfo
                {
                    CityId = reader.GetGuid(reader.GetOrdinal("CityId")),
                    CityName = reader.GetString(reader.GetOrdinal("CityName")),
                    Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                    Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                };
                cities.Add(city);
            }

            Console.WriteLine("Product fetched successfully.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        return cities;
    }

    public async Task<List<CityInfo>> GetCities(string cityNameContains)
    {
        var cities = new List<CityInfo>();

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = "SELECT * FROM cities WHERE LOWER(CityName) = @CityName";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CityName", cityNameContains.ToLower());

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            var city = new CityInfo
                            {
                                CityId = reader.GetGuid(reader.GetOrdinal("CityId")),
                                CityName = reader.GetString(reader.GetOrdinal("CityName")),
                                Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                                Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                            };
                            cities.Add(city);
                        }
                    }
                }
            }
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        return cities;
    }

    public async Task<CityInfo> UpdateCity(CityInfo updatedCity)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "UPDATE cities SET CityName = @CityName, Latitude = @Latitude, Longitude = @Longitude WHERE CityId = @CityId;";
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityId", updatedCity.CityId);
            command.Parameters.AddWithValue("@CityName", updatedCity.CityName);
            command.Parameters.AddWithValue("@Latitude", updatedCity.Latitude);
            command.Parameters.AddWithValue("@Longitude", updatedCity.Longitude);

            await command.ExecuteNonQueryAsync();

            Console.WriteLine("Product modified successfully.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        return await GetCity(updatedCity.CityId);
    }

    public async Task DeleteCity(Guid cityId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "DELETE FROM cities WHERE CityId = @CityId";
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@CityId", cityId);

            await command.ExecuteNonQueryAsync();

            Console.WriteLine("Product deleted successfully.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    public async Task<int> BulkInsertCitiesAsync(List<NewCityInfo> cities)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT IGNORE INTO cities (CityName, Latitude, Longitude)
                VALUES (@CityName, @Latitude, @Longitude)";

            var nameParam = command.Parameters.Add("@CityName", MySqlDbType.VarChar);
            var latParam = command.Parameters.Add("@Latitude", MySqlDbType.Double);
            var lonParam = command.Parameters.Add("@Longitude", MySqlDbType.Double);

            int insertedCount = 0;

            foreach (var city in cities)
            {
                try
                {
                    nameParam.Value = city.CityName;
                    latParam.Value = city.Latitude;
                    lonParam.Value = city.Longitude;

                    insertedCount += await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to insert city: {city.CityName}. Error: {ex.Message}");
                    // Continue with the next city
                }
            }

            await transaction.CommitAsync();
            return insertedCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"Error in bulk insert operation: {ex.Message}");
            throw;
        }
    }
}
