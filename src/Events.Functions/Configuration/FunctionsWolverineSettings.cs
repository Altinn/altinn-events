using Altinn.Platform.Events.Functions.Wolverine.Configuration;

namespace Altinn.Platform.Events.Functions.Configuration;

/// <summary>
/// Represents settings for configuring Wolverine integration in the Functions app.
/// Deliberately slim compared to the main Events API's WolverineSettings — this app
/// only ever needs the outbound and subscription-validation queues.
/// </summary>
public class FunctionsWolverineSettings
{
    /// <summary>
    /// Enables the Wolverine listener on the outbound queue.
    /// </summary>
    public bool EnableOutboundListener { get; set; } = false;

    /// <summary>
    /// Enables the Wolverine listener on the subscription validation queue.
    /// </summary>
    public bool EnableValidationListener { get; set; } = false;

    /// <summary>
    /// True when either listener flag is enabled. Drives whether Wolverine's Azure Service Bus
    /// transport is configured at all.
    /// </summary>
    public bool IsAzureServiceBusEnabled => EnableOutboundListener || EnableValidationListener;

    /// <summary>
    /// Connection string for Azure Service Bus.
    /// </summary>
    public string? ServiceBusConnectionString { get; set; }

    /// <summary>
    /// Number of listeners to be used against Azure Service Bus queues (per pod).
    /// </summary>
    public int ListenerCount { get; set; } = 20;

    /// <summary>
    /// Azure Service Bus queue name for event outbound. Same value in every environment,
    /// so it's a compiled-in default rather than something each environment must configure —
    /// still override-able via configuration if ever needed.
    /// </summary>
    public string OutboundQueueName { get; set; } = "altinn.events.outbound";

    /// <summary>
    /// Retry policy configuration for the outbound queue.
    /// </summary>
    public QueueRetryPolicy OutboundQueuePolicy { get; set; } = new()
    {
        CooldownDelaysMs = [10000],
        ScheduleDelaysMs = [30000, 60000, 300000, 600000, 1800000, 3600000, 10800000, 21600000, 43200000, 43200000]
    };

    /// <summary>
    /// Azure Service Bus queue name for event validation. Same value in every environment,
    /// so it's a compiled-in default rather than something each environment must configure —
    /// still override-able via configuration if ever needed.
    /// </summary>
    public string ValidationQueueName { get; set; } = "altinn.events.subscription.validation";

    /// <summary>
    /// Retry policy configuration for the validation queue.
    /// </summary>
    public QueueRetryPolicy ValidationQueuePolicy { get; set; } = new()
    {
        CooldownDelaysMs = [1000, 5000, 10000],
        ScheduleDelaysMs = [30000, 60000, 120000, 120000, 120000]
    };
}
