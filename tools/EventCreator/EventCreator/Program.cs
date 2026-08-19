using System.Reflection;

using Altinn.Platform.Storage.Configuration;
using Altinn.Platform.Storage.Interface.Models;

using EventCreator.Clients;
using EventCreator.Configuration;

using Microsoft.Extensions.Configuration;

bool batchMode = args.Contains("-b") || args.Contains("--batch");

var builder = new ConfigurationBuilder()
    .AddJsonFile($"appsettings.json", true, true)
    .AddUserSecrets(Assembly.GetExecutingAssembly());
var config = builder.Build();

QueueStorageSettings queueStorageSettings = new();
config.GetRequiredSection("QueueStorageSettings").Bind(queueStorageSettings);

StorageDbSettings postgreSqlSettings = new();
config.GetRequiredSection("StorageDbSettings").Bind(postgreSqlSettings);

GeneralSettings generalSettings = new();
config.GetRequiredSection("GeneralSettings").Bind(generalSettings);

EventsDbSettings eventsDbSettings = new();
config.GetRequiredSection("EventsDbSettings").Bind(eventsDbSettings);

EventsQueueClient eventsQueueClient = new(queueStorageSettings, generalSettings.SourceBaseAddress);
StorageClient storageClient = new(postgreSqlSettings.ConnectionString);
EventsClient eventsClient = new(eventsDbSettings.ConnectionString, eventsDbSettings.CommandTimeoutSeconds);

if (batchMode)
{
    using FileStream logStream = File.OpenWrite("log.txt");
    using StreamWriter logWriter = new(logStream);

    logWriter.WriteLine($"[{DateTime.Now}]: STARTING, reading instances.txt");

    var lines = File.ReadAllLines("instances.txt");
    for (var i = 0; i < lines.Length; i += 1)
    {
        var line = lines[i];
        Console.WriteLine($"Processing instance: {line}");

        logWriter.WriteLine($"[{DateTime.Now}]:[{line}]: Started processing, reading from Storage");

        Instance? instance = await storageClient.GetOne(Guid.Parse(line));

        if (instance is null)
        {
            logWriter.WriteLine($"[{DateTime.Now}]:[{line}]: Instance NOT FOUND, skipping");
            continue;
        }

        logWriter.WriteLine($"[{DateTime.Now}]:[{line}]: Instance FOUND, generating and sending event");

        //// await eventsQueueClient.AddEvent("app.instance.created", instance);
        //// await eventsQueueClient.AddEvent("app.instance.process.movedTo.Task_2", instance);
        //// await eventsQueueClient.AddEvent("app.instance.process.movedTo.Task_2Revisor", instance);
        //// await eventsQueueClient.AddEvent("app.instance.process.movedTo.Task_3", instance);
        //// await eventsQueueClient.AddEvent("app.instance.substatus.changed", instance);
        await eventsQueueClient.AddEvent("app.instance.process.completed", instance);

        logWriter.WriteLine($"[{DateTime.Now}]:[{line}]: Finished processing");
    }

    logWriter.WriteLine($"[{DateTime.Now}]: Finished processing");
}
else
{
    await RunInteractiveMenu(storageClient, eventsClient, eventsQueueClient);
}

static async Task RunInteractiveMenu(StorageClient storageClient, EventsClient eventsClient, EventsQueueClient eventsQueueClient)
{
    Instance? currentInstance = null;

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("=== EventCreator Menu ===");
        Console.WriteLine("1. Analyze app instance");
        Console.WriteLine("2. Compare with similar archived instances");
        Console.WriteLine($"3. Generate event for instance {(currentInstance is not null ? $" ({currentInstance.Id})" : string.Empty)}");
        Console.WriteLine("4. Exit");
        Console.Write("Select an option: ");

        string? input = Console.ReadLine();
        switch (input?.Trim())
        {
            case "1":
                currentInstance = await AnalyzeAppInstance(storageClient, eventsClient, currentInstance);
                break;
            case "2":
                currentInstance = await CompareSimilarInstances(storageClient, eventsClient, currentInstance);
                break;
            case "3":
                await GenerateEventForInstance(storageClient, eventsQueueClient, currentInstance);
                break;
            case "4":
                Console.WriteLine("Exiting...");
                return;
            default:
                Console.WriteLine("Invalid option. Please try again.");
                break;
        }
    }
}

static async Task GenerateEventForInstance(StorageClient storageClient, EventsQueueClient eventsQueueClient, Instance? currentInstance)
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

    const string defaultEventType = "app.instance.process.completed";
    Console.Write($"Enter event type [{defaultEventType}]: ");
    string? eventTypeInput = Console.ReadLine()?.Trim();
    string eventType = string.IsNullOrEmpty(eventTypeInput) ? defaultEventType : eventTypeInput;

    await using FileStream logStream = new("log.txt", FileMode.Append, FileAccess.Write);
    await using StreamWriter logWriter = new(logStream);

    logWriter.WriteLine($"[{DateTime.Now}]:[{instanceId}]: Generating and sending event of type '{eventType}'");

    await eventsQueueClient.AddEvent(eventType, instance);

    logWriter.WriteLine($"[{DateTime.Now}]:[{instanceId}]: Event sent");
    Console.WriteLine("Event generated successfully.");
}

