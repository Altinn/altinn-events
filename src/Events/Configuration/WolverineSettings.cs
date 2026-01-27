namespace Altinn.Platform.Events.Configuration;

/// <summary>
/// Represents settings for configuring Wolverine integration.
/// </summary>
public class WolverineSettings
{
    /// <summary>
    /// Connection string for Azure Service Bus.
    /// </summary>
    public string AzureServiceBusConnectionString { get; set; }

    /// <summary>
    /// Number of listeners to be used against Azure Service Bus queues (per pod).
    /// </summary>
    public int ListenerCount { get; set; }
}
