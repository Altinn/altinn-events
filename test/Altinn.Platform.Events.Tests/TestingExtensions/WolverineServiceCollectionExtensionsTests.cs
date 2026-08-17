using System.Collections.Generic;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Wolverine.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingExtensions;

/// <summary>
/// A collection of tests verifying the flag-driven Azure Service Bus vs. legacy Storage Queue
/// publisher DI-swap in <see cref="WolverineServiceCollectionExtensions"/>.
/// </summary>
public class WolverineServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWolverineServices_RegistrationPublisherEnabled_RegistersAsbPublisher()
    {
        // Arrange
        var provider = BuildProvider(enableRegistrationPublisher: true, enableValidationPublisher: false);

        // Act
        var publisher = provider.GetRequiredService<IRegistrationEventPublisher>();

        // Assert
        Assert.IsType<RegistrationEventPublisher>(publisher);
    }

    [Fact]
    public void AddWolverineServices_RegistrationPublisherDisabled_RegistersStorageQueuePublisher()
    {
        // Arrange
        var provider = BuildProvider(enableRegistrationPublisher: false, enableValidationPublisher: false);

        // Act
        var publisher = provider.GetRequiredService<IRegistrationEventPublisher>();

        // Assert
        Assert.IsType<StorageQueueRegistrationEventPublisher>(publisher);
    }

    [Fact]
    public void AddWolverineServices_ValidationPublisherEnabled_RegistersAsbPublisher()
    {
        // Arrange
        var provider = BuildProvider(enableRegistrationPublisher: false, enableValidationPublisher: true);

        // Act
        var publisher = provider.GetRequiredService<ISubscriptionValidationPublisher>();

        // Assert
        Assert.IsType<SubscriptionValidationPublisher>(publisher);
    }

    [Fact]
    public void AddWolverineServices_ValidationPublisherDisabled_RegistersStorageQueuePublisher()
    {
        // Arrange
        var provider = BuildProvider(enableRegistrationPublisher: false, enableValidationPublisher: false);

        // Act
        var publisher = provider.GetRequiredService<ISubscriptionValidationPublisher>();

        // Assert
        Assert.IsType<StorageQueueSubscriptionValidationPublisher>(publisher);
    }

    private static ServiceProvider BuildProvider(bool enableRegistrationPublisher, bool enableValidationPublisher)
    {
        var settings = new Dictionary<string, string>
        {
            ["WolverineSettings:EnableRegistrationPublisher"] = enableRegistrationPublisher.ToString(),
            ["WolverineSettings:EnableValidationPublisher"] = enableValidationPublisher.ToString(),
            ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://127.0.0.1;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            ["WolverineSettings:RegistrationQueueName"] = "altinn.events.register",
            ["WolverineSettings:InboundQueueName"] = "altinn.events.inbound",
            ["WolverineSettings:OutboundQueueName"] = "altinn.events.outbound",
            ["WolverineSettings:ValidationQueueName"] = "altinn.events.subscription.validation"
        };

        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        Mock<IHostEnvironment> envMock = new();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var services = new ServiceCollection();
        services.AddSingleton(new Mock<IEventsQueueClient>().Object);
        services.AddWolverineServices(config, envMock.Object);

        return services.BuildServiceProvider();
    }
}
