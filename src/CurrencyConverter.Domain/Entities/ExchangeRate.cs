namespace CurrencyConverter.Domain.Entities;

public record ExchangeRate
{
    public string BaseCurrency { get; init; } = null!;
    public DateTime Date { get; init; }
    public Dictionary<string, decimal> Rates { get; init; } = new();
}