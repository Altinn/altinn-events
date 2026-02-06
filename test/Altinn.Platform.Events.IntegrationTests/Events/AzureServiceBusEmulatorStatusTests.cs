using Altinn.Platform.Events.IntegrationTests.Emulator;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.Events;

/// <summary>
/// Tests that verify the Azure Service Bus Emulator is running and accessible.
/// </summary>
[Collection(nameof(AzureServiceBusEmulatorCollection))]
public class AzureServiceBusEmulatorStatusTests(AzureServiceBusEmulatorFixture fixture)
{
    private readonly AzureServiceBusEmulatorFixture _fixture = fixture;

    [Fact]
    public void EmulatorIsRunning()
    {
        Assert.True(_fixture.IsRunning, "Azure Service Bus Emulator should be running");
        Assert.NotEmpty(_fixture.ConnectionString);
        Assert.Contains("Endpoint=sb://127.0.0.1", _fixture.ConnectionString);
    }
}
