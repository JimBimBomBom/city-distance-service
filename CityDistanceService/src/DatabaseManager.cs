using MySql.Data.MySqlClient;
using System;

// TODO - refactor to allow operations on any table, with any parameters (with checks)
public class DatabaseManager
{
    // private readonly string _connectionString;

    // public DatabaseManager()
    // {
    //     _connectionString = "Server=localhost;Database=citydatabase;Uid=root;Pwd=Popkorn123;";
    // }

    public async Task<CityInfo> AddCityAsync(CityInfo city)
    {
        try
        {
            using (var connection = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQLCONNSTR_localdb")))
            {
                await connection.OpenAsync();

                var query = "INSERT INTO cities (CityName, Longitude, Latitude) VALUES (@CityName, @Longitude, @Latitude);  SELECT LAST_INSERT_ID();";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CityName", city.CityName);
                    command.Parameters.AddWithValue("@Latitude", city.Latitude);
                    command.Parameters.AddWithValue("@Longitude", city.Longitude);

                    // command.ExecuteNonQuery();
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
            // Handle any MySQL database related errors
            Console.WriteLine($"Error add1: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Handle all other types of errors
            Console.WriteLine($"Error add2: {ex.Message}");
        }

        return city;
    }

    public async Task<CityInfo> GetCityInfo(int cityId)
    {
        CityInfo result = null;
        try
        {
            using (var connection = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQLCONNSTR_localdb")))
            {
                await connection.OpenAsync();

                var query = "SELECT * FROM cities WHERE CityId = @CityId;";
                using (var command = new MySqlCommand(query, connection))
                {
                    // Use parameterized queries to prevent SQL injection
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
                                Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude"))
                            };
                        }
                    }
                }
            }

            Console.WriteLine("Product fetched successfully.");
        }
        catch (MySqlException ex)
        {
            // Handle any MySQL database related errors
            Console.WriteLine($"Error here: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Handle all other types of errors
            Console.WriteLine($"Error there: {ex.Message}");
        }

        return result;
    }

    public async Task<CityInfo> GetCityInfo(string cityName)
    {
        CityInfo result = null;
        try
        {
            using (var connection = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQLCONNSTR_localdb")))
            {
                await connection.OpenAsync();

                var query = "SELECT * FROM cities WHERE LOWER(CityName) = @CityName;";
                using (var command = new MySqlCommand(query, connection))
                {
                    // Use parameterized queries to prevent SQL injection
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
                                Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude"))
                            };
                        }
                    }
                }
            }

            Console.WriteLine("Product fetched successfully.");
        }
        catch (MySqlException ex)
        {
            // Handle any MySQL database related errors
            Console.WriteLine($"Error here: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Handle all other types of errors
            Console.WriteLine($"Error there: {ex.Message}");
        }

        return result;
    }

    public async Task<Coordinates> GetCityCoordinates(string cityName)
    {
        Coordinates result = null;

        try
        {
            using (var connection = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQLCONNSTR_localdb")))
            {
                connection.OpenAsync();

                var query = "SELECT * FROM cities WHERE LOWER(CityName) = @CityName;";
                using (var command = new MySqlCommand(query, connection))
                {
                    // Use parameterized queries to prevent SQL injection
                    command.Parameters.AddWithValue("@CityName", cityName.ToLower());

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            result = new Coordinates
                            {
                                Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
                                Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude"))
                            };
                        }
                    }
                }
            }

            Console.WriteLine("Product fetched successfully.");
        }
        catch (MySqlException ex)
        {
            // Handle any MySQL database related errors
            Console.WriteLine($"Error here: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Handle all other types of errors
            Console.WriteLine($"Error there: {ex.Message}");
        }

        return result;
    }

    public async Task<List<CityInfo>> GetCitiesByNameAsync(string cityName)
    {
        var cities = new List<CityInfo>();

        using (var connection = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQLCONNSTR_localdb")))
        {
            await connection.OpenAsync();

            var query = "SELECT * FROM Cities WHERE LOWER(CityName) = @CityName";
            using (var command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@CityName", cityName.ToLower());

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        var city = new CityInfo
                        {
                            CityId = reader.GetInt32(reader.GetOrdinal("CityId")), // Get integer value
                            CityName = reader.GetString(reader.GetOrdinal("CityName")), // Get string value
                            Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")), // Get decimal value and cast to double
                            Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")) // Get decimal value and cast to double
                        };
                        cities.Add(city);
                    }
                }
            }
        }

        return cities;
    }

    public async Task<CityInfo> ModifyItemAsync(CityInfo updatedCity)
    {
        try
        {
            using (var connection = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQLCONNSTR_localdb")))
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

            Console.WriteLine("Product modified successfully.");
        }
        catch (MySqlException ex)
        {
            // Handle any MySQL database related errors
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Handle all other types of errors
            Console.WriteLine($"Error: {ex.Message}");
        }

        return await GetCityInfo(updatedCity.CityId);
    }

    public async Task DeleteItem(int cityId)
    {
        try
        {
            using (var connection = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQLCONNSTR_localdb")))
            {
                await connection.OpenAsync();

                var query = "DELETE FROM cities WHERE CityId = @CityId";
                using (var command = new MySqlCommand(query, connection))
                {
                    // Use parameterized queries to prevent SQL injection
                    command.Parameters.AddWithValue("@CityId", cityId);

                    await command.ExecuteNonQueryAsync();
                }
            }

            Console.WriteLine("Product deleted successfully.");
        }
        catch (MySqlException ex)
        {
            // Handle any MySQL database related errors
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Handle all other types of errors
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
