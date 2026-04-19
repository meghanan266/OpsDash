namespace OpsDash.Application.Interfaces;

public interface ICacheService
{
    Task<CacheResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a cache lookup. <see cref="IsHit"/> is false when the key is missing or deserialization fails.
/// </summary>
public sealed class CacheResult<T>
{
    public bool IsHit { get; init; }

    public T? Value { get; init; }
}
