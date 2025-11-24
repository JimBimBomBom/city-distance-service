using Microsoft.Data.SqlClient;
using MySqlConnector;
using System.Text.Json;

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

    // public async Task<List<string>> GetCityNames()
    // {
    //     var cityNames = new List<string>();

    //     try
    //     {
    //         using var connection = new MySqlConnection(_connectionString);
    //         await connection.OpenAsync();

    //         var query = "SELECT CityName FROM cities;";
    //         using var command = new MySqlCommand(query, connection);

    //         using var reader = await command.ExecuteReaderAsync();
    //         while (reader.Read())
    //         {
    //             cityNames.Add(reader.GetString(reader.GetOrdinal("CityName")));
    //         }

    //         Console.WriteLine("Product fetched successfully.");
    //     }
    //     catch (MySqlException ex)
    //     {
    //         Console.WriteLine($"Error: {ex.Message}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error: {ex.Message}");
    //     }

    //     return cityNames;
    // }

    // public async Task<bool> AddCityNoReturn(NewCityInfo city)
    // {
    //     bool success = false;
    //     try
    //     {
    //         using var connection = new MySqlConnection(_connectionString);
    //         await connection.OpenAsync();

    //         var query = @"
    //             INSERT IGNORE INTO cities (CityName, Longitude, Latitude) 
    //             VALUES (@CityName, @Longitude, @Latitude);";
    //         using var command = new MySqlCommand(query, connection);
    //         command.Parameters.AddWithValue("@CityName", city.CityName);
    //         command.Parameters.AddWithValue("@Latitude", city.Latitude);
    //         command.Parameters.AddWithValue("@Longitude", city.Longitude);

    //         int rowsAffected = await command.ExecuteNonQueryAsync();
    //         success = rowsAffected > 0;
    //     }
    //     catch (MySqlException ex)
    //     {
    //         Console.WriteLine($"MySQL Error: {ex.Message}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"General Error: {ex.Message}");
    //     }

    //     return success;
    // }

    // public async Task<CityInfo?> AddCity(NewCityInfo city)
    // {
    //     CityInfo? addedCity = null;
    //     try
    //     {
    //         using var connection = new MySqlConnection(_connectionString);
    //         await connection.OpenAsync();

    //         if (await GetCity(city.CityName) == null) // If city already exists we still want to return it from the database
    //         {
    //             var query = "INSERT INTO cities (CityName, Longitude, Latitude) VALUES (@CityName, @Longitude, @Latitude);";
    //             using var command = new MySqlCommand(query, connection);
    //             command.Parameters.AddWithValue("@CityName", city.CityName);
    //             command.Parameters.AddWithValue("@Latitude", city.Latitude);
    //             command.Parameters.AddWithValue("@Longitude", city.Longitude);
    //             await command.ExecuteScalarAsync();
    //         }

    //         addedCity = await GetCity(city.CityName);
    //         if (addedCity == null)
    //         {
    //             throw new Exception("Internal error: City not found after adding.");
    //         }

    //         Console.WriteLine("Product added successfully.");
    //     }
    //     catch (MySqlException ex)
    //     {
    //         Console.WriteLine($"Error add1: {ex.Message}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error add2: {ex.Message}");
    //     }

    //     return addedCity;
    // }

    // public async Task<CityInfo?> GetCity(string cityId)
    // {
    //     CityInfo result = null;
    //     try
    //     {
    //         using (var connection = new MySqlConnection(_connectionString))
    //         {
    //             await connection.OpenAsync();

    //             var query = "SELECT * FROM cities WHERE CityId = @CityId;";
    //             using (var command = new MySqlCommand(query, connection))
    //             {
    //                 command.Parameters.AddWithValue("@CityId", cityId);

    //                 using var reader = await command.ExecuteReaderAsync();
    //                 if (reader.Read())
    //                 {
    //                     result = new CityInfo
    //                     {
    //                         CityId = reader.GetString(reader.GetOrdinal("CityId")),
    //                         CityName = reader.GetString(reader.GetOrdinal("CityName")),
    //                         Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
    //                         Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
    //                     };
    //                 }
    //             }
    //         }

    //         if (result == null)
    //         {
    //             Console.WriteLine("City not found.");
    //         }

    //         Console.WriteLine("Product fetched successfully.");
    //     }
    //     catch (MySqlException ex)
    //     {
    //         Console.WriteLine($"Error here: {ex.Message}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error there: {ex.Message}");
    //     }

    //     return result;
    // }

    // public async Task<Coordinates?> GetCityCoordinates(string cityName)
    // {
    //     Coordinates result = null;
    //     try
    //     {
    //         using var connection = new MySqlConnection(_connectionString);
    //         await connection.OpenAsync();

    //         var query = "SELECT * FROM cities WHERE LOWER(CityName) = @CityName;";
    //         using var command = new MySqlCommand(query, connection);
    //         command.Parameters.AddWithValue("@CityName", cityName.ToLower());

    //         using var reader = await command.ExecuteReaderAsync();
    //         if (reader.Read())
    //         {
    //             result = new Coordinates
    //             {
    //                 Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
    //                 Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
    //             };
    //         }

    //         Console.WriteLine("Product fetched successfully.");
    //     }
    //     catch (MySqlException ex)
    //     {
    //         Console.WriteLine($"Error here: {ex.Message}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error there: {ex.Message}");
    //     }

    //     return result;
    // }

    // public async Task<List<CityInfo>> GetCities(List<string> cityNames)
    // {
    //     var cities = new List<CityInfo>();

    //     try
    //     {
    //         using var connection = new MySqlConnection(_connectionString);
    //         await connection.OpenAsync();

    //         var parameters = cityNames.Select((name, index) => $"@CityName{index}").ToList();
    //         var query = $"SELECT * FROM cities WHERE CityName IN ({string.Join(",", parameters)});";
    //         using var command = new MySqlCommand(query, connection);

    //         for (int i = 0; i < cityNames.Count; i++)
    //         {
    //             command.Parameters.AddWithValue($"@CityName{i}", cityNames[i]);
    //         }

    //         using var reader = await command.ExecuteReaderAsync();
    //         while (reader.Read())
    //         {
    //             var city = new CityInfo
    //             {
    //                 CityId = reader.GetString(reader.GetOrdinal("CityId")),
    //                 CityName = reader.GetString(reader.GetOrdinal("CityName")),
    //                 Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
    //                 Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
    //             };
    //             cities.Add(city);
    //         }

    //         Console.WriteLine("Product fetched successfully.");
    //     }
    //     catch (MySqlException ex)
    //     {
    //         Console.WriteLine($"Error: {ex.Message}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error: {ex.Message}");
    //     }

    //     return cities;
    // }

    // public async Task<List<CityInfo>> GetCities(string cityNameContains)
    // {
    //     var cities = new List<CityInfo>();

    //     try
    //     {
    //         using (var connection = new MySqlConnection(_connectionString))
    //         {
    //             await connection.OpenAsync();

    //             var query = "SELECT * FROM cities WHERE LOWER(CityName) = @CityName";
    //             using (var command = new MySqlCommand(query, connection))
    //             {
    //                 command.Parameters.AddWithValue("@CityName", cityNameContains.ToLower());

    //                 using (var reader = await command.ExecuteReaderAsync())
    //                 {
    //                     while (reader.Read())
    //                     {
    //                         var city = new CityInfo
    //                         {
    //                             CityId = reader.GetString(reader.GetOrdinal("CityId")),
    //                             CityName = reader.GetString(reader.GetOrdinal("CityName")),
    //                             Latitude = (double)reader.GetDecimal(reader.GetOrdinal("Latitude")),
    //                             Longitude = (double)reader.GetDecimal(reader.GetOrdinal("Longitude")),
    //                         };
    //                         cities.Add(city);
    //                     }
    //                 }
    //             }
    //         }
    //     }
    //     catch (MySqlException ex)
    //     {
    //         Console.WriteLine($"Error: {ex.Message}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error: {ex.Message}");
    //     }

    //     return cities;
    // }

    // public async Task<CityInfo> UpdateCity(CityInfo updatedCity)
    // {
    //     try
    //     {
    //         using var connection = new MySqlConnection(_connectionString);
    //         await connection.OpenAsync();

    //         var query = "UPDATE cities SET CityName = @CityName, Latitude = @Latitude, Longitude = @Longitude WHERE CityId = @CityId;";
    //         using var command = new MySqlCommand(query, connection);
    //         command.Parameters.AddWithValue("@CityId", updatedCity.CityId);
    //         command.Parameters.AddWithValue("@CityName", updatedCity.CityName);
    //         command.Parameters.AddWithValue("@Latitude", updatedCity.Latitude);
    //         command.Parameters.AddWithValue("@Longitude", updatedCity.Longitude);

    //         await command.ExecuteNonQueryAsync();

    //         Console.WriteLine("Product modified successfully.");
    //     }
    //     catch (MySqlException ex)
    //     {
    //         Console.WriteLine($"Error: {ex.Message}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error: {ex.Message}");
    //     }

    //     return await GetCity(updatedCity.CityId);
    // }

    // public async Task DeleteCity(string cityId)
    // {
    //     try
    //     {
    //         using var connection = new MySqlConnection(_connectionString);
    //         await connection.OpenAsync();

    //         var query = "DELETE FROM cities WHERE CityId = @CityId";
    //         using var command = new MySqlCommand(query, connection);
    //         command.Parameters.AddWithValue("@CityId", cityId);

    //         await command.ExecuteNonQueryAsync();

    //         Console.WriteLine("Product deleted successfully.");
    //     }
    //     catch (MySqlException ex)
    //     {
    //         Console.WriteLine($"Error: {ex.Message}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error: {ex.Message}");
    //     }
    // }

    public async Task<DateTime> GetLastSyncAsync()
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

    public async Task UpdateLastSyncAsync(DateTime newTimestamp)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE sync_state
            SET LastSync = @ts
            WHERE SyncKey = 'CitySync';
        ";

        cmd.Parameters.AddWithValue("@ts", newTimestamp);

        await cmd.ExecuteNonQueryAsync();
    }

    // private static async Task<string> ExecuteSparqlAsync(string query)
    // {
    //     using var client = new HttpClient();

    //     client.DefaultRequestHeaders.UserAgent.ParseAdd(
    //         "CityDistanceService/1.0 (https://yourdomain.example; your-email@example.com)"
    //     );
    //     client.DefaultRequestHeaders.Accept.ParseAdd("application/sparql-results+json");

    //     var content = new FormUrlEncodedContent(new Dictionary<string, string>
    //     {
    //         ["query"] = query
    //     });

    //     var response = await client.PostAsync("https://query.wikidata.org/sparql", content);
    //     var raw = await response.Content.ReadAsStringAsync();

    //     Console.WriteLine("Wikidata response status: " + response.StatusCode);

    //     if (!response.IsSuccessStatusCode)
    //         throw new Exception($"Wikidata status {response.StatusCode}. Body:\n{raw}");

    //     return raw;
    // }

    // public async Task<List<SparQLCityInfo>> FetchCitiesAsync(DateTime lastSync)
    // {
    //     string lastSyncIso = lastSync.ToString("yyyy-MM-ddTHH:mm:ssZ");

    //     string query = $@"
    //         SELECT ?city ?cityLabel (LANG(?cityLabel) AS ?lang) ?lat ?lon ?modified WHERE {{
    //         ?city wdt:P625 ?coord .
    //         ?city wdt:P31/wdt:P279* wd:Q515 .        # items whose type is a subclass of city
    //         ?city schema:dateModified ?modified .
    //         FILTER(?modified > ""{lastSyncIso}""^^xsd:dateTime)

    //         # Get the label in ALL available languages
    //         ?city rdfs:label ?cityLabel.

    //         BIND(geof:latitude(?coord) AS ?lat)
    //         BIND(geof:longitude(?coord) AS ?lon)
    //         }}
    //     ";

    //     var raw = await ExecuteSparqlAsync(query);

    //     using var json = JsonDocument.Parse(raw);
    //     var bindings = json.RootElement
    //         .GetProperty("results")
    //         .GetProperty("bindings");

    //     int bindingCount = 0;
    //     var intermediateLabels = new List<SparQLCityLabel>();

    //     foreach (var row in bindings.EnumerateArray())
    //     {
    //         // 1. Basic property extraction 
    //         string id = row.GetProperty("city").GetProperty("value").GetString()!.Split('/').Last();
    //         string name = row.GetProperty("cityLabel").GetProperty("value").GetString()!;
    //         double lat = double.Parse(row.GetProperty("lat").GetProperty("value").GetString()!);
    //         double lon = double.Parse(row.GetProperty("lon").GetProperty("value").GetString()!);
    //         DateTime modified = DateTime.Parse(row.GetProperty("modified").GetProperty("value").GetString()!);

    //         // 2. Get the language code
    //         string lang = row.TryGetProperty("lang", out var langProp) &&
    //                     langProp.TryGetProperty("value", out var langVal)
    //                     ? langVal.GetString()!
    //                     : "und"; // 'und' for undefined if language is not set (rare, but safe)
            
    //         intermediateLabels.Add(new SparQLCityLabel
    //         {
    //             WikidataId = id,
    //             CityName = name,
    //             LanguageCode = lang,
    //             Latitude = lat,
    //             Longitude = lon,
    //             Modified = modified
    //         });
    //     }

    //     // 3. Aggregate Labels by CityId
    //     var finalDocuments = intermediateLabels
    //         .GroupBy(label => label.WikidataId)
    //         .Select(group => {
    //             // Take the first item to get coordinates and ID (all items in the group have the same coordinates)
    //             var first = group.First();
                
    //             return new ElasticCityDocument
    //             {
    //                 CityId = first.WikidataId,
    //                 Latitude = first.Latitude,
    //                 Longitude = first.Longitude,
    //                 Location = $"{first.Latitude},{first.Longitude}", // GeoPoint format
    //                 Modified = group.Max(g => g.Modified), // Use the latest modification date across all labels
                    
    //                 // Build the multilingual dictionary: { "en": "New York", "fr": "Nouvelle-York" }
    //                 CityName = group.ToDictionary(
    //                     label => label.LanguageCode,
    //                     label => label.CityName,
    //                     StringComparer.OrdinalIgnoreCase // Case-insensitive key comparison
    //                 )
    //             };
    //         })
    //         .ToList();
        
    //     Console.WriteLine($"SPARQL returned {intermediateLabels.Count} language bindings, aggregated into {finalDocuments.Count} unique cities.");
    //     return finalDocuments;
    // }

    // // Updates cities in MySQL database based on SparQL wiki query information gained from FetchCitiesAsync
    // public async Task<int> SynchronizeElasticSearchAsync()
    // {
    //     var lastSync = await GetLastSyncAsync();
    //     var cities = await FetchCitiesAsync(lastSync);

    //     if (cities.Count == 0)
    //     {
    //         Console.WriteLine("No new or updated cities found.");
    //         return 0;
    //     }

    //     Console.WriteLine($"Fetched {cities.Count} new or updated cities from Wikidata since {lastSync}.");

    //     var upsertedCities = await BulkUpsertCitiesAsync(cities);
    //     Console.WriteLine($"Updated and or inserted {upsertedCities} cities.");

    //     await UpdateLastSyncAsync(DateTime.UtcNow);

    //     return upsertedCities;
    // }

    // public async Task<int> BulkUpsertCitiesAsync(List<SparQLCityInfo> cities)
    // {
    //     using var connection = new MySqlConnection(_connectionString);
    //     await connection.OpenAsync();

    //     using var transaction = await connection.BeginTransactionAsync();

    //     var command = connection.CreateCommand();
    //     command.Transaction = transaction;

    //     command.CommandText = @"
    //         INSERT INTO cities (CityId, CityName, Latitude, Longitude)
    //         VALUES (@CityId, @CityName, @Latitude, @Longitude)
    //         ON DUPLICATE KEY UPDATE
    //             CityName = VALUES(CityName),
    //             Latitude = VALUES(Latitude),
    //             Longitude = VALUES(Longitude);";

    //     var idParam = command.Parameters.Add("@CityId", MySqlDbType.VarChar);
    //     var nameParam = command.Parameters.Add("@CityName", MySqlDbType.VarChar);
    //     var latParam = command.Parameters.Add("@Latitude", MySqlDbType.Double);
    //     var lonParam = command.Parameters.Add("@Longitude", MySqlDbType.Double);

    //     int affected = 0;

    //     try
    //     {
    //         foreach (var city in cities)
    //         {
    //             idParam.Value = city.WikidataId;
    //             nameParam.Value = city.CityName;
    //             latParam.Value = city.Latitude;
    //             lonParam.Value = city.Longitude;

    //             affected += await command.ExecuteNonQueryAsync();
    //         }

    //         await transaction.CommitAsync();
    //     }
    //     catch
    //     {
    //         Console.WriteLine("Error occurred while upserting cities.");
    //         await transaction.RollbackAsync();
    //         throw;
    //     }

    //     return affected;
    // }
}
