namespace EventCreator.Configuration;

/// <summary>
/// Selects which transport <see cref="EventCreator"/> uses to publish events.
/// </summary>
public enum PublishMode
{
    AzureStorageQueue,
    AzureServiceBus
}

public class PublishSettings
{
    public PublishMode Mode { get; set; } = PublishMode.AzureStorageQueue;
}
