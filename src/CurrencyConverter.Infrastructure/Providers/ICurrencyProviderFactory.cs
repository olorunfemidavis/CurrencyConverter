using CurrencyConverter.Domain.Interfaces;

namespace CurrencyConverter.Infrastructure.Providers;

public interface ICurrencyProviderFactory
{
    /// <summary>
    /// Creates an instance of ICurrencyProvider based on the provider name.
    /// </summary>
    /// <param name="providerName"></param>
    /// <returns></returns>
    ICurrencyProvider CreateProvider(string providerName);
}