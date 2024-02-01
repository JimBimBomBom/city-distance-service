// function that validates data for my CityInfo class in CityDistanceService/src/CitiesDistanceRequest.cs, we should check:
// 1. CityName is string, and is not null or empty
// 2. Latitude is double, and is within the range of -90 to 90
// 3. Longitude is double, and is within the range of -180 to 180

public class DataValidation
{
    public static bool ValidateCityInfo(CityInfo cityInfo)
    {
        if (string.IsNullOrWhiteSpace(cityInfo.CityName))
            return false;
        if (cityInfo.Latitude < -90 || cityInfo.Latitude > 90)
            return false;
        if (cityInfo.Longitude < -180 || cityInfo.Longitude > 180)
            return false;
        return true;
    }
}