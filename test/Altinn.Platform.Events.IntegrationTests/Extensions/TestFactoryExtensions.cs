#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.IntegrationTests.Infrastructure;
using Altinn.Platform.Events.IntegrationTests.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Altinn.Platform.Events.IntegrationTests.Utils;

/// <summary>
/// Utility extension methods for common test patterns with IntegrationTestWebApplicationFactory.
/// </summary>
public static class TestFactoryExtensions
{
    /// <summary>
    /// Initializes the factory and returns it ready for use.
    /// Creates the client which triggers host initialization.
    /// </summary>
    public static IntegrationTestWebApplicationFactory Initialize(this IntegrationTestWebApplicationFactory factory)
    {
        _ = factory.CreateClient();
        return factory;
    }

    /// <summary>
    /// Publishes a message using Wolverine's message bus.
    /// Creates a scope, gets the message bus, and publishes the message.
    /// </summary>
    public static async Task PublishMessageAsync<T>(this IntegrationTestWebApplicationFactory factory, T message)
        where T : class
    {
        using var scope = factory.Host.Services.CreateScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.PublishAsync(message);
    }

    /// <summary>
    /// Creates an HttpClient with a Bearer token for the given org and scope.
    /// </summary>
    public static HttpClient CreateAuthenticatedClient(
        this IntegrationTestWebApplicationFactory factory,
        string org = "digdir",
        string? scope = null)
    {
        var client = factory.CreateClient();
        var token = PrincipalUtil.GetOrgToken(org, scope: scope);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Replaces a service registration with a mock or test implementation.
    /// Removes the existing registration and adds the new one as a singleton.
    /// </summary>
    /// <typeparam name="TService">The service type to replace.</typeparam>
    /// <param name="factory">The factory to configure.</param>
    /// <param name="implementationFactory">Factory function to create the service instance. Receives IServiceProvider for dependency resolution.</param>
    /// <returns>The factory for method chaining.</returns>
    public static IntegrationTestWebApplicationFactory ReplaceService<TService>(
        this IntegrationTestWebApplicationFactory factory,
        Func<IServiceProvider, TService> implementationFactory)
        where TService : class
    {
        factory.ConfigureTestServices(services =>
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton(implementationFactory);
        });

        return factory;
    }

    /// <summary>
    /// Re-enables the DB-driven <c>RegisteredEventsBackgroundService</c> for this factory instance.
    /// The factory disables it globally (<c>TaskCount = 0</c>) to avoid interfering with tests that
    /// don't expect background polling/claiming of registered events; use this for end-to-end tests
    /// that rely on the background service actually delivering registered events to outbound.
    /// </summary>
    /// <param name="factory">The factory to configure.</param>
    /// <param name="taskCount">The number of concurrent polling tasks to run. Defaults to 1.</param>
    /// <param name="primaryTaskIdleDelaySeconds">
    /// The idle delay, in seconds, for the primary polling task when no event was available to claim.
    /// Kept short by default to minimize added test time.
    /// </param>
    /// <returns>The factory for method chaining.</returns>
    public static IntegrationTestWebApplicationFactory EnableRegisteredEventsProcessing(
        this IntegrationTestWebApplicationFactory factory,
        int taskCount = 1,
        int primaryTaskIdleDelaySeconds = 1)
    {
        factory.ConfigureTestServices(services =>
        {
            services.PostConfigure<EventsProcessingSettings>(o =>
            {
                o.TaskCount = taskCount;
                o.PrimaryTaskIdleDelaySeconds = primaryTaskIdleDelaySeconds;
            });
        });

        return factory;
    }
}
