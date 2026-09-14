using System.Reflection;

using Altinn.Platform.Storage.Configuration;
using Altinn.Platform.Storage.Interface.Models;

using CloudNative.CloudEvents;

using EventCreator.Clients;
using EventCreator.Configuration;
using EventCreator.Menu;
using EventCreator.Publishing;
using EventCreator.Workflows;

using Microsoft.Extensions.Configuration;

bool batchMode = args.Contains("-b") || args.Contains("--batch");

var builder = new ConfigurationBuilder()
    .AddJsonFile($"appsettings.json", true, true)
    .AddUserSecrets(Assembly.GetExecutingAssembly());
var config = builder.Build();

StorageDbSettings postgreSqlSettings = new();
config.GetRequiredSection("StorageDbSettings").Bind(postgreSqlSettings);

GeneralSettings generalSettings = new();
config.GetRequiredSection("GeneralSettings").Bind(generalSettings);

EventsDbSettings eventsDbSettings = new();
config.GetRequiredSection("EventsDbSettings").Bind(eventsDbSettings);

QueueStorageSettings queueStorageSettings = new();
config.GetRequiredSection("QueueStorageSettings").Bind(queueStorageSettings);

ServiceBusSettings serviceBusSettings = new();
config.GetSection("ServiceBusSettings").Bind(serviceBusSettings);

PublishSettings publishSettings = new();
config.GetSection("PublishSettings").Bind(publishSettings);

StorageClient storageClient = new(postgreSqlSettings.ConnectionString);
EventsClient eventsClient = new(eventsDbSettings.ConnectionString, eventsDbSettings.CommandTimeoutSeconds);

IEventPublisher eventPublisher = publishSettings.Mode switch
{
    PublishMode.AzureStorageQueue => new AzureStorageQueueEventPublisher(queueStorageSettings),
    PublishMode.AzureServiceBus => new AzureServiceBusEventPublisher(serviceBusSettings),
    _ => throw new InvalidOperationException($"Unsupported PublishSettings.Mode: '{publishSettings.Mode}'."),
};

async Task PublishEvent(string eventType, Instance instance)
{
    CloudEvent cloudEvent = CloudEventFactory.Create(eventType, instance, generalSettings.SourceBaseAddress);
    await eventPublisher.PublishAsync(cloudEvent);
}

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

        //// await PublishEvent("app.instance.created", instance);
        //// await PublishEvent("app.instance.process.movedTo.Task_2", instance);
        //// await PublishEvent("app.instance.process.movedTo.Task_2Revisor", instance);
        //// await PublishEvent("app.instance.process.movedTo.Task_3", instance);
        //// await PublishEvent("app.instance.substatus.changed", instance);
        await PublishEvent("app.instance.process.completed", instance);

        logWriter.WriteLine($"[{DateTime.Now}]:[{line}]: Finished processing");
    }

    logWriter.WriteLine($"[{DateTime.Now}]: Finished processing");
}
else
{
    AnalyzeInstanceWorkflow analyzeInstanceWorkflow = new(storageClient, eventsClient);
    CompareInstancesWorkflow compareInstancesWorkflow = new(storageClient, eventsClient);
    GenerateEventWorkflow generateEventWorkflow = new(storageClient, eventPublisher, generalSettings.SourceBaseAddress);

    InteractiveMenu menu = new(analyzeInstanceWorkflow, compareInstancesWorkflow, generateEventWorkflow);
    await menu.Run();
}

if (eventPublisher is IAsyncDisposable disposablePublisher)
{
    await disposablePublisher.DisposeAsync();
}
