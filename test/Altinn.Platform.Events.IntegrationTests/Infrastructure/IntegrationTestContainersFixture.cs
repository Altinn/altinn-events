#nullable enable
using System;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.IntegrationTests.Utils;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit fixture that starts all required containers for integration tests (PostgreSQL, MSSQL, Azure Service Bus Emulator).
/// This acts like a docker-compose setup, starting all infrastructure containers together.
/// The fixture is shared across all tests in the collection to avoid starting/stopping containers repeatedly.
/// </summary>
public class IntegrationTestContainersFixture : IAsyncLifetime
{
    private INetwork? _network;
    private IContainer? _postgresContainer;
    private IContainer? _mssqlContainer;
    private IContainer? _serviceBusEmulatorContainer;

    #region Properties

    /// <summary>
    /// Gets the Azure Service Bus connection string for the emulator.
    /// </summary>
    public string ServiceBusConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the PostgreSQL connection string.
    /// Matches the format in appsettings.integrationtest.json with port injected.
    /// </summary>
    public string PostgresConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the mapped PostgreSQL port.
    /// </summary>
    public int PostgresPort { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the emulator is running.
    /// </summary>
    public bool IsRunning { get; private set; }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the fixture by starting PostgreSQL, MSSQL, and Azure Service Bus Emulator containers.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _network = new NetworkBuilder()
                .WithName($"integration-test-network-{Guid.NewGuid():N}")
                .Build();

            await _network.CreateAsync();

            // Load PostgreSQL settings from appsettings
            var postgresSettings = LoadPostgreSqlSettings();

            // Start PostgreSQL with credentials from appsettings
            _postgresContainer = new ContainerBuilder(ContainerImageUtils.GetImage("postgres"))
                .WithNetwork(_network)
                .WithNetworkAliases("postgres")
                .WithEnvironment("POSTGRES_USER", postgresSettings.Username)
                .WithEnvironment("POSTGRES_PASSWORD", postgresSettings.Password)
                .WithEnvironment("POSTGRES_DB", postgresSettings.Database)
                .WithPortBinding(5432, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready"))
                .WithAutoRemove(true)
                .Build();

            await _postgresContainer.StartAsync();

            PostgresPort = _postgresContainer.GetMappedPublicPort(5432);
            PostgresConnectionString = $"Host=localhost;Port={PostgresPort};Database={postgresSettings.Database};Username={postgresSettings.Username};Password={postgresSettings.Password}";

            // Wait for PostgreSQL to be fully ready
            await WaitForPostgresAsync();

            Console.WriteLine($"PostgreSQL started on port {PostgresPort}");

            // Start MSSQL (required by Service Bus Emulator)
            _mssqlContainer = new ContainerBuilder(ContainerImageUtils.GetImage("mssql"))
                .WithNetwork(_network)
                .WithNetworkAliases("mssql")
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("MSSQL_SA_PASSWORD", "YourStrong!Passw0rd")
                .WithAutoRemove(true)
                .Build();

            await _mssqlContainer.StartAsync();

            string configPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "config.json");

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Emulator config file not found at: {configPath}");
            }

            _serviceBusEmulatorContainer = new ContainerBuilder(ContainerImageUtils.GetImage("serviceBusEmulator"))
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

            ServiceBusConnectionString = $"Endpoint=sb://127.0.0.1:{hostPort};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

            // Wait for the Service Bus Emulator to be fully ready to accept connections
            await WaitForServiceBusAsync();

            IsRunning = true;
            Console.WriteLine("All integration test containers started successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start integration test containers: {ex.Message}");
            IsRunning = false;
            await DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Disposes the fixture by stopping and removing all containers.
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

            if (_postgresContainer != null)
            {
                await _postgresContainer.StopAsync();
                await _postgresContainer.DisposeAsync();
            }

            if (_network != null)
            {
                await _network.DeleteAsync();
            }

            Console.WriteLine("All integration test containers cleaned up");
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

