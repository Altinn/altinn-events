namespace Altinn.Platform.Events.Configuration;

/// <summary>
/// Represents settings for configuring Wolverine integration.
/// </summary>
public class WolverineSettings
{
    /// <summary>
    /// Enables the Azure Service Bus publisher for the registration event, instead of the legacy Storage Queue.
    /// </summary>
    public bool EnableRegistrationPublisher { get; set; } = false;

    /// <summary>
    /// Enables the Azure Service Bus publisher for the subscription validation event, instead of the legacy Storage Queue.
    /// </summary>
    public bool EnableValidationPublisher { get; set; } = false;

    /// <summary>
    /// Enables the Wolverine listener on the registration queue.
    /// </summary>
    public bool EnableRegistrationListener { get; set; } = false;

    /// <summary>
    /// Enables the Wolverine listener on the inbound queue.
    /// </summary>
    public bool EnableInboundListener { get; set; } = false;

    /// <summary>
    /// Enables the Wolverine listener on the outbound queue.
    /// </summary>
    public bool EnableOutboundListener { get; set; } = false;

    /// <summary>
    /// Enables the Wolverine listener on the subscription validation queue.
    /// </summary>
    public bool EnableValidationListener { get; set; } = false;

    /// <summary>
    /// True when any publisher or listener flag is enabled. Drives whether Wolverine's Azure Service Bus
    /// transport is configured at all.
    /// </summary>
    public bool IsAzureServiceBusEnabled =>
        EnableRegistrationPublisher || EnableValidationPublisher ||
        EnableRegistrationListener || EnableInboundListener ||
        EnableOutboundListener || EnableValidationListener;

    /// <summary>
    /// Connection string for Azure Service Bus.
    /// </summary>
    public string ServiceBusConnectionString { get; set; }

    /// <summary>
    /// Azure Service Bus queue name for event registration.
    /// </summary>
    public string RegistrationQueueName { get; set; }

    /// <summary>
    /// Number of listeners to be used against the registration queue (per pod). Kept low by
    /// default: this queue's listener competes with the registration HTTP endpoint for the same
    /// process's CPU/DB-connection/thread-pool capacity, and perf testing showed registration
    /// throughput drops sharply as this count rises (see perftesting/ for the load-test results).
    /// </summary>
    public int RegistrationListenerCount { get; set; } = 1;

    /// <summary>
    /// Retry policy configuration for the registration queue.
    /// </summary>
    public QueueRetryPolicy RegistrationQueuePolicy { get; set; } = new();

    /// <summary>
    /// Azure Service Bus queue name for event Validation.
    /// </summary>
    public string ValidationQueueName { get; set; }

    /// <summary>
    /// Number of listeners to be used against the validation queue (per pod), when it's hosted
    /// here rather than in Events.Functions.
    /// </summary>
    public int ValidationListenerCount { get; set; } = 5;

    /// <summary>
    /// Retry policy configuration for the validation queue.
    /// </summary>
    public QueueRetryPolicy ValidationQueuePolicy { get; set; } = new();

    /// <summary>
    /// Azure Service Bus queue name for event inbound.
    /// </summary>
    public string InboundQueueName { get; set; }

    /// <summary>
    /// Number of listeners to be used against the inbound queue (per pod).
    /// </summary>
    public int InboundListenerCount { get; set; } = 5;

    /// <summary>
    /// Retry policy configuration for the inbound queue.
    /// </summary>
    public QueueRetryPolicy InboundQueuePolicy { get; set; } = new();

    /// <summary>
    /// Azure Service Bus queue name for event outbound.
    /// </summary>
    public string OutboundQueueName { get; set; }

    /// <summary>
    /// Number of listeners to be used against the outbound queue (per pod), when it's hosted
    /// here rather than in Events.Functions.
    /// </summary>
    public int OutboundListenerCount { get; set; } = 5;

    /// <summary>
    /// Retry policy configuration for the outbound queue.
    /// </summary>
    public QueueRetryPolicy OutboundQueuePolicy { get; set; } = new();
}
