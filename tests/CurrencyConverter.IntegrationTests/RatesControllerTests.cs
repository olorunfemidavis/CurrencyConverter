using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CurrencyConverter.Domain.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace CurrencyConverter.IntegrationTests;

public class RatesControllerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly WireMockServer _wireMockServer;
    private readonly HttpClient _client;

    public RatesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _wireMockServer = WireMockServer.Start();
        _client = _factory.CreateClient();
        
        // Configure WireMock to mock Frankfurter API
        _wireMockServer
            .Given(Request.Create().WithPath("/latest").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { baseCurrency = "EUR", date = "2025-06-10", rates = new { USD = 1.1m } }));
    }

    [Fact]
    public async Task GetLatestRates_ReturnsSuccess()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new("Bearer", GenerateTestJwt());

        // Act
        var response = await _client.GetAsync("/api/v1/rates/latest?baseCurrency=EUR");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExchangeRateResponse>();
        Assert.Equal("EUR", result?.BaseCurrency);
        Assert.Equal(1.1m, result?.Rates["USD"]);
    }

    private string GenerateTestJwt()
    {
        // Simplified JWT generation for testing
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes("your-secure-key-here-32-characters-long");
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "User") }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public void Dispose()
    {
        _wireMockServer.Stop();
        _client.Dispose();
    }
}