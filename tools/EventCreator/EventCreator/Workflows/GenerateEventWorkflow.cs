using Altinn.Platform.Storage.Interface.Models;

using CloudNative.CloudEvents;

using EventCreator.Clients;
using EventCreator.ConsoleSupport;
using EventCreator.Publishing;

namespace EventCreator.Workflows;

/// <summary>
/// Generates and publishes an event for an instance.
/// </summary>
public class GenerateEventWorkflow(StorageClient storageClient, IEventPublisher eventPublisher, string resourceBaseAddress)
{
    private const string DefaultEventType = "app.instance.process.completed";

    public async Task Run(Instance? currentInstance)
    {
        Instance? instance = currentInstance;

        if (instance is null)
        {
            Console.Write("Enter instance GUID: ");
            string? guidInput = Console.ReadLine();

            if (!Guid.TryParse(guidInput?.Trim(), out Guid instanceGuid))
            {
                Console.WriteLine("Invalid GUID format.");
                return;
            }

            instance = await storageClient.GetOne(instanceGuid);

            if (instance is null)
            {
                Console.WriteLine("Instance not found.");
                return;
            }
        }

        string instanceId = instance.Id;

        Console.Write($"Enter event type [{DefaultEventType}]: ");
        string? eventTypeInput = Console.ReadLine()?.Trim();
        string eventType = string.IsNullOrEmpty(eventTypeInput) ? DefaultEventType : eventTypeInput;

        if (!ConsoleInput.Confirm($"This will generate a '{eventType}' event for instance {instanceId}. Proceed?"))
        {
            Console.WriteLine("Cancelled. No event was generated.");
            return;
        }

        await using FileStream logStream = new("log.txt", FileMode.Append, FileAccess.Write);
        await using StreamWriter logWriter = new(logStream);

        logWriter.WriteLine($"[{DateTime.Now}]:[{instanceId}]: Generating and sending event of type '{eventType}'");

        CloudEvent cloudEvent = CloudEventFactory.Create(eventType, instance, resourceBaseAddress);
        await eventPublisher.PublishAsync(cloudEvent);

        logWriter.WriteLine($"[{DateTime.Now}]:[{instanceId}]: Event sent");
        Console.WriteLine("Event generated successfully.");
    }
}
