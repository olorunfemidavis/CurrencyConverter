using CurrencyConverter.Application.Queries;
using CurrencyConverter.Domain.DTOs;
using CurrencyConverter.Domain.Interfaces;
using CurrencyConverter.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace CurrencyConverter.UnitTests;

/// <summary>
/// Unit tests for the GetLatestRatesQueryHandler.
/// </summary>
public class GetLatestRatesQueryHandlerTests
{
    private readonly Mock<ICurrencyProviderFactory> _providerFactoryMock;
    private readonly Mock<ICurrencyProvider> _providerMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<GetLatestRatesQueryHandler>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly GetLatestRatesQueryHandler _handler;

    public GetLatestRatesQueryHandlerTests()
    {
        // Initialize mocks
        _providerFactoryMock = new Mock<ICurrencyProviderFactory>();
        _providerMock = new Mock<ICurrencyProvider>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<GetLatestRatesQueryHandler>>();
        _configurationMock = new Mock<IConfiguration>();

        // Setup configuration and provider factory
        _configurationMock.Setup(c => c["CurrencyProvider:ActiveProvider"]).Returns("Frankfurter");
        _providerFactoryMock.Setup(f => f.CreateProvider("Frankfurter")).Returns(_providerMock.Object);

        // Initialize the handler with the mocked dependencies
        _handler = new GetLatestRatesQueryHandler(
            _providerFactoryMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object,
            _configurationMock.Object);
    }

    /// <summary>
    /// Handle method returns cached result when cache hit occurs.
    /// </summary>
    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedResult()
    {
        // Arrange
        var query = new GetLatestRatesQuery("EUR");
        var cachedResponse = new ExchangeRateResponse
        {
            Base = "EUR",
            Date = DateTime.UtcNow.Date,
            Rates = new Dictionary<string, decimal> { { "USD", 1.1m } }
        };

        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>("rates:latest:EUR"))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(cachedResponse);
        _providerFactoryMock.Verify(f => f.CreateProvider(It.IsAny<string>()), Times.Never());
        _cacheServiceMock.Verify(c => c.GetAsync<ExchangeRateResponse>("rates:latest:EUR"), Times.Once());
        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<ExchangeRateResponse>(), It.IsAny<TimeSpan>()), Times.Never());
    }

    /// <summary>
    /// Handle method calls provider and caches result when cache miss occurs.
    /// </summary>
    [Fact]
    public async Task Handle_CacheMiss_CallsProviderAndCachesResult()
    {
        // Arrange
        var query = new GetLatestRatesQuery("EUR");
        var providerResponse = new ExchangeRateResponse
        {
            Base = "EUR",
            Date = DateTime.UtcNow.Date,
            Rates = new Dictionary<string, decimal> { { "USD", 1.1m } }
        };

        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>("rates:latest:EUR"))
            .ReturnsAsync((ExchangeRateResponse)null);
        _providerMock.Setup(p => p.GetLatestRatesAsync("EUR"))
            .ReturnsAsync(providerResponse);
        _cacheServiceMock.Setup(c => c.SetAsync("rates:latest:EUR", providerResponse, TimeSpan.FromHours(1)))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(providerResponse);
        _providerFactoryMock.Verify(f => f.CreateProvider("Frankfurter"), Times.Once());
        _providerMock.Verify(p => p.GetLatestRatesAsync("EUR"), Times.Once());
        _cacheServiceMock.Verify(c => c.GetAsync<ExchangeRateResponse>("rates:latest:EUR"), Times.Once());
        _cacheServiceMock.Verify(c => c.SetAsync("rates:latest:EUR", providerResponse, TimeSpan.FromHours(1)), Times.Once());
    }

    /// <summary>
    /// Handle method throws NotSupportedException when an invalid provider is configured.
    /// </summary>
    [Fact]
    public async Task Handle_InvalidProvider_ThrowsNotSupportedException()
    {
        // Arrange
        var query = new GetLatestRatesQuery("EUR");
        _configurationMock.Setup(c => c["CurrencyProvider:ActiveProvider"]).Returns("InvalidProvider");
        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>("rates:latest:EUR"))
            .ReturnsAsync((ExchangeRateResponse)null);
        _providerFactoryMock.Setup(f => f.CreateProvider("InvalidProvider"))
            .Throws(new NotSupportedException("Provider InvalidProvider not supported."));

        var handler = new GetLatestRatesQueryHandler(
            _providerFactoryMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object,
            _configurationMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            handler.Handle(query, CancellationToken.None));
        _providerFactoryMock.Verify(f => f.CreateProvider("InvalidProvider"), Times.Once());
        _providerMock.Verify(p => p.GetLatestRatesAsync(It.IsAny<string>()), Times.Never());
    }

    /// <summary>
    /// Handle method throws ArgumentException when an invalid base currency is specified.
    /// </summary>
    [Fact]
    public async Task Handle_InvalidBaseCurrency_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetLatestRatesQuery("INVALID");
        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>("rates:latest:INVALID"))
            .ReturnsAsync((ExchangeRateResponse)null);
        _providerMock.Setup(p => p.GetLatestRatesAsync("INVALID"))
            .ThrowsAsync(new ArgumentException("Invalid base currency"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(query, CancellationToken.None));
        _providerFactoryMock.Verify(f => f.CreateProvider("Frankfurter"), Times.Once());
        _providerMock.Verify(p => p.GetLatestRatesAsync("INVALID"), Times.Once());
    }

    /// <summary>
    /// Handle method throws OperationCanceledException when cancellation is requested.
    /// </summary>
    [Fact]
    public async Task Handle_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var query = new GetLatestRatesQuery("EUR");
        var cts = new CancellationTokenSource();
        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>("rates:latest:EUR"))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await cts.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _handler.Handle(query, cts.Token));
        _providerFactoryMock.Verify(f => f.CreateProvider(It.IsAny<string>()), Times.Never());
    }

    /// <summary>
    /// Handle method logs information when a cache hit occurs.
    /// </summary>
    [Fact]
    public async Task Handle_CacheHit_LogsInformation()
    {
        // Arrange
        var query = new GetLatestRatesQuery("EUR");
        var cachedResponse = new ExchangeRateResponse
        {
            Base = "EUR",
            Date = DateTime.UtcNow.Date,
            Rates = new Dictionary<string, decimal> { { "USD", 1.1m } }
        };

        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>("rates:latest:EUR"))
            .ReturnsAsync(cachedResponse);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cache hit for rates:latest:EUR")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once());
    }
}