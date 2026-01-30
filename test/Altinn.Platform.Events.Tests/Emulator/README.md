# Azure Service Bus Emulator Test Fixture

This fixture uses Testcontainers to automatically start and stop the Azure Service Bus Emulator (with MSSQL 2022 dependency) during integration tests.

## How to Use

### 1. Mark your test class with the collection attribute

```csharp
using Altinn.Platform.Events.Tests.Emulator;
using Xunit;

namespace Altinn.Platform.Events.Tests.IntegrationTests;

[Collection(nameof(AzureServiceBusEmulatorCollection))]
public class WolverineIntegrationTests
{
    private readonly AzureServiceBusEmulatorFixture _fixture;

    public WolverineIntegrationTests(AzureServiceBusEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CanPublishMessageToEmulator()
    {
        // Arrange
        var connectionString = _fixture.ConnectionString;
        Assert.True(_fixture.IsRunning);

        // Act & Assert
        // Your test code here using the emulator connection string
    }
}
```

### 2. Benefits

- **Shared Instance**: All tests in the collection share the same emulator instance (faster than starting/stopping for each test)
- **Automatic Cleanup**: The emulator is automatically stopped and removed after all tests complete
- **Realistic Testing**: Tests run against an actual Azure Service Bus Emulator instead of mocks

### 3. Requirements

- Docker must be running on the test machine
- The test machine needs to pull the following images:
  - `mcr.microsoft.com/mssql/server:2022-latest`
  - `mcr.microsoft.com/azure-messaging/servicebus-emulator:latest`

### 4. GitHub Actions

Testcontainers works out-of-the-box in GitHub Actions since it has Docker available. No additional configuration needed!

### 5. Local Development

To run tests locally with the emulator:

```bash
# Ensure Docker is running, then:
dotnet test
```

The first test run will download the required Docker images (one-time operation).
