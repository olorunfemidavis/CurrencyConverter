using System.Text.Json.Serialization;

namespace CurrencyConverter.Domain.DTOs;

public record PagedHistoricalRatesResponse
{
    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    /// <summary>
    /// Base currency code for the exchange rates (e.g., "USD", "EUR").
    /// </summary>
    [JsonPropertyName("base")]
    public string Base { get; init; } = null!;

    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; init; }

    [JsonPropertyName("end_date")]
    public DateTime EndDate { get; init; }

    /// <summary>
    /// Dictionary of rates where the key is the date in "yyyy-MM-dd" format. The value is another dictionary of currency rates
    /// </summary>
    [JsonPropertyName("rates")]
    public Dictionary<DateTime, Dictionary<string, decimal>> Rates { get; init; } = new();


    //Extra properties for pagination
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalRecords { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
}