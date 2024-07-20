using MySql.Data.MySqlClient;
using System;

public class MySQLManager : IDatabaseManager
{
    private readonly string _connectionString;

    public MySQLManager(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string> TestConnection()
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                Console.WriteLine("Connection successful.");
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

        return _connectionString;
    }

    public async Task<CityInfo> AddCity(CityInfo city)
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = "INSERT INTO cities (CityName, Longitude, Latitude) VALUES (@CityName, @Longitude, @Latitude);  SELECT LAST_INSERT_ID();";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CityName", city.CityName);
                    command.Parameters.AddWithValue("@Latitude", city.Latitude);
                    command.Parameters.AddWithValue("@Longitude", city.Longitude);

                    var cityId = await command.ExecuteScalarAsync();
                    if (cityId != null)
                    {
                        city.CityId = Convert.ToInt32(cityId);
                    }
                }
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

        return city;
    }

    public async Task<CityInfo> GetCity(int cityId)
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

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            result = new CityInfo
                            {
                                CityId = reader.GetInt32(reader.GetOrdinal("CityId")),
                                CityName = reader.GetString(reader.GetOrdinal("CityName")),
                                Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                                Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                            };
                        }
                    }
                }
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

    public async Task<CityInfo> GetCity(string cityName)
    {
        CityInfo result = null;
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = "SELECT * FROM cities WHERE LOWER(CityName) = @CityName;";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CityName", cityName.ToLower());

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            result = new CityInfo
                            {
                                CityId = reader.GetInt32(reader.GetOrdinal("CityId")),
                                CityName = reader.GetString(reader.GetOrdinal("CityName")),
                                Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                                Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                            };
                        }
                    }
                }
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
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = "SELECT * FROM cities WHERE LOWER(CityName) = @CityName;";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CityName", cityName.ToLower());

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            result = new Coordinates
                            {
                                Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                                Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                            };
                        }
                    }
                }

                Console.WriteLine(cityName + "fetched successfully.");
            }
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

    public async Task<List<CityInfo>> GetCities(string cityNameContains)
    {
        var cities = new List<CityInfo>();

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
                            CityId = reader.GetInt32(reader.GetOrdinal("CityId")),
                            CityName = reader.GetString(reader.GetOrdinal("CityName")),
                            Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                            Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
                        };
                        cities.Add(city);
                    }
                }
            }
        }

        return cities;
    }

    public async Task<CityInfo> UpdateCity(CityInfo updatedCity)
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = "UPDATE cities SET CityName = @CityName, Latitude = @Latitude, Longitude = @Longitude WHERE CityId = @CityId;";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CityId", updatedCity.CityId);
                    command.Parameters.AddWithValue("@CityName", updatedCity.CityName);
                    command.Parameters.AddWithValue("@Latitude", updatedCity.Latitude);
                    command.Parameters.AddWithValue("@Longitude", updatedCity.Longitude);

                    await command.ExecuteNonQueryAsync();
                }
            }

            Console.WriteLine(updatedCity.CityName + "modified successfully.");
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

    public async Task DeleteCity(int cityId)
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = "DELETE FROM cities WHERE CityId = @CityId";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CityId", cityId);

                    await command.ExecuteNonQueryAsync();
                }
            }

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
}
