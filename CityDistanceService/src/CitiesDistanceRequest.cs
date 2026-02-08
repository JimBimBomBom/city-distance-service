using Elastic.Clients.Elasticsearch;
using FluentValidation;

public class CityId
{
    public string Id { get; set; }
}

public class CityDoc
{
    public string CityId { get; set; }
    public List<string> CityNames { get; set; } = new();  // Multilingual names
    public GeoLocation Location { get; set; }
    
    // Metadata for display and filtering
    public string CountryCode { get; set; }
    public string Country { get; set; }
    public string AdminRegion { get; set; }
    public int? Population { get; set; }
}

public class SparQLCityInfo
{
    public string WikidataId { get; set; }
    public string CityName { get; set; }
    public List<string> AllNames { get; set; } = new();
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    // Metadata fields - 
    public string Country { get; set; }
    public string CountryCode { get; set; }
    public string AdminRegion { get; set; }
    public int? Population { get; set; }
}

public class CityInfo
{
    public string CityId { get; set; }
    public string CityName { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    // Metadata fields
    public string CountryCode { get; set; }  // e.g., "GB", "US"
    public string Country { get; set; }       // e.g., "United Kingdom"
    public string AdminRegion { get; set; }   // e.g., "England", "Texas"
    public int? Population { get; set; }
}

public class NewCityInfo
{
    public string CityName { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    // Metadata fields
    public string CountryCode { get; set; }
    public string Country { get; set; }
    public string AdminRegion { get; set; }
    public int? Population { get; set; }
}

public class CitySuggestion
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string CountryCode { get; set; }
    public string Country { get; set; }
    public string AdminRegion { get; set; }
    public int? Population { get; set; }
    public string Flag => GetCountryFlag(CountryCode);

    private static string GetCountryFlag(string countryCode)
    {
        if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2)
            return "";
        
        return string.Concat(countryCode.ToUpper().Select(c => 
            char.ConvertFromUtf32(c + 0x1F1A5)));
    }
}

public class Coordinates
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class CitiesDistanceRequest
{
    public string City1Name { get; set; }
    public string City2Name { get; set; }
}

// API Response wrapper class
public class ApiResponse<T>
{
    public T Data { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }

    public ApiResponse(T data, string message)
    {
        Data = data;
        Message = message;
        Timestamp = DateTime.UtcNow;
    }
}
