using Altinn.Platform.Storage.Interface.Models;

using EventCreator.Clients;

namespace EventCreator.ConsoleSupport;

/// <summary>
/// Formatting/output helpers shared by the interactive workflows.
/// </summary>
public static class ConsoleFormatter
{
    public static string ToLocal(DateTime? utc) => utc?.ToLocalTime().ToString() ?? "-";

    public static string ToLocalPrecise(DateTime utc) => utc.ToLocalTime().ToString("dd.MM.yyyy HH.mm.ss.fff");

    public static string FormatConfirmed(Instance instance) =>
        instance.CompleteConfirmations is { Count: > 0 } ? $"Yes ({instance.CompleteConfirmations.Count})" : "No";

    public static async Task PrintInstanceEvents(EventsClient eventsClient, Instance instance)
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
}
