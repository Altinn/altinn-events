using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;

using Moq;

using Npgsql;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// A collection of tests related to <see cref="RegisteredEventsProcessingService"/>.
    /// </summary>
    public class RegisteredEventsProcessingServiceTests
    {
        private readonly Mock<ICloudEventRepository> _cloudEventRepositoryMock;
        private readonly Mock<IUnitOfWorkRepository> _unitOfWorkRepositoryMock;
        private readonly Mock<IOutboundService> _outboundServiceMock;
        private readonly Mock<ILogger<RegisteredEventsProcessingService>> _loggerMock;
        private readonly UnitOfWork _unitOfWork;

        public RegisteredEventsProcessingServiceTests()
        {
            _cloudEventRepositoryMock = new();
            _unitOfWorkRepositoryMock = new();
            _outboundServiceMock = new();
            _loggerMock = new();

            _unitOfWork = new UnitOfWork
            {
                Connection = new NpgsqlConnection(),
                Transaction = null!
            };

            _unitOfWorkRepositoryMock
                .Setup(u => u.StartUnitOfWork())
                .ReturnsAsync(_unitOfWork);
        }

        /// <summary>
        /// Scenario:
        ///   TryProcessEvent is called but no registered event is available to claim.
        /// Expected result:
        ///   The unit of work is rolled back and false is returned.
        /// Success criteria:
        ///   RollbackUnitOfWork is called once; outbound delivery and commit are never attempted.
        /// </summary>
        [Fact]
        public async Task TryProcessEvent_NoRegisteredEventAvailable_RollsBackAndReturnsFalse()
        {
            // Arrange
            _cloudEventRepositoryMock
                .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ClaimedEvent)null);

            var target = GetTarget();

            // Act
            bool result = await target.TryProcessEvent(CancellationToken.None);

            // Assert
            Assert.False(result);
            _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(_unitOfWork), Times.Once);
            _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
            _outboundServiceMock.Verify(o => o.PostOutbound(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
        }

        /// <summary>
        /// Scenario:
        ///   TryProcessEvent claims a registered event and outbound delivery succeeds.
        /// Expected result:
        ///   The event is marked processed and the unit of work is committed.
        /// Success criteria:
        ///   PostOutbound, MarkEventProcessedAsync, and CommitUnitOfWork are all called once; true is returned.
        /// </summary>
        [Fact]
        public async Task TryProcessEvent_EventClaimedAndOutboundSucceeds_MarksProcessedAndCommits()
        {
            // Arrange
            ClaimedEvent claimedEvent = new()
            {
                SequenceNo = 42,
                CloudEvent = GetCloudEvent()
            };

            _cloudEventRepositoryMock
                .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
                .ReturnsAsync(claimedEvent);

            _outboundServiceMock
                .Setup(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true))
                .Returns(Task.CompletedTask);

            var target = GetTarget();

            // Act
            bool result = await target.TryProcessEvent(CancellationToken.None);

            // Assert
            Assert.True(result);
            _outboundServiceMock.Verify(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true), Times.Once);
            _cloudEventRepositoryMock.Verify(r => r.MarkEventProcessedAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(_unitOfWork), Times.Once);
            _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        }

        /// <summary>
        /// Scenario:
        ///   TryProcessEvent claims a registered event but outbound delivery throws.
        /// Expected result:
        ///   The unit of work is rolled back (releasing the claim so the event can be retried later)
        ///   and false is returned. Retry tracking (retrycount/lastretried) is not yet implemented.
        /// Success criteria:
        ///   RollbackUnitOfWork is called once; MarkEventProcessedAsync and CommitUnitOfWork are never called.
        /// </summary>
        [Fact]
        public async Task TryProcessEvent_OutboundThrows_RollsBackAndReturnsFalse()
        {
            // Arrange
            ClaimedEvent claimedEvent = new()
            {
                SequenceNo = 7,
                CloudEvent = GetCloudEvent()
            };

            _cloudEventRepositoryMock
                .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
                .ReturnsAsync(claimedEvent);

            _outboundServiceMock
                .Setup(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true))
                .ThrowsAsync(new InvalidOperationException("Outbound delivery failed"));

            var target = GetTarget();

            // Act
            bool result = await target.TryProcessEvent(CancellationToken.None);

            // Assert
            Assert.False(result);
            _cloudEventRepositoryMock.Verify(r => r.MarkEventProcessedAsync(It.IsAny<UnitOfWork>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
            _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(_unitOfWork), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Starting the unit of work itself fails (e.g. connection unavailable).
        /// Expected result:
        ///   The error is logged and false is returned without attempting to claim an event.
        /// Success criteria:
        ///   ClaimRegisteredEventAsync is never called; an error is logged once.
        /// </summary>
        [Fact]
        public async Task TryProcessEvent_StartUnitOfWorkThrows_LogsErrorAndReturnsFalse()
        {
            // Arrange
            _unitOfWorkRepositoryMock
                .Setup(u => u.StartUnitOfWork())
                .ThrowsAsync(new InvalidOperationException("Connection unavailable"));

            var target = GetTarget();

            // Act
            bool result = await target.TryProcessEvent(CancellationToken.None);

            // Assert
            Assert.False(result);
            _cloudEventRepositoryMock.Verify(r => r.ClaimRegisteredEventAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()), Times.Never);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Outbound delivery succeeds, but marking the event processed throws.
        /// Expected result:
        ///   The unit of work is rolled back rather than committed, and false is returned.
        /// Success criteria:
        ///   RollbackUnitOfWork is called once; CommitUnitOfWork is never called.
        /// </summary>
        [Fact]
        public async Task TryProcessEvent_MarkEventProcessedThrows_RollsBackAndReturnsFalse()
        {
            // Arrange
            ClaimedEvent claimedEvent = new()
            {
                SequenceNo = 99,
                CloudEvent = GetCloudEvent()
            };

            _cloudEventRepositoryMock
                .Setup(r => r.ClaimRegisteredEventAsync(_unitOfWork, It.IsAny<CancellationToken>()))
                .ReturnsAsync(claimedEvent);

            _outboundServiceMock
                .Setup(o => o.PostOutbound(claimedEvent.CloudEvent, It.IsAny<CancellationToken>(), true))
                .Returns(Task.CompletedTask);

            _cloudEventRepositoryMock
                .Setup(r => r.MarkEventProcessedAsync(_unitOfWork, claimedEvent.SequenceNo, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            var target = GetTarget();

            // Act
            bool result = await target.TryProcessEvent(CancellationToken.None);

            // Assert
            Assert.False(result);
            _unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
            _unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(_unitOfWork), Times.Once);
        }

        private RegisteredEventsProcessingService GetTarget()
        {
            return new RegisteredEventsProcessingService(
                _cloudEventRepositoryMock.Object,
                _unitOfWorkRepositoryMock.Object,
                _outboundServiceMock.Object,
                _loggerMock.Object);
        }

        private static CloudEvent GetCloudEvent()
        {
            return new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "instance.created",
                Source = new Uri("https://ttd.apps.altinn.no/ttd/apps-test/"),
                Time = DateTimeOffset.Now,
                Subject = "/party/456456"
            };
        }
    }
}
