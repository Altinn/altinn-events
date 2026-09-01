using Altinn.Platform.Storage.Interface.Models;

using EventCreator.Clients;

namespace EventCreator.Workflows;

/// <summary>
/// Generates and publishes an event for an instance.
/// </summary>
public class GenerateEventWorkflow(StorageClient storageClient, EventsQueueClient eventsQueueClient)
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

        await using FileStream logStream = new("log.txt", FileMode.Append, FileAccess.Write);
        await using StreamWriter logWriter = new(logStream);

        logWriter.WriteLine($"[{DateTime.Now}]:[{instanceId}]: Generating and sending event of type '{eventType}'");

        await eventsQueueClient.AddEvent(eventType, instance);

        logWriter.WriteLine($"[{DateTime.Now}]:[{instanceId}]: Event sent");
        Console.WriteLine("Event generated successfully.");
    }
}