static async Task<Instance?> AnalyzeAppInstance(StorageClient storageClient, EventsClient eventsClient, Instance? currentInstance)
{
    string previousGuid = currentInstance?.Id ?? string.Empty;
    string prompt = string.IsNullOrEmpty(previousGuid)
        ? "Enter instance GUID: "
        : $"Enter instance GUID [{previousGuid}]: ";

    Console.Write(prompt);
    string? guidInput = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(guidInput))
        guidInput = previousGuid;

    if (!Guid.TryParse(guidInput, out Guid instanceGuid))
    {
        Console.WriteLine("Invalid GUID format.");
        return null;
    }

    Console.WriteLine($"Fetching instance {instanceGuid}...");
    Instance? instance = await storageClient.GetOne(instanceGuid);

    if (instance is null)
    {
        Console.WriteLine("Instance not found.");
        return null;
    }

    Console.WriteLine();
    Console.WriteLine($"  Id:           {instance.Id}");
    Console.WriteLine($"  App:          {instance.AppId}");
    Console.WriteLine($"  Created:      {ToLocal(instance.Created)}");
    Console.WriteLine($"  Last changed: {ToLocal(instance.LastChanged)}");
    Console.WriteLine($"  Process Step: {instance.Process?.CurrentTask?.ElementId ?? "-"}");
    Console.WriteLine($"  Archived:     {ToLocal(instance.Status?.Archived)}");
    
    Console.WriteLine();
    Console.WriteLine("  Confirmations:");

    if (instance.CompleteConfirmations is not null && instance.CompleteConfirmations.Count > 0)
    {
        foreach (var confirmation in instance.CompleteConfirmations)
        {
            Console.WriteLine($"    [{ToLocal(confirmation.ConfirmedOn)}] {confirmation.StakeholderId}");
        }
    }
    else
    {
        Console.WriteLine("    No confirmations");
    }

    Console.WriteLine();
    Console.WriteLine("  Events:");
    await PrintInstanceEvents(eventsClient, instance);

    return instance;
}

static async Task<Instance?> CompareSimilarInstances(StorageClient storageClient, EventsClient eventsClient, Instance? currentInstance)
{
    string previousGuid = currentInstance?.Id ?? string.Empty;
    string prompt = string.IsNullOrEmpty(previousGuid)
        ? "Enter instance GUID: "
        : $"Enter instance GUID [{previousGuid}]: ";

    Console.Write(prompt);
    string? guidInput = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(guidInput))
        guidInput = previousGuid;

    if (!Guid.TryParse(guidInput, out Guid instanceGuid))
    {
        Console.WriteLine("Invalid GUID format.");
        return null;
    }

    Console.WriteLine($"Fetching instance {instanceGuid}...");
    Instance? instance = await storageClient.GetOne(instanceGuid);

    if (instance is null)
    {
        Console.WriteLine("Instance not found.");
        return null;
    }

    const int defaultLimit = 5;
    Console.Write($"How many similar archived instances to compare against [{defaultLimit}]: ");
    string? limitInput = Console.ReadLine()?.Trim();
    int limit = int.TryParse(limitInput, out int parsedLimit) && parsedLimit > 0 ? parsedLimit : defaultLimit;

    Console.WriteLine($"Looking for archived instances of '{instance.AppId}' similar to {instance.Id}...");
    List<Instance> similarInstances = await storageClient.GetSimilarArchivedInstances(instance.AppId, instanceGuid, limit);

    Console.WriteLine();
    Console.WriteLine($"  Target instance {instance.Id} (Process Step: {instance.Process?.CurrentTask?.ElementId ?? "-"}, Archived: {ToLocal(instance.Status?.Archived)}, Confirmed: {FormatConfirmed(instance)}):");
    await PrintInstanceEvents(eventsClient, instance);

    if (similarInstances.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("  No similar archived instances found for this app.");
        return instance;
    }

    foreach (Instance similar in similarInstances)
    {
        Console.WriteLine();
        Console.WriteLine($"  Similar archived instance {similar.Id} (Archived: {ToLocal(similar.Status?.Archived)}, Confirmed: {FormatConfirmed(similar)}):");
        await PrintInstanceEvents(eventsClient, similar);
    }

    return instance;
}

static string FormatConfirmed(Instance instance) =>
    instance.CompleteConfirmations is { Count: > 0 } ? $"Yes ({instance.CompleteConfirmations.Count})" : "No";

static async Task PrintInstanceEvents(EventsClient eventsClient, Instance instance)
{
    List<AppInstanceEvent> events = await eventsClient.GetInstanceEvents(instance);

    if (events.Count == 0)
    {
        Console.WriteLine("    No events found.");
        return;
    }

    foreach (AppInstanceEvent e in events)
    {
        Console.WriteLine($"    [{ToLocalPrecise(e.RegisteredTime)}] (seq {e.SequenceNo}) {e.EventType} ({e.EventId})");
    }
}

static string ToLocal(DateTime? utc) => utc?.ToLocalTime().ToString() ?? "-";

static string ToLocalPrecise(DateTime utc) => utc.ToLocalTime().ToString("dd.MM.yyyy HH.mm.ss.fff");
