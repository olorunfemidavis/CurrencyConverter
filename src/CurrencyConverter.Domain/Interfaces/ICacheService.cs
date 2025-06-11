namespace CurrencyConverter.Domain.Interfaces;

/// <summary>
/// Interface for caching services.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    /// <param name="key"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task<T?> GetAsync<T>(string key) where T : class;
    
    /// <summary>
    /// Sets a value in the cache with a specified key and expiry time.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="expiry"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task SetAsync<T>(string key, T value, TimeSpan expiry) where T : class;
}