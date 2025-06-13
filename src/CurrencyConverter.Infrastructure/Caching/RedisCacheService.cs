using CurrencyConverter.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Infrastructure.Caching;

/// <summary>
/// RedisCacheService is an implementation of ICacheService that uses Redis for caching.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    /// <param name="key"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var value = await _cache.GetStringAsync(key);
        if (value == null)
            return null;

        _logger.LogDebug("Retrieved from cache: {Key}", key);
        return JsonSerializer.Deserialize<T>(value);
    }

    /// <summary>
    /// Sets a value in the cache with a specified key and expiry time.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="expiry"></param>
    /// <typeparam name="T"></typeparam>
    public async Task SetAsync<T>(string key, T value, TimeSpan expiry) where T : class
    {
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry };
        var serialized = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, serialized, options);
        _logger.LogDebug("Cached {Key} with expiry {Expiry}", key, expiry);
    }
}