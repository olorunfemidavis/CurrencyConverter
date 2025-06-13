using System.Net.Http.Json;
using System.Text.Json;
using CurrencyConverter.API;
using CurrencyConverter.API.Controllers;
using CurrencyConverter.Domain.DTOs;
using CurrencyConverter.Domain.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace CurrencyConverter.IntegrationTests;

/// <summary>
/// Integration tests for the RatesController.
/// </summary>
public class RatesControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly WireMockServer _wireMockServer;
    private readonly HttpClient _client;

    public RatesControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Initialize WireMock server
        _wireMockServer = WireMockServer.Start();

        // Configure the WebApplicationFactory to use the WireMock server and mock services
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Redis:ConnectionString", "localhost:6379" }, // Placeholder, unused with mock
                    { "Frankfurter:BaseUrl", _wireMockServer.Url! },
                    { "Jwt:Key", "your-secure-key-here-32-characters-long" },
                    { "Jwt:Issuer", "CurrencyConverterAPI" },
                    { "Jwt:Audience", "CurrencyConverterAPI" },
                    { "CurrencyProvider:ActiveProvider", "Frankfurter" }
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICacheService>();
                services.AddSingleton<ICacheService, MockCacheService>();
            });
        });

        // Create the HttpClient for testing
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Disposes the resources used by the test class.
    /// </summary>
    public void Dispose()
    {
        _wireMockServer.Stop();
        _client.Dispose();
    }

    /// <summary>
    /// Initializes the WireMock server with predefined responses for testing.
    /// </summary>
    private async Task InitializeWireMockAsync()
    {
        // Mock latest rates: GET /latest?from=EUR
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/latest")
                .WithParam("from", "EUR")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    amount = 1.0,
                    @base = "EUR",
                    date = "2025-06-12",
                    rates = new Dictionary<string, decimal>
                    {
                        { "USD", 1.1594m },
                        { "AUD", 1.7798m },
                        { "TRY", 45.589m },
                        { "PLN", 4.2690m },
                        { "THB", 37.546m },
                        { "MXN", 21.901m }
                    }
                }));

        // Mock currency conversion: GET /latest?from=EUR&to=USD&amount=100
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/latest")
                .WithParam("from", "EUR")
                .WithParam("to", "USD")
                .WithParam("amount", "100")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    amount = 100.0,
                    @base = "EUR",
                    date = "2025-06-12",
                    rates = new Dictionary<string, decimal>
                    {
                        { "USD", 115.94m }
                    }
                }));

        // Mock historical rates: GET /2025-01-01..2025-01-05?from=EUR
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/2025-01-01..2025-01-05")
                .WithParam("from", "EUR")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    amount = 1.0,
                    @base = "EUR",
                    start_date = "2024-12-31",
                    end_date = "2025-01-05",
                    rates = new Dictionary<string, Dictionary<string, decimal>>
                    {
                        { "2024-12-31", new Dictionary<string, decimal> { { "USD", 1.1374m }, { "AUD", 1.7800m }, { "TRY", 43.735m } } },
                        { "2025-01-01", new Dictionary<string, decimal> { { "USD", 1.1374m }, { "AUD", 1.7800m }, { "TRY", 43.735m } } },
                        { "2025-01-02", new Dictionary<string, decimal> { { "USD", 1.1395m }, { "AUD", 1.7850m }, { "TRY", 43.850m } } }
                    }
                }));

        // Mock invalid base currency: GET /latest?from=INVALID
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/latest")
                .WithParam("from", "INVALID")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithBodyAsJson(new { error = "Invalid base currency" }));

        // Mock invalid target currency: GET /latest?from=EUR&to=INVALID
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/latest")
                .WithParam("from", "EUR")
                .WithParam("to", "INVALID")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithBodyAsJson(new { error = "Invalid target currency" }));
    }

    /// <summary>
    /// Gets a JWT token for the specified role.
    /// </summary>
    /// <param name="role"></param>
    /// <returns></returns>
    private async Task<string> GetJwtToken(string role)
    {
        var request = new TokenRequest("test", "password", role);
        var response = await _client.PostAsJsonAsync("/api/v1/auth/token", request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        return content.GetProperty("token").GetString()!;
    }

    /// <summary>
    /// Tests the GetLatestRates endpoint with a valid user role and currency.
    /// </summary>
    [Fact]
    public async Task GetLatestRates_UserRoleValidCurrency_ReturnsSuccess()
    {
        await InitializeWireMockAsync();
        var token = await GetJwtToken("User");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/v1/rates/latest?baseCurrency=EUR");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ExchangeRateResponse>();
        result.Should().NotBeNull();
        result!.Base.Should().Be("EUR");
        result.Rates["USD"].Should().Be(1.1594m); 
    }

    /// <summary>
    /// Tests the GetLatestRates endpoint with an Unauthorized request.
    /// </summary>
    [Fact]
    public async Task GetLatestRates_UnauthorizedNoToken_Returns401()
    {
        await InitializeWireMockAsync();
        var response = await _client.GetAsync("/api/v1/rates/latest?baseCurrency=EUR");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Tests the GetLatestRates endpoint with an invalid currency.
    /// </summary>
    [Fact]
    public async Task GetLatestRates_InvalidCurrency_ReturnsBadRequest()
    {
        await InitializeWireMockAsync();
        var token = await GetJwtToken("User");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/v1/rates/latest?baseCurrency=INVALID");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests the Convert endpoint with a valid admin role and currency conversion request.
    /// </summary>
    [Fact]
    public async Task Convert_AdminRoleValidInput_ReturnsSuccess()
    {
        await InitializeWireMockAsync();
        var token = await GetJwtToken("Admin");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/v1/rates/convert?fromCurrency=EUR&toCurrency=USD&amount=100");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ExchangeRateResponse>();
        result.Should().NotBeNull();
        result!.Base.Should().Be("EUR");
        result.Rates["USD"].Should().Be(115.94m);
    }

    /// <summary>
    /// Tests the Convert endpoint with an Excluded Currency, which should return a BadRequest.
    /// </summary>
    [Fact]
    public async Task Convert_ExcludedFromCurrency_ReturnsBadRequest()
    {
        await InitializeWireMockAsync();
        var token = await GetJwtToken("Admin");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/v1/rates/convert?fromCurrency=TRY&toCurrency=USD&amount=100");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests the Convert endpoint with an invalid amount, which should return a bad request.
    /// </summary>
    [Fact]
    public async Task Convert_NegativeAmount_ReturnsBadRequest()
    {
        await InitializeWireMockAsync();
        var token = await GetJwtToken("Admin");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/v1/rates/convert?fromCurrency=EUR&toCurrency=USD&amount=-100");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests the Convert endpoint with no Auth, which should return Unauthorized.
    /// </summary>
    [Fact]
    public async Task Convert_UnauthorizedNoToken_Returns401()
    {
        await InitializeWireMockAsync();
        var response = await _client.GetAsync("/api/v1/rates/convert?fromCurrency=EUR&toCurrency=USD&amount=100");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Tests the GetHistoricalRates endpoint with a valid admin role and date range.
    /// </summary>
    [Fact]
    public async Task GetHistoricalRates_AdminRoleValidInput_ReturnsSuccess()
    {
        await InitializeWireMockAsync();
        var token = await GetJwtToken("Admin");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/v1/rates/historical?baseCurrency=EUR&startDate=2025-01-01&endDate=2025-01-05&page=1&pageSize=10");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedHistoricalRatesResponse>();
        result.Should().NotBeNull();
        result!.Base.Should().Be("EUR");
        result.Rates.Should().HaveCount(2);
    }

    /// <summary>
    /// Tests the GetHistoricalRates endpoint with a user role, which should return forbidden.
    /// </summary>
    [Fact]
    public async Task GetHistoricalRates_UserRole_ReturnsForbidden()
    {
        await InitializeWireMockAsync();
        var token = await GetJwtToken("User");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/v1/rates/historical?baseCurrency=EUR&startDate=2025-01-01&endDate=2025-01-05&page=1&pageSize=10");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Tests the GetHistoricalRates endpoint with an invalid date range, which should return a bad request.
    /// </summary>
    [Fact]
    public async Task GetHistoricalRates_InvalidDateRange_ReturnsBadRequest()
    {
        await InitializeWireMockAsync();
        var token = await GetJwtToken("Admin");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/v1/rates/historical?baseCurrency=EUR&startDate=2025-01-05&endDate=2025-01-01&page=1&pageSize=10");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests the GetHistoricalRates endpoint with no Auth, which should return Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetHistoricalRates_UnauthorizedNoToken_Returns401()
    {
        await InitializeWireMockAsync();
        var response = await _client.GetAsync("/api/v1/rates/historical?baseCurrency=EUR&startDate=2025-01-01&endDate=2025-01-05&page=1&pageSize=10");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}

/// <summary>
/// Mock implementation of ICacheService for testing purposes.
/// </summary>
public class MockCacheService : ICacheService
{
    private readonly Dictionary<string, object> _cache = new();
    public Task<T> GetAsync<T>(string key) where T : class => Task.FromResult(_cache.TryGetValue(key, out var value) ? (T)value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan expiry) where T : class
    {
        _cache[key] = value!;
        return Task.CompletedTask;
    }
}