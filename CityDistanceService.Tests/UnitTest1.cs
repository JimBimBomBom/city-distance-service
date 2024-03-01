namespace CityDistanceService.Tests;

using Xunit;

public class UnitTest1
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 90, 0, 10007.54339852944)]
    [InlineData(0, 0, 0, 90, 10007.54339852944)]
    [InlineData(0, 0, 90, 90, 10007.54339852944)]
    [InlineData(0, 0, 180, 0, 20015.08679605888)]
    [InlineData(0, 0, 0, 180, 20015.08679605888)]
    public void CalculateGreatCircleDistance_UnitTests(double lat1, double lon1, double lat2, double lon2, double expected)
    {
        var coord1 = new Coordinates { Latitude = lat1, Longitude = lon1 };
        var coord2 = new Coordinates { Latitude = lat2, Longitude = lon2 };
        var actual = DistanceCalculationService.CalculateGreatCircleDistance(coord1, coord2);
        Assert.Equal(Math.Round(expected, 5), Math.Round(actual, 5));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(180, Math.PI)]
    [InlineData(360, 2 * Math.PI)]
    public void ToRadians_UnitTest(double degrees, double expected)
    {
        var actual = DistanceCalculationService.ToRadians(degrees);
        Assert.Equal(expected, actual);
    }
}