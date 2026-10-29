using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Telemetry;

using Moq;
using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// Test class for the MetricsService
    /// </summary>
    public class MetricsServiceTests
    {
        /// <summary>
        /// Scenario:
        ///   Metrics are triggered.
        /// Expected result:
        ///   The values from the repository are recorded as backlog metrics.
        /// Success criteria:
        ///   Both record methods are called once with the values returned by the repository.
        /// </summary>
        [Fact]
        public async Task TriggerGetMetrics_RecordsValuesFromRepository()
        {
            // Arrange
            var repository = new Mock<IMetricsRepository>();
            var backlogMetrics = new Mock<IBacklogMetrics>();
            repository
                .Setup(r => r.GetBacklogMetrics(It.IsAny<CancellationToken>()))
                .ReturnsAsync((RegisteredCount: 5L, RegisteredOldestAgeSeconds: 12.5, RetryExhaustedCount: 3L));

            var service = new MetricsService(repository.Object, backlogMetrics.Object);

            // Act
            await service.TriggerGetMetrics(TestContext.Current.CancellationToken);

            // Assert
            backlogMetrics.Verify(m => m.RecordRegistered(5, 12.5), Times.Once);
            backlogMetrics.Verify(m => m.RecordRetryExhausted(3), Times.Once);
        }
    }
}
