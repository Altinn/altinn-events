namespace Altinn.Platform.Events.Models;

/// <summary>
/// Represents a cache key associated with a specific consumer for authorization decisions.
/// </summary>
public class ConsumerCacheMap
{
    /// <summary>
    /// Gets or sets the consumer identifier.
    /// </summary>
    public string Consumer { get; set; }

    /// <summary>
    /// Gets or sets the cache key used for storing/retrieving authorization decisions.
    /// </summary>
    public string CacheKey { get; set; }
}
