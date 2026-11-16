namespace Altinn.Platform.Events.Configuration;

/// <summary>
/// Configuration object used to hold settings for the EventsOutbound function and related services.
/// </summary>
public class EventsOutboundSettings
{
    /// <summary>
    /// The number of seconds the event push http client will wait for a response before timing out.
    /// </summary>
    public int RequestTimeout { get; set; } = 45;

    /// <summary>
    /// Used to flag whether Maskinporten authentication should be available on webhook http requests
    /// </summary>
    public bool EnableWebhookAuthentication { get; set; } = false;
}
