#nullable enable
using System;
using System.Threading.Tasks;
using Altinn.Platform.Events.IntegrationTests.Infrastructure;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Platform.Events.IntegrationTests.Data;

/// <summary>
/// Utility class for creating and managing test subscriptions in integration tests.
/// </summary>
public static class SubscriptionTestData
{
    /// <summary>
    /// Creates a test subscription directly in the database via the repository.
    /// The subscription is created with Validated = false.
    /// </summary>
    public static async Task<Subscription> CreateTestSubscriptionInDb(
        IntegrationTestWebApplicationFactory factory,
        string? resourceFilter = null,
        string? typeFilter = null)
    {
        using var scope = factory.Host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();

        var subscription = new Subscription
        {
            EndPoint = new Uri("https://test-subscriber.example.com/webhook"),
            ResourceFilter = resourceFilter ?? "urn:altinn:resource:app_ttd_test-app",
            Consumer = "/org/digdir",
            CreatedBy = "/org/digdir",
            TypeFilter = typeFilter ?? "app.instance.created"
        };

        return await repo.CreateSubscription(subscription);
    }

    /// <summary>
    /// Marks a subscription as validated in the database.
    /// </summary>
    public static async Task ValidateSubscription(IntegrationTestWebApplicationFactory factory, int subscriptionId)
    {
        using var scope = factory.Host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        await repo.SetValidSubscription(subscriptionId);
    }

    /// <summary>
    /// Gets a subscription from the database by ID.
    /// </summary>
    public static async Task<Subscription?> GetSubscriptionFromDb(IntegrationTestWebApplicationFactory factory, int subscriptionId)
    {
        using var scope = factory.Host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        return await repo.GetSubscription(subscriptionId);
    }
}
