using Xunit;
using FluentValidation;

namespace CityDistanceService.Tests;
public class DistanceCalculationServiceTests
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
        var actual = DistanceCalculationService.CalculateHaversineDistance(coord1, coord2);
        Assert.Equal(Math.Round(expected, 5), Math.Round(actual, 5));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 90, 0, 10007.54339852944)]
    [InlineData(0, 0, 0, 90, 10007.54339852944)]
    [InlineData(0, 0, 90, 90, 10007.54339852944)]
    [InlineData(0, 0, 180, 0, 20015.08679605888)]
    public void CalculateGreatCircleDistance_UnitTests2(double lat1, double lon1, double lat2, double lon2, double expected)
    {
        var coord1 = new Coordinates { Latitude = lat1, Longitude = lon1 };
        var coord2 = new Coordinates { Latitude = lat2, Longitude = lon2 };
        var actual = DistanceCalculationService.CalculateHaversineDistance(coord1, coord2);
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

public class StringValidatorTests
{
    private readonly StringValidator _validator = new StringValidator();

    [Theory]
    [InlineData("Tokyo")]
    [InlineData("New York")]
    [InlineData("タシケント")]
    [InlineData("ベンガルール")]
    [InlineData("北京")]
    [InlineData("Москва")]
    [InlineData("القاهرة")]
    [InlineData("São Paulo")]
    public void Should_Validate_International_City_Names(string cityName)
    {
        var result = _validator.Validate(cityName);
        Assert.True(result.IsValid, $"Expected '{cityName}' to be valid but got: {string.Join(", ", result.Errors.Select(e => e.ErrorMessage))}");
    }

    [Theory]
    [InlineData("<script>")]
    [InlineData("test!@#")]
    [InlineData("hello'world")]
    [InlineData("test%injection")]
    public void Should_Reject_Invalid_Characters(string input)
    {
        var result = _validator.Validate(input);
        Assert.False(result.IsValid);
    }
}

public class FakeElasticSearchService : IElasticSearchService
{
    public Task EnsureIndexExistsAsync() => Task.CompletedTask;

    public Task<List<CitySuggestion>> GetCitySuggestionsAsync(string partialName, string language)
    {
        return Task.FromResult(new List<CitySuggestion>
        {
            new CitySuggestion
            {
                Id = "Q269",
                Name = "タシュケント",
                CountryCode = "UZ",
                Country = "Uzbekistan",
                Population = 2956384
            }
        });
    }

    public Task<CityDoc?> GetCityDocByIdAsync(string cityId) => Task.FromResult<CityDoc?>(null);
    public Task BulkUpsertCitiesAsync(List<SparQLCityInfo> cities) => Task.CompletedTask;
    public Task UpsertCityAsync(CityDoc city) => Task.CompletedTask;
    public Task<long> GetDocumentCountAsync() => Task.FromResult(1L);
}

public class RequestHandlerTests
{
    [Fact]
    public async Task GetCitySuggestionsAsync_Should_Accept_Japanese_Input()
    {
        var fakeEs = new FakeElasticSearchService();
        var result = await RequestHandler.GetCitySuggestionsAsync("タシ", fakeEs, "ja");

        // Verify we get an Ok result, not BadRequest
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<CitySuggestion>>>(result);
    }

    [Fact]
    public async Task GetCitySuggestionsAsync_Should_Return_Suggestions_For_Japanese_Query()
    {
        var fakeEs = new FakeElasticSearchService();
        var result = await RequestHandler.GetCitySuggestionsAsync("タシ", fakeEs, "ja");

        var okResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<CitySuggestion>>>(result);
        var suggestions = okResult.Value;
        Assert.NotNull(suggestions);
        Assert.Single(suggestions);
        Assert.Equal("タシュケント", suggestions[0].Name);
    }

    [Fact]
    public async Task GetCitySuggestionsAsync_Should_Reject_Short_Query()
    {
        var fakeEs = new FakeElasticSearchService();
        var result = await RequestHandler.GetCitySuggestionsAsync("x", fakeEs, "ja");

        Assert.IsNotType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<CitySuggestion>>>(result);
    }
}