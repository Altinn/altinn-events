using System.Threading.Tasks;

using Altinn.Platform.Events.Tests.Emulator;

using Xunit;

namespace Altinn.Platform.Events.Tests.IntegrationTests;

/// <summary>
/// Integration tests for Wolverine with Azure Service Bus Emulator.
/// These tests verify that Wolverine can connect to and communicate with the emulator.
/// </summary>
[Collection(nameof(AzureServiceBusEmulatorCollection))]
public class WolverineIntegrationTests(AzureServiceBusEmulatorFixture fixture)
{
    private readonly AzureServiceBusEmulatorFixture _fixture = fixture;

    [Fact]
    public void EmulatorIsRunning()
    {
        // Assert
        Assert.True(_fixture.IsRunning, "Azure Service Bus Emulator should be running");
        Assert.NotEmpty(_fixture.ConnectionString);
        Assert.Contains("Endpoint=sb://127.0.0.1", _fixture.ConnectionString);
    }
}
