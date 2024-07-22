using System.ComponentModel.DataAnnotations;

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

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}

public class CityId
{
    public int Id { get; set; }
}

public class CityName
{
    public string Name { get; set; }
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
