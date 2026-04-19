namespace OpsDash.Application.DTOs.Common;

/// <summary>
/// Wraps an API payload with cache metadata for response headers (e.g. X-Cache).
/// </summary>
public sealed class CachedApiResponse<T>
{
    public ApiResponse<T> Response { get; init; } = null!;

    public bool FromCache { get; init; }
}
