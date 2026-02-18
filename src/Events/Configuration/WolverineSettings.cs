namespace Altinn.Platform.Events.Configuration;

/// <summary>
/// Represents settings for configuring Wolverine integration.
/// </summary>
public class WolverineSettings
{
    /// <summary>
    /// Indicates whether Azure Service Bus should be configured.
    /// </summary>
    public bool EnableServiceBus { get; set; } = false;

    /// <summary>
    /// Connection string for Azure Service Bus.
    /// </summary>
    public string ServiceBusConnectionString { get; set; }

    /// <summary>
    /// Number of listeners to be used against Azure Service Bus queues (per pod).
    /// </summary>
    public int ListenerCount { get; set; }

    /// <summary>
    /// Azure Service Bus queue name for event registration.
    /// </summary>
    public string RegistrationQueueName { get; set; }

    /// <summary>
    /// Retry policy configuration for the registration queue.
    /// </summary>
    public QueueRetryPolicy RegistrationQueuePolicy { get; set; } = new();

    /// <summary>
    /// Azure Service Bus queue name for event Validation.
    /// </summary>
    public string ValidationQueueName { get; set; }

    /// <summary>
    /// Retry policy configuration for the validation queue.
    /// </summary>
    public QueueRetryPolicy ValidationQueuePolicy { get; set; } = new();

    /// <summary>
    /// Azure Service Bus queue name for event inbound.
    /// </summary>
    public string InboundQueueName { get; set; }

    /// <summary>
    /// Retry policy configuration for the inbound queue.
    /// </summary>
    public QueueRetryPolicy InboundQueuePolicy { get; set; } = new();

    /// <summary>
    /// Azure Service Bus queue name for event outbound.
    /// </summary>
    public string OutboundQueueName { get; set; }

    /// <summary>
    /// Retry policy configuration for the outbound queue.
    /// </summary>
    public QueueRetryPolicy OutboundQueuePolicy { get; set; } = new();
}
