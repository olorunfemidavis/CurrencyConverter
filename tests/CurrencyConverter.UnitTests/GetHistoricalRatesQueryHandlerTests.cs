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
/// Tests for the GetHistoricalRatesQueryHandler.
/// </summary>
public class GetHistoricalRatesQueryHandlerTests
{
    private readonly Mock<ICurrencyProviderFactory> _providerFactoryMock;
    private readonly Mock<ICurrencyProvider> _providerMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<GetHistoricalRatesQueryHandler>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly GetHistoricalRatesQueryHandler _handler;

    public GetHistoricalRatesQueryHandlerTests()
    {
        // Initialize mocks
        _providerFactoryMock = new Mock<ICurrencyProviderFactory>();
        _providerMock = new Mock<ICurrencyProvider>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<GetHistoricalRatesQueryHandler>>();
        _configurationMock = new Mock<IConfiguration>();

        // Setup configuration and provider factory
        _configurationMock.Setup(c => c["CurrencyProvider:ActiveProvider"]).Returns("Frankfurter");
        _providerFactoryMock.Setup(f => f.CreateProvider("Frankfurter")).Returns(_providerMock.Object);

        // Initialize the handler with the mocked dependencies
        _handler = new GetHistoricalRatesQueryHandler(
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
        var query = new GetHistoricalRatesQuery("EUR", new DateTime(2025, 1, 1), new DateTime(2025, 1, 5), 1, 10);
        var cachedResponse = new PagedHistoricalRatesResponse
        {
            BaseCurrency = "EUR",
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 1, 5),
            Page = 1,
            PageSize = 10,
            TotalRecords = 5,
            Rates = []
        };
        cachedResponse.Rates.Add(new DateTime(2025, 1, 1), new Dictionary<string, decimal> { { "USD", 1.1m } });

        _cacheServiceMock.Setup(c => c.GetAsync<PagedHistoricalRatesResponse>("historical:EUR:2025-01-01:2025-01-05:1:10"))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(cachedResponse);
        _providerFactoryMock.Verify(f => f.CreateProvider(It.IsAny<string>()), Times.Never());
        _cacheServiceMock.Verify(c => c.GetAsync<PagedHistoricalRatesResponse>("historical:EUR:2025-01-01:2025-01-05:1:10"), Times.Once());
        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<PagedHistoricalRatesResponse>(), It.IsAny<TimeSpan>()), Times.Never());
    }

    /// <summary>
    /// Handle method calls provider and caches result when cache miss occurs.
    /// </summary>
    [Fact]
    public async Task Handle_CacheMiss_CallsProviderAndCachesResult()
    {
        // Arrange
        var query = new GetHistoricalRatesQuery("EUR", new DateTime(2025, 1, 1), new DateTime(2025, 1, 5), 1, 10);
        var providerResponse = new PagedHistoricalRatesResponse
        {
            BaseCurrency = "EUR",
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 1, 5),
            Page = 1,
            PageSize = 10,
            TotalRecords = 5,
            Rates = []
        };
        providerResponse.Rates.Add(new DateTime(2025, 1, 1), new Dictionary<string, decimal> { { "USD", 1.1m } });

        _cacheServiceMock.Setup(c => c.GetAsync<PagedHistoricalRatesResponse>("historical:EUR:2025-01-01:2025-01-05:1:10"))
            .ReturnsAsync((PagedHistoricalRatesResponse)null);
        _providerMock.Setup(p => p.GetHistoricalRatesAsync("EUR", new DateTime(2025, 1, 1), new DateTime(2025, 1, 5), 1, 10))
            .ReturnsAsync(providerResponse);
        _cacheServiceMock.Setup(c => c.SetAsync("historical:EUR:2025-01-01:2025-01-05:1:10", providerResponse, TimeSpan.FromHours(24)))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(providerResponse);
        _providerFactoryMock.Verify(f => f.CreateProvider("Frankfurter"), Times.Once());
        _providerMock.Verify(p => p.GetHistoricalRatesAsync("EUR", new DateTime(2025, 1, 1), new DateTime(2025, 1, 5), 1, 10), Times.Once());
        _cacheServiceMock.Verify(c => c.GetAsync<PagedHistoricalRatesResponse>("historical:EUR:2025-01-01:2025-01-05:1:10"), Times.Once());
        _cacheServiceMock.Verify(c => c.SetAsync("historical:EUR:2025-01-01:2025-01-05:1:10", providerResponse, TimeSpan.FromHours(24)), Times.Once());
    }

    /// <summary>
    /// Handle method throws NotSupportedException when an invalid provider is configured.
    /// </summary>
    [Fact]
    public async Task Handle_InvalidProvider_ThrowsNotSupportedException()
    {
        // Arrange
        var query = new GetHistoricalRatesQuery("EUR", new DateTime(2025, 1, 1), new DateTime(2025, 1, 5), 1, 10);
        _configurationMock.Setup(c => c["CurrencyProvider:ActiveProvider"]).Returns("InvalidProvider");
        _cacheServiceMock.Setup(c => c.GetAsync<PagedHistoricalRatesResponse>("historical:EUR:2025-01-01:2025-01-05:1:10"))
            .ReturnsAsync((PagedHistoricalRatesResponse)null);
        _providerFactoryMock.Setup(f => f.CreateProvider("InvalidProvider"))
            .Throws(new NotSupportedException("Provider InvalidProvider not supported."));

        var handler = new GetHistoricalRatesQueryHandler(
            _providerFactoryMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object,
            _configurationMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            handler.Handle(query, CancellationToken.None));
        _providerFactoryMock.Verify(f => f.CreateProvider("InvalidProvider"), Times.Once());
        _providerMock.Verify(p => p.GetHistoricalRatesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never());
    }

    /// <summary>
    /// Handle method throws ArgumentException when an invalid date range is specified (start date after end date).
    /// </summary>
    [Fact]
    public async Task Handle_InvalidDateRange_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetHistoricalRatesQuery("EUR", new DateTime(2025, 1, 5), new DateTime(2025, 1, 1), 1, 10);
        _cacheServiceMock.Setup(c => c.GetAsync<PagedHistoricalRatesResponse>("historical:EUR:2025-01-05:2025-01-01:1:10"))
            .ReturnsAsync((PagedHistoricalRatesResponse)null);
        _providerMock.Setup(p => p.GetHistoricalRatesAsync("EUR", new DateTime(2025, 1, 5), new DateTime(2025, 1, 1), 1, 10))
            .ThrowsAsync(new ArgumentException("Start date cannot be after end date"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(query, CancellationToken.None));
        _providerFactoryMock.Verify(f => f.CreateProvider("Frankfurter"), Times.Once());
        _providerMock.Verify(p => p.GetHistoricalRatesAsync("EUR", new DateTime(2025, 1, 5), new DateTime(2025, 1, 1), 1, 10), Times.Once());
    }

    /// <summary>
    /// Handle method throws OperationCanceledException when cancellation is requested.
    /// </summary>
    [Fact]
    public async Task Handle_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var query = new GetHistoricalRatesQuery("EUR", new DateTime(2025, 1, 1), new DateTime(2025, 1, 5), 1, 10);
        var cts = new CancellationTokenSource();
        _cacheServiceMock.Setup(c => c.GetAsync<PagedHistoricalRatesResponse>("historical:EUR:2025-01-01:2025-01-05:1:10"))
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
        var query = new GetHistoricalRatesQuery("EUR", new DateTime(2025, 1, 1), new DateTime(2025, 1, 5), 1, 10);
        var cachedResponse = new PagedHistoricalRatesResponse
        {
            BaseCurrency = "EUR",
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 1, 5),
            Page = 1,
            PageSize = 10,
            TotalRecords = 5,
            Rates = []
        };
        cachedResponse.Rates.Add(new DateTime(2025, 1, 1), new Dictionary<string, decimal> { { "USD", 1.1m } });

        _cacheServiceMock.Setup(c => c.GetAsync<PagedHistoricalRatesResponse>("historical:EUR:2025-01-01:2025-01-05:1:10"))
            .ReturnsAsync(cachedResponse);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cache hit for historical:EUR:2025-01-01:2025-01-05:1:10")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once());
    }
}