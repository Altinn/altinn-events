namespace Altinn.Platform.Events.Configuration;

/// <summary>
/// Represents a set of configuration options when communicating with the platform API.
/// Instances of this class is initialised with values from app settings. Some values can be overridden by environment variables.
/// </summary>
public class PlatformSettings
{
    /// <summary>
    /// Gets or sets the base address for the Register API.
    /// </summary>
    public string RegisterApiBaseAddress { get; set; }

    /// <summary>
    /// Gets or sets the size of the urn list we send to the Register API for party lookup.
    /// Default value is 100.
    /// </summary>
    public int RegisterApiChunkSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the url for the Profile API endpoint
    /// </summary>
    public string ApiProfileEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the apps domain used to match events source
    /// </summary>
    public string AppsDomain { get; set; }

    /// <summary>
    /// The lifetime to cache subscriptions
    /// </summary>
    public int SubscriptionCachingLifetimeInSeconds { get; set; }
}