    #endregion

    #region Private Helpers

    /// <summary>
    /// Waits for PostgreSQL to be fully ready by attempting to connect with retries.
    /// </summary>
    private Task WaitForPostgresAsync() =>
        WaitForTcpPortAsync("PostgreSQL", "127.0.0.1", PostgresPort);

    /// <summary>
    /// Waits for the Service Bus Emulator to be fully ready by attempting to connect with retries.
    /// The container being "started" doesn't mean the AMQP listener is ready.
    /// </summary>
    private Task WaitForServiceBusAsync()
    {
        int hostPort = _serviceBusEmulatorContainer!.GetMappedPublicPort(5672);
        return WaitForTcpPortAsync("Service Bus Emulator", "127.0.0.1", hostPort);
    }

    /// <summary>
    /// Loads PostgreSQL settings from appsettings.integrationtest.json.
    /// </summary>
    private static PostgreSqlSettings LoadPostgreSqlSettings()
    {
        string appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.integrationtest.json");

        if (!File.Exists(appSettingsPath))
        {
            throw new FileNotFoundException($"appsettings.integrationtest.json not found at: {appSettingsPath}");
        }

        string json = File.ReadAllText(appSettingsPath);
        using JsonDocument doc = JsonDocument.Parse(json);

        var postgresSection = doc.RootElement.GetProperty("PostgreSQLSettings");

        // Extract database, username from AdminConnectionString format
        // Format: "Host=localhost;Port=5432;Username=platform_events_admin;Password={0};Database=eventsdb"
        string adminConnString = postgresSection.GetProperty("AdminConnectionString").GetString()
            ?? throw new InvalidOperationException("AdminConnectionString not found");

        string database = ExtractConnectionStringPart(adminConnString, "Database");
        string username = ExtractConnectionStringPart(adminConnString, "Username");
        string password = postgresSection.GetProperty("EventsDbAdminPwd").GetString()
            ?? throw new InvalidOperationException("EventsDbAdminPwd not found");

        return new PostgreSqlSettings(database, username, password);
    }

    private static string ExtractConnectionStringPart(string connectionString, string key)
    {
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2 && keyValue[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return keyValue[1].Trim();
            }
        }

        throw new InvalidOperationException($"{key} not found in connection string");
    }

    /// <summary>
    /// Generic helper to wait for a TCP port to become available.
    /// </summary>
    private static async Task WaitForTcpPortAsync(string serviceName, string host, int port)
    {
        const int maxRetries = 30;
        const int delayMs = 1000;
        const int connectTimeoutMs = 1000;

        int attemptNumber = 0;
        bool ready = await WaitForUtils.WaitForAsync(
            async () =>
            {
                attemptNumber++;
                try
                {
                    using var client = new TcpClient();
                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(connectTimeoutMs));
                    await client.ConnectAsync(host, port, cts.Token);

                    Console.WriteLine($"{serviceName} ready after {attemptNumber} attempt(s)");
                    return true;
                }
                catch (Exception ex) when (ex is SocketException or OperationCanceledException)
                {
                    Console.WriteLine($"Waiting for {serviceName}... attempt {attemptNumber}/{maxRetries}");
                    return false;
                }
            },
            maxRetries,
            delayMs);

        if (!ready)
        {
            throw new TimeoutException($"{serviceName} did not become ready after {maxRetries} attempts");
        }
    }

    #endregion
}

/// <summary>
/// PostgreSQL settings loaded from appsettings.
/// </summary>
internal record PostgreSqlSettings(string Database, string Username, string Password);

/// <summary>
/// xUnit collection definition for integration tests that require infrastructure containers.
/// All tests in this collection will share the same container instances (PostgreSQL, MSSQL, Service Bus Emulator).
/// </summary>
[CollectionDefinition(nameof(IntegrationTestContainersCollection))]
public class IntegrationTestContainersCollection : ICollectionFixture<IntegrationTestContainersFixture>
{
}
