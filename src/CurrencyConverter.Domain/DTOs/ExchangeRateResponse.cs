using System.Text.Json.Serialization;

namespace CurrencyConverter.Domain.DTOs;

/// <summary>
/// Parseable response from the currency exchange rate provider.
/// </summary>
public record ExchangeRateResponse
{
    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    /// <summary>
    /// Base currency code for the exchange rates (e.g., "USD", "EUR").
    /// </summary>
    [JsonPropertyName("base")]
    public string Base { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; init; }
    
    /// <summary>
    /// Dictionary containing currency codes as keys and their corresponding exchange rates as values.
    /// </summary>
    [JsonPropertyName("rates")]
    public Dictionary<string, decimal> Rates { get; init; } = new();
}