// write unit tests for every implemented endpoint in Program.cs
// Path: CityDistanceService/src/Program.cs
// implement the endpoints for the following routes, depending on HTTP Methods and query parameters:
// GET requests:
// /distance?city1=city1&city2=city2
// /city?cityName=cityName
// /city?cityId=cityId
// /city?cityNameContains=cityNameContains
// POST requests:
// /city - data in the body
// PUT requests:
// /city - data in the body
// DELETE requests:
// /city?cityId=cityId
// Generate the unit tests for the implemented endpoints in Program.cs below:   

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CityDistanceService.UnitTests
{
    public class ProgramTests : IClassFixture<WebApplicationFactory<CityDistanceService.Program>>
    {
        private readonly WebApplicationFactory<CityDistanceService.Program> _factory;

        public ProgramTests(WebApplicationFactory<CityDistanceService.Program> factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/distance?city1=city1&city2=city2", "GET", "Distance between: city1 to city2 is: 0.")]
        [InlineData("/city?cityName=cityName", "GET", "{\"CityName\":null}")]
        [InlineData("/city?cityId=cityId", "GET", "{\"CityName\":null}")]
        [InlineData("/city?cityNameContains=cityNameContains", "GET", "[]")]
        public async Task TestEndpoint(string url, string method, string expected)
        {
            var client = _factory.CreateClient();
            var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal(expected, responseString);
        }

        [Fact]
        public async Task TestPostEndpoint()
        {
            var client = _factory.CreateClient();
            var response = await client.PostAsync("/city", new StringContent(""));
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal("[]", responseString);
        }

        [Fact]
        public async Task TestPutEndpoint()
        {
            var client = _factory.CreateClient();
            var response = await client.PutAsync("/city", new StringContent(""));
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal("[]", responseString);
        }

        [Fact]
        public async Task TestDeleteEndpoint()
        {
            var client = _factory.CreateClient();
            var response = await client.DeleteAsync("/city?cityId=cityId");
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal("[]", responseString);
        }
    }
}
```