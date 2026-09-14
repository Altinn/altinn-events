using Altinn.Platform.Storage.Interface.Models;

using EventCreator.Clients;
using EventCreator.ConsoleSupport;

namespace EventCreator.Workflows;

/// <summary>
/// Fetches an instance and prints its key details, confirmations, and event history.
/// </summary>
public class AnalyzeInstanceWorkflow(StorageClient storageClient, EventsClient eventsClient)
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

        Console.WriteLine();
        Console.WriteLine($"  Id:           {instance.Id}");
        Console.WriteLine($"  App:          {instance.AppId}");
        Console.WriteLine($"  Created:      {ConsoleFormatter.ToLocal(instance.Created)}");
        Console.WriteLine($"  Last changed: {ConsoleFormatter.ToLocal(instance.LastChanged)}");
        Console.WriteLine($"  Process Step: {instance.Process?.CurrentTask?.ElementId ?? "-"}");
        Console.WriteLine($"  Archived:     {ConsoleFormatter.ToLocal(instance.Status?.Archived)}");

        Console.WriteLine();
        Console.WriteLine("  Confirmations:");

        if (instance.CompleteConfirmations is not null && instance.CompleteConfirmations.Count > 0)
        {
            foreach (var confirmation in instance.CompleteConfirmations)
            {
                Console.WriteLine($"    [{ConsoleFormatter.ToLocal(confirmation.ConfirmedOn)}] {confirmation.StakeholderId}");
            }
        }
        else
        {
            Console.WriteLine("    No confirmations");
        }

        Console.WriteLine();
        Console.WriteLine("  Events:");
        await ConsoleFormatter.PrintInstanceEvents(eventsClient, instance);

        return instance;
    }
}
