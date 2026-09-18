namespace EventCreator.Configuration;

/// <summary>
/// Configuration object used to hold settings for Azure Service Bus.
/// </summary>
public class ServiceBusSettings
{
    /// <summary>
    /// Connection string for Azure Service Bus.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Azure Service Bus queue name for event registration.
    /// </summary>
    public string RegistrationQueueName { get; set; } = string.Empty;
}
