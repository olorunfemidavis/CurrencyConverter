using System.ComponentModel.DataAnnotations;
using CurrencyConverter.Domain.DTOs;
using CurrencyConverter.Domain.Interfaces;
using CurrencyConverter.Infrastructure.Providers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Application.Queries;

public class ConvertCurrencyQuery : IRequest<ExchangeRateResponse>
{
    [Required]
    public string FromCurrency { get; set; }

    [Required]
    public string ToCurrency { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive.")]
    public decimal Amount { get; set; }
}
public class ConvertCurrencyQueryHandler : IRequestHandler<ConvertCurrencyQuery, ExchangeRateResponse>
{
    private readonly ICurrencyProviderFactory _providerFactory;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ConvertCurrencyQueryHandler> _logger;
    private readonly string _activeProvider;


    public ConvertCurrencyQueryHandler(ICurrencyProviderFactory providerFactory, ICacheService cacheService, ILogger<ConvertCurrencyQueryHandler> logger, IConfiguration configuration)
    {
        _providerFactory = providerFactory;
        _cacheService = cacheService;
        _logger = logger;
        _activeProvider = configuration["CurrencyProvider:ActiveProvider"] ?? string.Empty;
    }

    /// <summary>
    /// Handles the ConvertCurrencyQuery to convert an amount from one currency to another. Caches the result for 1 hour. Caches the result for 1 hour.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ExchangeRateResponse> Handle(ConvertCurrencyQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"convert:{request.FromCurrency}:{request.ToCurrency}:{request.Amount}";
        var cachedResult = await _cacheService.GetAsync<ExchangeRateResponse>(cacheKey);
        if (cachedResult != null)
        {
            _logger.LogInformation("Cache hit for {CacheKey}", cacheKey);
            return cachedResult;
        }

        var provider = _providerFactory.CreateProvider(_activeProvider);
        var result = await provider.ConvertCurrencyAsync(request.FromCurrency, request.ToCurrency, request.Amount);
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));

        return result;
    }
}