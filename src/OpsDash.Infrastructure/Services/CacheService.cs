using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OpsDash.Application.Interfaces;

namespace OpsDash.Infrastructure.Services;

public sealed class CacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly ConcurrentDictionary<string, byte> RegisteredKeys = new();

    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<CacheResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                return new CacheResult<T> { IsHit = false, Value = default };
            }

            var value = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
            return new CacheResult<T> { IsHit = true, Value = value };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache get failed for key {Key}", key);
            return new CacheResult<T> { IsHit = false, Value = default };
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = expiration ?? TimeSpan.FromMinutes(5),
            };

            await _cache.SetAsync(key, bytes, options, cancellationToken).ConfigureAwait(false);
            RegisteredKeys.TryAdd(key, 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache set failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            RegisteredKeys.TryRemove(key, out _);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache remove failed for key {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return;
        }

        try
        {
            var keys = RegisteredKeys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            if (keys.Count == 0)
            {
                _logger.LogDebug("Cache prefix remove: no tracked keys for prefix {Prefix}", prefix);
                return;
            }

            foreach (var key in keys)
            {
                await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache prefix remove failed for prefix {Prefix}", prefix);
        }
    }
}
