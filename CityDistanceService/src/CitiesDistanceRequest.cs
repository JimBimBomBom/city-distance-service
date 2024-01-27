using System.ComponentModel.DataAnnotations;
public class GeocodeApiResponse
{
    public double Lat { get; set; }
    public double Lon { get; set; }
    // Add other fields from the API response as needed
}

public class CitiesDistanceRequest
{
    public string? City1 { get; set; }
    public string? City2 { get; set; }
}

public class Coordinates
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class CityInfo
{
    public int CityId { get; set; }
    public string CityName { get; set; }
    // public string Country { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class ApiResponse<T>
{
    public T Data { get; set; }
    public string Message { get; set; }

    public ApiResponse(T data, string message)
    {
        Data = data;
        Message = message;
    }
}
