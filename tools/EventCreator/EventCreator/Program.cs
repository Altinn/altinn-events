using System.Reflection;

using Altinn.Platform.Storage.Configuration;
using Altinn.Platform.Storage.Interface.Models;

using EventCreator.Clients;
using EventCreator.Configuration;

using Microsoft.Extensions.Configuration;

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

EventsQueueClient eventsQueueClient = new(queueStorageSettings, generalSettings.SourceBaseAddress);
PgClient pgClient = new(postgreSqlSettings.ConnectionString);

using FileStream logStream = File.OpenWrite("log.txt");
using StreamWriter logWriter = new(logStream);

logWriter.WriteLine($"[{DateTime.Now}]: STARTING, reading instances.txt");

var lines = File.ReadAllLines("instances.txt");
for (var i = 0; i < lines.Length; i += 1)
{
    var line = lines[i];
    Console.WriteLine($"Processing instance: {line}");

    logWriter.WriteLine($"[{DateTime.Now}]:[{line}]: Started processing, reading from Storage");

    Instance? instance = await pgClient.GetOne(Guid.Parse(line));

    if (instance is null)
    {
        logWriter.WriteLine($"[{DateTime.Now}]:[{line}]: Instance NOT FOUND, skipping");
        continue;
    }

    logWriter.WriteLine($"[{DateTime.Now}]:[{line}]: Instance FOUND, generating and sending event");

    //// await eventsQueueClient.AddEvent("app.instance.process.movedTo.Task_2", instance);
    //// await eventsQueueClient.AddEvent("app.instance.process.movedTo.Task_2Revisor", instance);
    //// await eventsQueueClient.AddEvent("app.instance.process.movedTo.Task_3", instance);
    //// await eventsQueueClient.AddEvent("app.instance.substatus.changed", instance);
    await eventsQueueClient.AddEvent("app.instance.process.completed", instance);

    logWriter.WriteLine($"[{DateTime.Now}]:[{line}]: Finished processing");
}

logWriter.WriteLine($"[{DateTime.Now}]: Finished processing");
