using System;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Wolverine.Publishers;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingExtensions;

/// <summary>
/// A collection of tests verifying the flag-driven Azure Service Bus vs. legacy Storage Queue
/// publisher DI-swap in <see cref="WolverineServiceCollectionExtensions"/>.
/// </summary>
/// <remarks>
/// These call the registration helpers directly rather than going through
/// <see cref="WolverineServiceCollectionExtensions.AddWolverineServices"/>, since that also configures
/// Wolverine's Azure Service Bus transport — resolving a service that depends on <see cref="IMessageBus"/>
/// from a container built that way can trigger a real broker connection attempt, which is not something
/// a unit test should depend on.
/// </remarks>
public class WolverineServiceCollectionExtensionsTests
{
    [Fact]
    public void RegisterRegistrationEventPublisher_PublisherEnabled_RegistersAsbPublisher()
    {
        // Arrange
        var provider = BuildProvider(settings => settings.EnableRegistrationPublisher = true);

        // Act
        var publisher = provider.GetRequiredService<IRegistrationEventPublisher>();

        // Assert
        Assert.IsType<RegistrationEventPublisher>(publisher);
    }

    [Fact]
    public void RegisterRegistrationEventPublisher_PublisherDisabled_RegistersStorageQueuePublisher()
    {
        // Arrange
        var provider = BuildProvider(settings => settings.EnableRegistrationPublisher = false);

        // Act
        var publisher = provider.GetRequiredService<IRegistrationEventPublisher>();

        // Assert
        Assert.IsType<StorageQueueRegistrationEventPublisher>(publisher);
    }

    [Fact]
    public void RegisterSubscriptionValidationPublisher_PublisherEnabled_RegistersAsbPublisher()
    {
        // Arrange
        var provider = BuildProvider(settings => settings.EnableValidationPublisher = true);

        // Act
        var publisher = provider.GetRequiredService<ISubscriptionValidationPublisher>();

        // Assert
        Assert.IsType<SubscriptionValidationPublisher>(publisher);
    }

    [Fact]
    public void RegisterSubscriptionValidationPublisher_PublisherDisabled_RegistersStorageQueuePublisher()
    {
        // Arrange
        var provider = BuildProvider(settings => settings.EnableValidationPublisher = false);

        // Act
        var publisher = provider.GetRequiredService<ISubscriptionValidationPublisher>();

        // Assert
        Assert.IsType<StorageQueueSubscriptionValidationPublisher>(publisher);
    }

    private static ServiceProvider BuildProvider(Action<WolverineSettings> configureSettings)
    {
        var settings = new WolverineSettings();
        configureSettings(settings);

        var services = new ServiceCollection();
        services.AddSingleton(new Mock<IMessageBus>().Object);
        services.AddSingleton(new Mock<IEventsQueueClient>().Object);

        WolverineServiceCollectionExtensions.RegisterRegistrationEventPublisher(services, settings);
        WolverineServiceCollectionExtensions.RegisterSubscriptionValidationPublisher(services, settings);

        return services.BuildServiceProvider();
    }
}
