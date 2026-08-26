#nullable enable
using Altinn.Platform.Events.Functions.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingExtensions;

/// <summary>
/// A collection of tests verifying <see cref="WolverineServiceCollectionExtensions.AddWolverineServices"/>
/// validates its configuration before wiring up Wolverine's Azure Service Bus transport.
/// </summary>
public class WolverineServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWolverineServices_ListenerEnabledWithoutConnectionString_ThrowsWithConfigKeyName()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["WolverineSettings:EnableOutboundListener"] = "true"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddWolverineServices(config, new Mock<IHostEnvironment>().Object));

        // Assert
        Assert.Contains("WolverineSettings:ServiceBusConnectionString", exception.Message);
    }
}
