using Altinn.Platform.Storage.Interface.Models;

using EventCreator.Workflows;

namespace EventCreator.Menu;

/// <summary>
/// Drives the interactive menu loop, dispatching to the individual workflows.
/// </summary>
public class InteractiveMenu(
    AnalyzeInstanceWorkflow analyzeInstanceWorkflow,
    CompareInstancesWorkflow compareInstancesWorkflow,
    GenerateEventWorkflow generateEventWorkflow)
{
    public async Task Run()
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
                    currentInstance = await analyzeInstanceWorkflow.Run(currentInstance);
                    break;
                case "2":
                    currentInstance = await compareInstancesWorkflow.Run(currentInstance);
                    break;
                case "3":
                    await generateEventWorkflow.Run(currentInstance);
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
}
