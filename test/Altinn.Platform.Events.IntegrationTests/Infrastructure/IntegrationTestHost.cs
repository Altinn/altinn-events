#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Commands;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;
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
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.ErrorHandling;

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
    private Configuration.WolverineSettings? _settings;
    private Mock<ICloudEventRepository> _cloudEventRepositoryMock = new();
    private Action<WolverineOptions>? _customWolverineConfig;
    private bool _useShortRetryPolicy;
    private bool _purgeQueuesOnStart;

    public string RegisterQueueName => _settings?.RegistrationQueueName
        ?? throw new InvalidOperationException("Host not started. Call StartAsync() first.");

    public string InboundQueueName => _settings?.InboundQueueName
        ?? throw new InvalidOperationException("Host not started. Call StartAsync() first.");

    public Mock<ICloudEventRepository> CloudEventRepositoryMock => _cloudEventRepositoryMock;

    #region Builder Methods

    public IntegrationTestHost WithCloudEventRepository(Action<Mock<ICloudEventRepository>> configure)
    {
        _cloudEventRepositoryMock = new Mock<ICloudEventRepository>();
        configure(_cloudEventRepositoryMock);
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
        await using var receiver = _serviceBusClient.CreateReceiver(queueName);
        using var cts = new CancellationTokenSource(actualTimeout);

        try
        {
            while (await receiver.PeekMessageAsync(cancellationToken: cts.Token) != null)
            {
                await Task.Delay(pollInterval, cts.Token);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Waits until the dead letter queue for the specified queue is empty.
    /// </summary>
    public Task<bool> WaitForDeadLetterEmptyAsync(string queueName, TimeSpan? timeout = null)
        => WaitForEmptyAsync($"{queueName}/$deadletterqueue", timeout);

    /// <summary>
    /// Waits until the CloudEventRepository mock has been invoked at least once.
    /// </summary>
    /// <param name="timeout">Maximum time to wait. Defaults to 5 seconds.</param>
    /// <returns>True if the mock was invoked, false if timeout was reached.</returns>
    public async Task<bool> WaitForRepositoryInvocationAsync(TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var pollInterval = TimeSpan.FromMilliseconds(100);
        using var cts = new CancellationTokenSource(actualTimeout);

        try
        {
            while (_cloudEventRepositoryMock.Invocations.Count == 0)
            {
                await Task.Delay(pollInterval, cts.Token);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the PostgreSQL connection string from the fixture.
    /// </summary>
    public string PostgresConnectionString => _fixture.PostgresConnectionString;

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

    private void ConfigureAppSettings(ConfigurationManager configuration)
    {
        configuration.Sources.Clear();

        configuration.SetBasePath(AppContext.BaseDirectory);
        configuration.AddJsonFile("appsettings.integrationtest.json", optional: false);

        configuration.AddInMemoryCollection([
            new("WolverineSettings:ServiceBusConnectionString", _fixture.ServiceBusConnectionString)
        ]);
    }

    private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ConfigureMockedDependencies(services);
        ConfigureWolverine(services, configuration);

        services.AddSingleton<IEventsService, Services.EventsService>();
    }

    private void ConfigureMockedDependencies(IServiceCollection services)
    {
        services.AddSingleton(_cloudEventRepositoryMock.Object);
        services.AddSingleton(new Mock<ITraceLogService>().Object);
        services.AddSingleton(new Mock<IAuthorization>().Object);

        services.AddSingleton(CreateEventsQueueClientMock());
        services.AddSingleton(new Mock<IRegisterService>().Object);
        services.AddSingleton(new Mock<IPDP>().Object);
        services.AddSingleton(new Mock<IPublicSigningKeyProvider>().Object);
        services.AddSingleton(new Mock<IPostConfigureOptions<JwtCookieOptions>>().Object);
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

        opts.ListenToAzureServiceBusQueue(settings.RegistrationQueueName)
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
