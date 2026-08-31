using CloudNative.CloudEvents;

namespace EventCreator.Publishing;

/// <summary>
/// Publishes a <see cref="CloudEvent"/> to the event registration transport.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(CloudEvent cloudEvent);
}
