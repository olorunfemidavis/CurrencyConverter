using CurrencyConverter.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CurrencyConverter.Infrastructure.Providers;

/// <summary>
/// CurrencyProviderFactory is responsible for creating instances of ICurrencyProvider.
/// </summary>
public class CurrencyProviderFactory : ICurrencyProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public CurrencyProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Provides an instance of ICurrencyProvider based on the provider name.
    /// </summary>
    /// <param name="providerName"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public ICurrencyProvider CreateProvider(string providerName) =>
        providerName.ToLower() switch
        {
            "frankfurter" => _serviceProvider.GetRequiredService<FrankfurterProvider>(),
            _ => throw new NotSupportedException($"Provider {providerName} not supported.")
        };
}