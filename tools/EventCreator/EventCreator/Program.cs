using System.Reflection;

using Altinn.Platform.Storage.Configuration;
using Altinn.Platform.Storage.Interface.Models;

using EventCreator.Clients;
using EventCreator.Configuration;
using EventCreator.Menu;
using EventCreator.Workflows;

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
    AnalyzeInstanceWorkflow analyzeInstanceWorkflow = new(storageClient, eventsClient);
    CompareInstancesWorkflow compareInstancesWorkflow = new(storageClient, eventsClient);
    GenerateEventWorkflow generateEventWorkflow = new(storageClient, eventsQueueClient);

    InteractiveMenu menu = new(analyzeInstanceWorkflow, compareInstancesWorkflow, generateEventWorkflow);
    await menu.Run();
}
