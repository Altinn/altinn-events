using Altinn.Platform.Storage.Interface.Models;

using EventCreator.Clients;
using EventCreator.ConsoleSupport;

namespace EventCreator.Workflows;

/// <summary>
/// Fetches an instance and compares its event history against similar archived instances of the same app.
/// </summary>
public class CompareInstancesWorkflow(StorageClient storageClient, EventsClient eventsClient)
{
    public async Task<Instance?> Run(Instance? currentInstance)
    {
        string previousGuid = currentInstance?.Id ?? string.Empty;

        if (!ConsoleInput.TryReadInstanceGuid(previousGuid, out Guid instanceGuid))
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

        int limit = ConsoleInput.ReadInt("How many archived instances to compare against", 3, v => v > 0);
        int minDaysSinceArchived = ConsoleInput.ReadInt(
            "Minimum days since archived, to avoid instances still waiting on a third party to confirm",
            1,
            v => v >= 0);
        DateTime archivedBefore = DateTime.UtcNow.AddDays(-minDaysSinceArchived);

        Console.WriteLine($"Looking for the latest {limit} archived instances of '{instance.AppId}' excluding {instance.Id}, archived before {ConsoleFormatter.ToLocal(archivedBefore)}...");
        List<Instance> similarInstances = await storageClient.GetSimilarArchivedInstances(instance.AppId, instanceGuid, limit, archivedBefore);

        Console.WriteLine();
        Console.WriteLine($"  Target instance {instance.Id} (Process Step: {instance.Process?.CurrentTask?.ElementId ?? "-"}, Archived: {ConsoleFormatter.ToLocal(instance.Status?.Archived)}, Confirmed: {ConsoleFormatter.FormatConfirmed(instance)}):");
        await ConsoleFormatter.PrintInstanceEvents(eventsClient, instance);

        if (similarInstances.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("  No similar archived instances found for this app.");
            return instance;
        }

        foreach (Instance similar in similarInstances)
        {
            Console.WriteLine();
            Console.WriteLine($"  Similar archived instance {similar.Id} (Archived: {ConsoleFormatter.ToLocal(similar.Status?.Archived)}, Confirmed: {ConsoleFormatter.FormatConfirmed(similar)}):");
            await ConsoleFormatter.PrintInstanceEvents(eventsClient, similar);
        }

        return instance;
    }
}
