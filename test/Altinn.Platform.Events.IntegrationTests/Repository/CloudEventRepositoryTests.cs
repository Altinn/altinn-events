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
public class CloudEventRepositoryTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    [Fact]
    public async Task ClaimRegisteredEventAsync_EventRegistered_ClaimsAndMarksProcessed()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();
        await using (factory)
        {
            using var scope = factory.Host.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICloudEventRepository>();
            var unitOfWorkRepository = scope.ServiceProvider.GetRequiredService<IUnitOfWorkRepository>();

            var cloudEvent = CloudEventTestData.CreateTestCloudEvent();
            await repo.CreateEvent(cloudEvent.Serialize(), null, TestContext.Current.CancellationToken); // register the event first

            UnitOfWork unitOfWork = await unitOfWorkRepository.StartUnitOfWork();
            ClaimedEvent? claimed = null;
            try
            {
                claimed = await repo.ClaimRegisteredEventAsync(unitOfWork, TestContext.Current.CancellationToken);
                Assert.NotNull(claimed);
                Assert.Equal(cloudEvent.Id, claimed!.CloudEvent.Id);

                await repo.MarkEventProcessedAsync(unitOfWork, claimed.SequenceNo, TestContext.Current.CancellationToken);
                await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

            }
            catch
            {
                await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                throw;
            }

            // Assert that the event is marked as processed in the database
            string? status = await PostgresTestUtils.GetEventStatusAsync(_fixture.PostgresConnectionString, claimed!.SequenceNo);
            Assert.Equal("processed", status);
        }
    }
}
