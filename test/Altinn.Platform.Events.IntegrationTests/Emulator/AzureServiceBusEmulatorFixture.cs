#nullable enable
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.Emulator;

/// <summary>
/// xUnit fixture that starts Azure Service Bus Emulator with MSSQL 2022 using Testcontainers.
/// This fixture is shared across all tests in the collection to avoid starting/stopping containers repeatedly.
/// </summary>
public class AzureServiceBusEmulatorFixture : IAsyncLifetime
{
    private INetwork? _network;
    private IContainer? _mssqlContainer;
    private IContainer? _serviceBusEmulatorContainer;

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
            _network = new NetworkBuilder()
                .WithName($"asb-test-network-{Guid.NewGuid():N}")
                .Build();

            await _network.CreateAsync();

            _mssqlContainer = new ContainerBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                .WithNetwork(_network)
                .WithNetworkAliases("mssql")
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("MSSQL_SA_PASSWORD", "YourStrong!Passw0rd")
                .WithAutoRemove(true)
                .Build();

            await _mssqlContainer.StartAsync();

            string configPath = Path.Combine(AppContext.BaseDirectory, "Emulator", "config.json");

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Emulator config file not found at: {configPath}");
            }

            _serviceBusEmulatorContainer = new ContainerBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
                .WithNetwork(_network)
                .WithEnvironment("SQL_SERVER", "mssql")
                .WithEnvironment("MSSQL_SA_PASSWORD", "YourStrong!Passw0rd")
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("SQL_WAIT_INTERVAL", "5")
                .WithBindMount(configPath, "/ServiceBus_Emulator/ConfigFiles/Config.json", AccessMode.ReadOnly)
                .WithPortBinding(5672, true)
                .WithAutoRemove(true)
                .Build();

            await _serviceBusEmulatorContainer.StartAsync();

            int hostPort = _serviceBusEmulatorContainer.GetMappedPublicPort(5672);

            ConnectionString = $"Endpoint=sb://127.0.0.1:{hostPort};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

            // Wait for the emulator to be fully ready to accept connections
            await WaitUntilReadyAsync();

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
    /// Waits for the emulator to be fully ready by attempting to connect with retries.
    /// The container being "started" doesn't mean the AMQP listener is ready.
    /// </summary>
    private async Task WaitUntilReadyAsync()
    {
        const int maxRetries = 30;
        const int delayMs = 1000;
        int hostPort = _serviceBusEmulatorContainer!.GetMappedPublicPort(5672);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                // Simple TCP socket check - verify the AMQP port is accepting connections
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", hostPort);

                Console.WriteLine($"Emulator ready after {i + 1} attempt(s)");
                return;
            }
            catch (SocketException)
            {
                Console.WriteLine($"Waiting for emulator to be ready... attempt {i + 1}/{maxRetries}");
                await Task.Delay(delayMs);
            }
        }

        throw new TimeoutException($"Emulator did not become ready after {maxRetries} attempts");
    }

    /// <summary>
    /// Disposes the fixture by stopping and removing the containers.
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
}
