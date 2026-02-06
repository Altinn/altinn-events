#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Commands;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.IntegrationTests.Utils;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Telemetry;
using AltinnCore.Authentication.JwtCookie;
using Azure.Messaging.ServiceBus;
using CloudNative.CloudEvents;
using JasperFx.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.ErrorHandling;
using Yuniql.AspNetCore;
using Yuniql.PostgreSql;

namespace Altinn.Platform.Events.IntegrationTests.Infrastructure;

/// <summary>
/// A test host for integration tests.
/// Manages the WebApplication lifecycle and provides helpers for queue and database operations.
/// </summary>
public class IntegrationTestHost(IntegrationTestContainersFixture fixture) : IAsyncDisposable
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;
    private readonly ServiceBusClient _serviceBusClient = new(fixture.ServiceBusConnectionString);

    private WebApplication? _app;
    private Task? _runTask;
    private WolverineSettings? _settings;
    private Action<WolverineOptions>? _customWolverineConfig;
    private bool _useShortRetryPolicy;
    private bool _purgeQueuesOnStart;

    // Service replacements - allows tests to inject their own implementations
    private readonly Dictionary<Type, object> _serviceReplacements = [];

    public string RegisterQueueName => _settings?.RegistrationQueueName
        ?? throw new InvalidOperationException("Host not started. Call StartAsync() first.");

    public string InboundQueueName => _settings?.InboundQueueName
        ?? throw new InvalidOperationException("Host not started. Call StartAsync() first.");

    public string OutboundQueueName => _settings?.OutboundQueueName
        ?? throw new InvalidOperationException("Host not started. Call StartAsync() first.");

    public string ValidationQueueName => _settings?.ValidationQueueName
        ?? throw new InvalidOperationException("Host not started. Call StartAsync() first.");

    public string PostgresConnectionString => _fixture.PostgresConnectionString;

    #region Builder Methods

    /// <summary>
    /// Replaces a service registration with a custom implementation.
    /// Useful for injecting mocks in specific tests.
    /// </summary>
    /// <example>
    /// var mockRepo = new Mock&lt;ICloudEventRepository&gt;();
    /// host.WithServiceReplacement(mockRepo.Object);
    /// </example>
    public IntegrationTestHost WithServiceReplacement<TService>(TService implementation)
        where TService : class
    {
        _serviceReplacements[typeof(TService)] = implementation;
        return this;
    }

    public IntegrationTestHost WithShortRetryPolicy()
    {
        _useShortRetryPolicy = true;
        return this;
    }

    public IntegrationTestHost WithCleanQueues()
    {
        _purgeQueuesOnStart = true;
        return this;
    }

    public IntegrationTestHost WithWolverineConfig(Action<WolverineOptions> configure)
    {
        _customWolverineConfig = configure;
        return this;
    }

    #endregion

    #region Public API

    public async Task<IntegrationTestHost> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;

        ConfigureAppSettings(builder.Configuration);
        ConfigureServices(builder.Services, builder.Configuration);

        if (_purgeQueuesOnStart)
        {
            await PurgeQueuesAsync();
        }

        _app = builder.Build();

        // Run database migrations (always use real database unless service was replaced)
        await CreatePlatformEventsRoleAsync();
        RunDatabaseMigrations();

        _runTask = _app.RunAsync();

        await WaitForWolverineReadyAsync();

        return this;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();

            // Check if the run task faulted
            if (_runTask is { IsFaulted: true })
            {
                await _runTask; // Observe the exception
            }

            await _app.DisposeAsync();
        }

        await _serviceBusClient.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public async Task PublishAsync<T>(T message)
        where T : class
    {
        EnsureStarted();
        var messageBus = _app!.Services.GetRequiredService<IMessageBus>();
        await messageBus.PublishAsync(message);
    }

    public async Task<ServiceBusReceivedMessage?> WaitForMessageAsync(string queueName, TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(10);
        await using var receiver = _serviceBusClient.CreateReceiver(queueName);
        using var cts = new CancellationTokenSource(actualTimeout);

        try
        {
            var message = await receiver.ReceiveMessageAsync(actualTimeout, cts.Token);

            if (message != null)
            {
                await receiver.CompleteMessageAsync(message, cts.Token);
            }

            return message;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public Task<ServiceBusReceivedMessage?> WaitForDeadLetterMessageAsync(string queueName, TimeSpan? timeout = null)
        => WaitForMessageAsync($"{queueName}/$deadletterqueue", timeout);

    /// <summary>
    /// Waits until the specified queue is empty (no messages waiting).
    /// Polls until empty or timeout is reached.
    /// </summary>
    public async Task<bool> WaitForEmptyAsync(string queueName, TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(10);
        var pollInterval = TimeSpan.FromMilliseconds(100);
        var maxAttempts = (int)(actualTimeout.TotalMilliseconds / pollInterval.TotalMilliseconds);

        await using var receiver = _serviceBusClient.CreateReceiver(queueName);

        return await WaitForUtils.WaitForAsync(
            async () => await receiver.PeekMessageAsync() == null,
            maxAttempts,
            (int)pollInterval.TotalMilliseconds);
    }

    /// <summary>
    /// Waits until the dead letter queue for the specified queue is empty.
    /// </summary>
    public Task<bool> WaitForDeadLetterEmptyAsync(string queueName, TimeSpan? timeout = null)
        => WaitForEmptyAsync($"{queueName}/$deadletterqueue", timeout);

    public static CloudEvent CreateTestCloudEvent(string? id = null)
    {
        var cloudEvent = new CloudEvent
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Source = new Uri("https://ttd.apps.altinn.no/ttd/test-app/instances/12345/abcd-1234"),
            Type = "app.instance.created",
            Subject = "/party/12345",
            Time = DateTimeOffset.UtcNow,
            DataContentType = "application/json",
        };

        cloudEvent["resource"] = "urn:altinn:resource:app_ttd_test-app";

        return cloudEvent;
    }

    #endregion

    #region Private Helpers

    private void EnsureStarted()
    {
        if (_app == null)
        {
            throw new InvalidOperationException("Test host not started. Call StartAsync() first.");
        }
    }

    private async Task CreatePlatformEventsRoleAsync()
    {
        try
        {
            Console.WriteLine("Creating platform_events database role...");

            await using var dataSource = NpgsqlDataSource.Create(_fixture.PostgresConnectionString);
            await using var command = dataSource.CreateCommand(
                "CREATE ROLE platform_events WITH LOGIN PASSWORD 'Password';");
            await command.ExecuteNonQueryAsync();

            Console.WriteLine("Created platform_events database role");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateObject)
        {
            Console.WriteLine("platform_events role already exists");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create platform_events role: {ex.Message}");
            throw;
        }
    }

    private void RunDatabaseMigrations()
    {
        try
        {
            Console.WriteLine("Running database migrations using Yuniql...");

            var traceService = new ConsoleTraceService { IsDebugEnabled = true };

            // Use the PostgreSQL connection string that was configured for the container
            string connectionString = _fixture.PostgresConnectionString;

            // Find the Migration directory
            string migrationPath = FindMigrationPath();

            // Use the same approach as Program.cs
            _app!.UseYuniql(
                new PostgreSqlDataService(traceService),
                new PostgreSqlBulkImportService(traceService),
                traceService,
                new Yuniql.AspNetCore.Configuration
                {
                    Workspace = migrationPath,
                    ConnectionString = connectionString,
                    IsAutoCreateDatabase = false,
                    IsDebug = true
                });

            Console.WriteLine("Database migrations completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to run migrations: {ex.Message}");
            throw;
        }
    }

    private static string FindMigrationPath()
    {
        // Navigate up from the test bin directory to find the src/Events/Migration folder
        string? currentDir = AppContext.BaseDirectory;

        for (int i = 0; i < 10; i++)
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
            if (currentDir == null)
            {
                break;
            }

            string migrationPath = Path.Combine(currentDir, "src", "Events", "Migration");
            if (Directory.Exists(migrationPath))
            {
                return migrationPath;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find Migration directory. Expected structure: <repo-root>/src/Events/Migration. " +
            $"Searched up to 10 parent directories from: {AppContext.BaseDirectory}");
    }

    private void ConfigureAppSettings(ConfigurationManager configuration)
    {
        configuration.Sources.Clear();

        configuration.SetBasePath(AppContext.BaseDirectory);
        configuration.AddJsonFile("appsettings.integrationtest.json", optional: false);

        var inMemorySettings = new List<KeyValuePair<string, string?>>
        {
            new("WolverineSettings:ServiceBusConnectionString", _fixture.ServiceBusConnectionString),
            new("PostgreSQLSettings:ConnectionString", _fixture.PostgresConnectionString),
            new("PostgreSQLSettings:AdminConnectionString", _fixture.PostgresConnectionString)
        };

        configuration.AddInMemoryCollection(inMemorySettings);
    }

    private void ConfigureServices(IServiceCollection services, ConfigurationManager configuration)
    {
        ConfigureDependencies(services);
        ConfigureWolverine(services, configuration);

        services.AddMemoryCache();
        services.Configure<PlatformSettings>(configuration.GetSection("PlatformSettings"));

        services.AddScoped<IEventsService, EventsService>();
        services.AddScoped<IOutboundService, OutboundService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IAppSubscriptionService, AppSubscriptionService>();
        services.AddScoped<IGenericSubscriptionService, GenericSubscriptionService>();
    }

    private void ConfigureDependencies(IServiceCollection services)
    {
        // Always register database connection
        var connectionString = _fixture.PostgresConnectionString;
        services.AddNpgsqlDataSource(connectionString, builder => builder
            .EnableParameterLogging(true)
            .EnableDynamicJson());

        // Register repositories - use replacements if provided, otherwise use real implementations
        RegisterServiceOrReplacement<ICloudEventRepository, CloudEventRepository>(services);
        RegisterServiceOrReplacement<ISubscriptionRepository, SubscriptionRepository>(services);
        RegisterServiceOrReplacement<ITraceLogRepository, TraceLogRepository>(services);

        // Decorate subscription repository with caching (if real implementation is used)
        if (!_serviceReplacements.ContainsKey(typeof(ISubscriptionRepository)))
        {
            services.Decorate<ISubscriptionRepository, SubscriptionRepositoryCachingDecorator>();
        }

        // Mock non-critical services that tests don't typically need to verify
        services.AddSingleton(new Mock<ITraceLogService>().Object);
        services.AddSingleton(new Mock<IAuthorization>().Object);

        // TelemetryClient is a sealed class, so we use a real instance instead of mocking
        services.AddSingleton(new TelemetryClient());

        services.AddSingleton(CreateEventsQueueClientMock());
        services.AddSingleton(new Mock<IRegisterService>().Object);
        services.AddSingleton(new Mock<IPDP>().Object);
        services.AddSingleton(new Mock<IPublicSigningKeyProvider>().Object);
        services.AddSingleton(new Mock<IPostConfigureOptions<JwtCookieOptions>>().Object);
    }

    private void RegisterServiceOrReplacement<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (_serviceReplacements.TryGetValue(typeof(TService), out var replacement))
        {
            services.AddSingleton(typeof(TService), replacement);
        }
        else
        {
            services.AddSingleton<TService, TImplementation>();
        }
    }

    private static IEventsQueueClient CreateEventsQueueClientMock()
    {
        var mock = new Mock<IEventsQueueClient>();
        var successReceipt = new Models.QueuePostReceipt { Success = true };

        mock.Setup(x => x.EnqueueRegistration(It.IsAny<string>())).ReturnsAsync(successReceipt);
        mock.Setup(x => x.EnqueueInbound(It.IsAny<string>())).ReturnsAsync(successReceipt);
        mock.Setup(x => x.EnqueueOutbound(It.IsAny<string>())).ReturnsAsync(successReceipt);
        mock.Setup(x => x.EnqueueSubscriptionValidation(It.IsAny<string>())).ReturnsAsync(successReceipt);

        return mock.Object;
    }

    private void ConfigureWolverine(IServiceCollection services, IConfiguration configuration)
    {
        _settings = configuration.GetSection("WolverineSettings").Get<Configuration.WolverineSettings>()
            ?? throw new InvalidOperationException("WolverineSettings not found in configuration");

        services.AddWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(SaveEventHandler).Assembly);

            ConfigureServiceBusTransport(opts, _settings);
            ConfigureRetryPolicy(opts);

            _customWolverineConfig?.Invoke(opts);
        });
    }

    private static void ConfigureServiceBusTransport(WolverineOptions opts, Configuration.WolverineSettings settings)
    {
        if (!settings.EnableServiceBus)
        {
            return;
        }

        opts.ConfigureEventsDefaults(
            new HostEnvironmentStub(Environments.Development),
            settings.ServiceBusConnectionString);

        opts.PublishMessage<RegisterEventCommand>()
            .ToAzureServiceBusQueue(settings.RegistrationQueueName);
        opts.PublishMessage<InboundEventCommand>()
            .ToAzureServiceBusQueue(settings.InboundQueueName);
        opts.PublishMessage<OutboundEventCommand>()
            .ToAzureServiceBusQueue(settings.OutboundQueueName);
        opts.PublishMessage<ValidateSubscriptionCommand>()
            .ToAzureServiceBusQueue(settings.ValidationQueueName);

        opts.ListenToAzureServiceBusQueue(settings.RegistrationQueueName)
            .ListenerCount(settings.ListenerCount)
            .ProcessInline();
        opts.ListenToAzureServiceBusQueue(settings.InboundQueueName)
            .ListenerCount(settings.ListenerCount)
            .ProcessInline();
        opts.ListenToAzureServiceBusQueue(settings.ValidationQueueName)
            .ListenerCount(settings.ListenerCount)
            .ProcessInline();

        opts.Policies.AllListeners(x => x.ProcessInline());
        opts.Policies.AllSenders(x => x.SendInline());
    }

    private void ConfigureRetryPolicy(WolverineOptions opts)
    {
        if (!_useShortRetryPolicy)
        {
            return;
        }

        opts.Policies.OnException<TaskCanceledException>()
            .Or<InvalidOperationException>()
            .RetryWithCooldown(100.Milliseconds(), 100.Milliseconds(), 100.Milliseconds())
            .Then.ScheduleRetry(500.Milliseconds(), 500.Milliseconds(), 500.Milliseconds())
            .Then.MoveToErrorQueue();
    }

    private async Task PurgeQueuesAsync()
    {
        await PurgeQueueAsync(RegisterQueueName);
        await PurgeQueueAsync(InboundQueueName);
        await PurgeQueueAsync(OutboundQueueName);
        await PurgeQueueAsync(ValidationQueueName);
    }

    private async Task PurgeQueueAsync(string queueName)
    {
        try
        {
            await using var receiver = _serviceBusClient.CreateReceiver(queueName);
            while (await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(1)) is { Count: > 0 } messages)
            {
                foreach (var message in messages)
                {
                    await receiver.CompleteMessageAsync(message);
                }
            }
        }
        catch (ServiceBusException)
        {
            // Queue may not exist yet - acceptable for test setup
            Console.WriteLine($"Queue '{queueName}' does not exist yet or is not accessible - skipping purge");
        }
    }

    private async Task WaitForWolverineReadyAsync(TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(30);

        var lifetime = _app!.Services.GetRequiredService<IHostApplicationLifetime>();
        var startedTcs = new TaskCompletionSource();
        using var registration = lifetime.ApplicationStarted.Register(() => startedTcs.TrySetResult());

        if (!lifetime.ApplicationStarted.IsCancellationRequested)
        {
            var timeoutTask = Task.Delay(actualTimeout);
            var completedTask = await Task.WhenAny(startedTcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Host did not start within {actualTimeout.TotalSeconds}s");
            }
        }
    }

    #endregion

    private class HostEnvironmentStub(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Altinn.Platform.Events.IntegrationTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
