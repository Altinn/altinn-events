#nullable enable
using System.Threading.Tasks;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.IntegrationTests.Data;
using Altinn.Platform.Events.IntegrationTests.Infrastructure;
using Altinn.Platform.Events.IntegrationTests.Utils;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.Repository;

[Collection(nameof(IntegrationTestContainersCollection))]
public class MetricsRepositoryTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    [Fact]
    public async Task GetBacklogMetrics_RegisteredAndRetryExhaustedEvents_AreCounted()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();
        await using (factory)
        {
            using var scope = factory.Host.Services.CreateScope();
            var cloudEventRepository = scope.ServiceProvider.GetRequiredService<ICloudEventRepository>();
            var metricsRepository = scope.ServiceProvider.GetRequiredService<IMetricsRepository>();

            // The database is shared between tests, so assert on the change rather than absolute values.
            var before = await metricsRepository.GetBacklogMetrics(TestContext.Current.CancellationToken);

            var registeredEvent = CloudEventTestData.CreateTestCloudEvent();
            var exhaustedEvent = CloudEventTestData.CreateTestCloudEvent();
            await cloudEventRepository.CreateEvent(registeredEvent.Serialize(), null, TestContext.Current.CancellationToken);
            await cloudEventRepository.CreateEvent(exhaustedEvent.Serialize(), null, TestContext.Current.CancellationToken);
            await PostgresTestUtils.SetEventStatusAsync(_fixture.PostgresConnectionString, exhaustedEvent.Id!, (int)EventStatus.RetryExhausted);

            var after = await metricsRepository.GetBacklogMetrics(TestContext.Current.CancellationToken);

            Assert.Equal(before.RegisteredCount + 1, after.RegisteredCount);
            Assert.Equal(before.RetryExhaustedCount + 1, after.RetryExhaustedCount);
            Assert.True(after.RegisteredOldestAgeSeconds >= 0);
        }
    }
}
