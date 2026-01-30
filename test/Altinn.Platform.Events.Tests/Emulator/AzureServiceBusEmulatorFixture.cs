using System;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit;

namespace Altinn.Platform.Events.Tests.Emulator;

/// <summary>
/// xUnit fixture that starts Azure Service Bus Emulator with MSSQL 2022 using Testcontainers.
/// This fixture is shared across all tests in the collection to avoid starting/stopping containers repeatedly.
/// </summary>
public class AzureServiceBusEmulatorFixture : IAsyncLifetime
{
    private INetwork _network;
    private IContainer _mssqlContainer;
    private IContainer _serviceBusEmulatorContainer;

    /// <summary>
    /// Gets the Azure Service Bus connection string for the emulator.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the emulator is running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Initializes the fixture by starting MSSQL 2022 and Azure Service Bus Emulator containers.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Configure Testcontainers to not use the Resource Reaper (cleanup helper)
            // This avoids timeout issues with the Ryuk container in some Docker environments
            TestcontainersSettings.ResourceReaperEnabled = false;

            // Create a dedicated network for the containers
            _network = new NetworkBuilder()
                .WithName($"asb-test-network-{Guid.NewGuid():N}")
                .Build();

            await _network.CreateAsync();

            // Start MSSQL 2022 container first (dependency for Service Bus Emulator)
            _mssqlContainer = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .WithNetwork(_network)
                .WithNetworkAliases("mssql")
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("MSSQL_SA_PASSWORD", "YourStrong!Passw0rd")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
                .WithAutoRemove(true) // Automatically remove container when stopped
                .Build();

            await _mssqlContainer.StartAsync();

            // Get the path to the emulator config file (copied from emulator/config.json at build time)
            string configPath = Path.Combine(AppContext.BaseDirectory, "Emulator", "config.json");

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Emulator config file not found at: {configPath}");
            }

            // Start Service Bus Emulator container
            _serviceBusEmulatorContainer = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
                .WithNetwork(_network)
                .WithEnvironment("SQL_SERVER", "mssql")
                .WithEnvironment("MSSQL_SA_PASSWORD", "YourStrong!Passw0rd")
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("SQL_WAIT_INTERVAL", "5") // Reduce SQL wait time from default 15s to 5s
                .WithBindMount(configPath, "/ServiceBus_Emulator/ConfigFiles/Config.json", AccessMode.ReadOnly)
                .WithPortBinding(5672, true) // Bind to a random host port to avoid conflicts
                .WithAutoRemove(true) // Automatically remove container when stopped
                .WithStartupCallback((container, ct) =>
                {
                    // Log that emulator is starting
                    Console.WriteLine("Azure Service Bus Emulator container started, waiting for initialization...");
                    return Task.CompletedTask;
                })
                .Build();

            await _serviceBusEmulatorContainer.StartAsync();

            // Note: No additional wait strategy needed. The emulator is ready after StartAsync() completes
            // due to SQL_WAIT_INTERVAL handling the internal initialization timing.

            // Get the dynamically assigned host port
            int hostPort = _serviceBusEmulatorContainer.GetMappedPublicPort(5672);

            // Connection string for the emulator
            ConnectionString = $"Endpoint=sb://127.0.0.1:{hostPort};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
            IsRunning = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start Azure Service Bus Emulator: {ex.Message}");
            IsRunning = false;
            throw;
        }
    }

    /// <summary>
    /// Disposes the fixture by stopping and removing the Azure Service Bus Emulator containers.
    /// </summary>
    public async Task DisposeAsync()
    {
        try
        {
            if (_serviceBusEmulatorContainer != null)
            {
                await _serviceBusEmulatorContainer.StopAsync();
                await _serviceBusEmulatorContainer.DisposeAsync();
            }

            if (_mssqlContainer != null)
            {
                await _mssqlContainer.StopAsync();
                await _mssqlContainer.DisposeAsync();
            }

            if (_network != null)
            {
                await _network.DeleteAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during cleanup: {ex.Message}");
        }
        finally
        {
            // Restore Resource Reaper to default (enabled) to avoid affecting other tests
            TestcontainersSettings.ResourceReaperEnabled = true;
            IsRunning = false;
        }
    }
}

/// <summary>
/// xUnit collection definition for tests that require the Azure Service Bus Emulator.
/// All tests in this collection will share the same emulator instance.
/// </summary>
[CollectionDefinition(nameof(AzureServiceBusEmulatorCollection))]
public class AzureServiceBusEmulatorCollection : ICollectionFixture<AzureServiceBusEmulatorFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
