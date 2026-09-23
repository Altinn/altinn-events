#nullable enable
using System.Collections.Generic;
using System.Linq;
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

    [Fact]
    public async Task MarkEventRetryAsync_RepeatedFailures_IncrementsRetryCount_AndFlagsRetryExhausted()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();
        await using (factory)
        {
            using var scope = factory.Host.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICloudEventRepository>();
            var unitOfWorkRepository = scope.ServiceProvider.GetRequiredService<IUnitOfWorkRepository>();
            var settings = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Configuration.EventsProcessingSettings>>().Value;

            var cloudEvent = CloudEventTestData.CreateTestCloudEvent();
            await repo.CreateEvent(cloudEvent.Serialize(), null, TestContext.Current.CancellationToken);

            UnitOfWork unitOfWork = await unitOfWorkRepository.StartUnitOfWork();
            ClaimedEvent? claimed;
            try
            {
                claimed = await repo.ClaimRegisteredEventAsync(unitOfWork, TestContext.Current.CancellationToken);
                Assert.NotNull(claimed);

                await repo.MarkEventRetryAsync(unitOfWork, claimed!.SequenceNo, TestContext.Current.CancellationToken);
                await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
            }
            catch
            {
                await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                throw;
            }

            // First failure: retry count incremented, status remains registered.
            int? retryCount = await PostgresTestUtils.GetEventRetryCountAsync(_fixture.PostgresConnectionString, claimed!.SequenceNo);
            string? status = await PostgresTestUtils.GetEventStatusAsync(_fixture.PostgresConnectionString, claimed.SequenceNo);
            Assert.Equal(1, retryCount);
            Assert.Equal("registered", status);

            // Drive remaining retries directly (bypassing claim, since claim only selects 'registered' rows,
            // and we want to isolate MarkEventRetryAsync behavior at the boundary).
            for (int i = 1; i < settings.MaxRetryCount; i++)
            {
                UnitOfWork retryUnitOfWork = await unitOfWorkRepository.StartUnitOfWork();
                try
                {
                    await repo.MarkEventRetryAsync(retryUnitOfWork, claimed.SequenceNo, TestContext.Current.CancellationToken);
                    await unitOfWorkRepository.CommitUnitOfWork(retryUnitOfWork);
                }
                catch
                {
                    await unitOfWorkRepository.RollbackUnitOfWork(retryUnitOfWork);
                    throw;
                }
            }

            retryCount = await PostgresTestUtils.GetEventRetryCountAsync(_fixture.PostgresConnectionString, claimed.SequenceNo);
            status = await PostgresTestUtils.GetEventStatusAsync(_fixture.PostgresConnectionString, claimed.SequenceNo);

            Assert.Equal(settings.MaxRetryCount, retryCount);
            Assert.Equal("retryExhausted", status);
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    public async Task ClaimRegisteredEventAsync_ConcurrentClaims_EachRowClaimedByExactlyOneCaller(int eventCount)
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();
        await using (factory)
        {
            var expectedIds = new HashSet<string>();

            using (var seedScope = factory.Host.Services.CreateScope())
            {
                var seedRepo = seedScope.ServiceProvider.GetRequiredService<ICloudEventRepository>();

                for (int i = 0; i < eventCount; i++)
                {
                    var cloudEvent = CloudEventTestData.CreateTestCloudEvent();
                    expectedIds.Add(cloudEvent.Id);
                    await seedRepo.CreateEvent(cloudEvent.Serialize(), null, TestContext.Current.CancellationToken);
                }
            }

            // Simulate the background service model: several parallel tasks, each resolving its own
            // scoped repository/unit-of-work-repository, racing to claim rows concurrently.
            // FOR UPDATE SKIP LOCKED should guarantee each row is claimed by exactly one caller.
            var claimTasks = new List<Task<string?>>();
            for (int i = 0; i < eventCount; i++)
            {
                claimTasks.Add(ClaimAndProcessOneInNewScopeAsync(factory));
            }

            string?[] results = await Task.WhenAll(claimTasks);
            var claimedIds = new HashSet<string>(results.Where(id => id is not null)!);

            // No row should have been claimed twice, and every registered row should have been claimed.
            Assert.Equal(eventCount, claimedIds.Count);
            Assert.Equal(expectedIds, claimedIds);

            // No more registered rows should be available.
            string? none = await ClaimAndProcessOneInNewScopeAsync(factory);
            Assert.Null(none);
        }
    }

    private static async Task<string?> ClaimAndProcessOneInNewScopeAsync(IntegrationTestWebApplicationFactory factory)
    {
        using var scope = factory.Host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICloudEventRepository>();
        var unitOfWorkRepository = scope.ServiceProvider.GetRequiredService<IUnitOfWorkRepository>();

        UnitOfWork unitOfWork = await unitOfWorkRepository.StartUnitOfWork();
        try
        {
            ClaimedEvent? claimed = await repo.ClaimRegisteredEventAsync(unitOfWork, TestContext.Current.CancellationToken);
            if (claimed is null)
            {
                await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
                return null;
            }

            await repo.MarkEventProcessedAsync(unitOfWork, claimed.SequenceNo, TestContext.Current.CancellationToken);
            await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
            return claimed.CloudEvent.Id;
        }
        catch
        {
            await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            throw;
        }
    }
}
