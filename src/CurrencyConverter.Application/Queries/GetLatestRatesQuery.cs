using System.ComponentModel.DataAnnotations;
using CurrencyConverter.Domain.DTOs;
using CurrencyConverter.Domain.Interfaces;
using CurrencyConverter.Infrastructure.Providers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Application.Queries;

public record GetLatestRatesQuery(
    [Required(ErrorMessage = "Base currency is required.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Base currency must be a valid 3-letter ISO code.")]
    string BaseCurrency
) : IRequest<ExchangeRateResponse>;

/// <summary>
/// Handler for the GetLatestRatesQuery.
/// </summary>
public class GetLatestRatesQueryHandler : IRequestHandler<GetLatestRatesQuery, ExchangeRateResponse>
{
    private readonly ICurrencyProviderFactory _providerFactory;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetLatestRatesQueryHandler> _logger;
    private readonly string _activeProvider;


    public GetLatestRatesQueryHandler(ICurrencyProviderFactory providerFactory, ICacheService cacheService, ILogger<GetLatestRatesQueryHandler> logger, IConfiguration configuration)
    {
        _providerFactory = providerFactory;
        _cacheService = cacheService;
        _logger = logger;
        _activeProvider = configuration["CurrencyProvider:ActiveProvider"] ?? string.Empty;
    }

    /// <summary>
    /// Handles the GetLatestRatesQuery to fetch the latest exchange rates for a given base currency. Caches the result for 1 hour.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ExchangeRateResponse> Handle(GetLatestRatesQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"rates:latest:{request.BaseCurrency}";
        var cachedRates = await _cacheService.GetAsync<ExchangeRateResponse>(cacheKey);
        if (cachedRates != null)
        {
            _logger.LogInformation("Cache hit for {CacheKey}", cacheKey);
            return cachedRates;
        }

        var provider = _providerFactory.CreateProvider(_activeProvider);
        var rates = await provider.GetLatestRatesAsync(request.BaseCurrency);
        await _cacheService.SetAsync(cacheKey, rates, TimeSpan.FromHours(1));

        return rates;
    }
}