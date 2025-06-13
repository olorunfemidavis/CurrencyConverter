using System.ComponentModel.DataAnnotations;
using CurrencyConverter.Application.Helpers;
using CurrencyConverter.Domain.DTOs;
using CurrencyConverter.Domain.Interfaces;
using CurrencyConverter.Infrastructure.Providers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Application.Queries;

public record GetHistoricalRatesQuery(
    [Required(ErrorMessage = "Base currency is required.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Base currency must be a valid 3-letter ISO code.")]
    string BaseCurrency,

    [Required(ErrorMessage = "Start date is required.")]
    DateTime StartDate,

    [Required(ErrorMessage = "End date is required.")]
    [DateRange(nameof(StartDate), ErrorMessage = "EndDate must be greater than or equal to StartDate and not in the future.")]
    DateTime EndDate,

    [Range(1, int.MaxValue, ErrorMessage = "Page must be a positive integer.")]
    int Page = 1,

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    int PageSize = 10
) : IRequest<PagedHistoricalRatesResponse>;

public class GetHistoricalRatesQueryHandler : IRequestHandler<GetHistoricalRatesQuery, PagedHistoricalRatesResponse>
{
    private readonly ICurrencyProviderFactory _providerFactory;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetHistoricalRatesQueryHandler> _logger;
    private readonly string _activeProvider;


    public GetHistoricalRatesQueryHandler(ICurrencyProviderFactory providerFactory, ICacheService cacheService, ILogger<GetHistoricalRatesQueryHandler> logger, IConfiguration configuration)
    {
        _providerFactory = providerFactory;
        _cacheService = cacheService;
        _logger = logger;
        _activeProvider = configuration["CurrencyProvider:ActiveProvider"] ?? string.Empty;
    }

    /// <summary>
    /// Handles the GetHistoricalRatesQuery to fetch historical exchange rates for a given base currency and date range. Caches the result for 24 hours.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<PagedHistoricalRatesResponse> Handle(GetHistoricalRatesQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"historical:{request.BaseCurrency}:{request.StartDate:yyyy-MM-dd}:{request.EndDate:yyyy-MM-dd}:{request.Page}:{request.PageSize}";
        var cachedResult = await _cacheService.GetAsync<PagedHistoricalRatesResponse>(cacheKey);
        if (cachedResult != null)
        {
            _logger.LogInformation("Cache hit for {CacheKey}", cacheKey);
            return cachedResult;
        }

        var provider = _providerFactory.CreateProvider(_activeProvider);
        var result = await provider.GetHistoricalRatesAsync(request.BaseCurrency, request.StartDate, request.EndDate, request.Page, request.PageSize);
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(24));

        return result;
    }
}