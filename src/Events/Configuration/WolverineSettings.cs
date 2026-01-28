namespace Altinn.Platform.Events.Configuration;

/// <summary>
/// Represents settings for configuring Wolverine integration.
/// </summary>
public class WolverineSettings
{
    /// <summary>
    /// Indicates whether Azure Service Bus should be configured.
    /// </summary>
    public bool EnableServiceBus { get; set; } = true;

    /// <summary>
    /// Connection string for Azure Service Bus.
    /// </summary>
    public string ServiceBusConnectionString { get; set; }

    /// <summary>
    /// Number of listeners to be used against Azure Service Bus queues (per pod).
    /// </summary>
    public int ListenerCount { get; set; }
}
