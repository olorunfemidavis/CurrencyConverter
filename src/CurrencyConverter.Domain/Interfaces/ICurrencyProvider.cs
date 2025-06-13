using CurrencyConverter.Domain.DTOs;

namespace CurrencyConverter.Domain.Interfaces;

/// <summary>
/// Interface for currency providers that interact with external APIs to fetch exchange rates and perform currency conversions.
/// </summary>
public interface ICurrencyProvider
{
    /// <summary>
    /// Gets the latest exchange rates for a given base currency.
    /// </summary>
    /// <param name="baseCurrency"></param>
    /// <returns></returns>
    Task<ExchangeRateResponse> GetLatestRatesAsync(string baseCurrency);

    /// <summary>
    /// Converts an amount from one currency to another using the provider's API.
    /// </summary>
    /// <param name="fromCurrency"></param>
    /// <param name="toCurrency"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    Task<ExchangeRateResponse> ConvertCurrencyAsync(string fromCurrency, string toCurrency, decimal amount);

    /// <summary>
    /// Gets historical exchange rates for a given base currency within a specified date range.
    /// </summary>
    /// <param name="baseCurrency"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    Task<PagedHistoricalRatesResponse> GetHistoricalRatesAsync(string baseCurrency, DateTime startDate, DateTime endDate, int page, int pageSize);
}