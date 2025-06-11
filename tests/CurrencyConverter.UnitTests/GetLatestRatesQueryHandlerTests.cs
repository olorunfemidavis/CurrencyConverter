using CurrencyConverter.Application.Queries;
using CurrencyConverter.Domain.DTOs;
using CurrencyConverter.Domain.Interfaces;
using CurrencyConverter.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CurrencyConverter.UnitTests;

public class GetLatestRatesQueryHandlerTests
{
    private readonly Mock<ICurrencyProviderFactory> _providerFactoryMock;
    private readonly Mock<ICurrencyProvider> _providerMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<GetLatestRatesQueryHandler>> _loggerMock;
    private readonly GetLatestRatesQueryHandler _handler;

    public GetLatestRatesQueryHandlerTests()
    {
        _providerFactoryMock = new Mock<ICurrencyProviderFactory>();
        _providerMock = new Mock<ICurrencyProvider>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<GetLatestRatesQueryHandler>>();
        _providerFactoryMock.Setup(f => f.CreateProvider("Frankfurter")).Returns(_providerMock.Object);
        _handler = new GetLatestRatesQueryHandler(_providerFactoryMock.Object, _cacheServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedRates()
    {
        // Arrange
        var query = new GetLatestRatesQuery("EUR");
        var cachedRates = new ExchangeRateResponse { BaseCurrency = "EUR", Date = DateTime.UtcNow, Rates = new() { { "USD", 1.1m } } };
        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>())).ReturnsAsync(cachedRates);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(cachedRates);
        _providerMock.Verify(p => p.GetLatestRatesAsync(It.IsAny<string>()), Times.Never());
    }
}