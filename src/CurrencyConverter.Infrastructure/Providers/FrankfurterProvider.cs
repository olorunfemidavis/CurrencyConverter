using CurrencyConverter.Domain.Interfaces;
using System.Net.Http.Json;
using CurrencyConverter.Domain.DTOs;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Infrastructure.Providers;

/// <summary>
/// FrankfurterProvider is an implementation of ICurrencyProvider that interacts with the Frankfurter API
/// </summary>
public class FrankfurterProvider : ICurrencyProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FrankfurterProvider> _logger;
    private static readonly string[] ExcludedCurrencies = { "TRY", "PLN", "THB", "MXN" };

    public FrankfurterProvider(HttpClient httpClient, ILogger<FrankfurterProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the latest exchange rates for a given base currency
    /// </summary>
    /// <param name="baseCurrency"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    public async Task<ExchangeRateResponse> GetLatestRatesAsync(string baseCurrency)
    {
        // Use the HttpClient directly, as resilience is now handled by the configured handler
        var result = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>($"latest?from={baseCurrency}");
        if (result == null)
            throw new HttpRequestException("Invalid response from Frankfurter API");
        FilterExcludedCurrencies(result.Rates);
        return result;
    }

    /// <summary>
    /// Converts an amount from one currency to another using the Frankfurter API
    /// </summary>
    /// <param name="fromCurrency"></param>
    /// <param name="toCurrency"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    public async Task<ExchangeRateResponse> ConvertCurrencyAsync(string fromCurrency, string toCurrency, decimal amount)
    {
        var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>($"latest?from={fromCurrency}&to={toCurrency}&amount={amount}");
        if (response == null)
            throw new HttpRequestException("Invalid response from Frankfurter API");

        FilterExcludedCurrencies(response.Rates);
        return response;
    }

    /// <summary>
    /// Gets historical exchange rates for a given base currency within a specified date range
    /// </summary>
    /// <param name="baseCurrency"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    public async Task<PagedHistoricalRatesResponse> GetHistoricalRatesAsync(string baseCurrency, DateTime startDate, DateTime endDate, int page, int pageSize)
    {
        var url = $"{startDate:yyyy-MM-dd}..{endDate:yyyy-MM-dd}?from={baseCurrency}";
        var result = await _httpClient.GetFromJsonAsync<PagedHistoricalRatesResponse>(url);
        if (result == null)
            throw new HttpRequestException("Invalid response from Frankfurter API");

        // Filter rates within the date range
        var filteredByDate = result.Rates
            .Where(r => r.Key >= startDate && r.Key <= endDate);
        
        //Sort result.Rates by date, apply pagination, and filter excluded currencies
        // Note: The Frankfurter API does not support pagination directly, so we will handle it manually
        var sortedRates = filteredByDate
            .OrderBy(r => r.Key);
        
        // Apply pagination
        var pagedRates = sortedRates
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
         
        // Filter out excluded currencies
        var filteredRates = pagedRates.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
                .Where(currency => !ExcludedCurrencies.Contains(currency.Key))
                .ToDictionary(c => c.Key, c => c.Value)
        );

        return new PagedHistoricalRatesResponse
        {
            Amount = result.Amount,
            BaseCurrency = baseCurrency,
            StartDate = startDate,
            EndDate = endDate,
            Page = page,
            PageSize = pageSize,
            TotalRecords = result.Rates.Count,
            Rates = filteredRates
        };
    }

    private void FilterExcludedCurrencies(Dictionary<string, decimal> rates)
    {
        foreach (var currency in ExcludedCurrencies)
            rates.Remove(currency);
    }
}