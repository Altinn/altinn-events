using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// Test class for the TraceLogService
    /// </summary>
    public class TraceLogServiceTest
    {
        private readonly CloudEvent _cloudEvent = new()
        {
            Id = Guid.NewGuid().ToString()
        };

        /// <summary>
        /// Scenario:
        ///   Save a log entry upon saving a cloud event to persistence
        /// Expected result:
        ///   Cloud Event id is returned
        /// Success criteria:
        ///   TraceLogRepository.CreateTraceLogEntry is called once  
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Create_TraceLogRegisteredEntry_CreateTraceLogEntryCalledOnce()
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, NullLogger<TraceLogService>.Instance);

            // Act
            var result = await traceLogService.CreateRegisteredEntry(_cloudEvent);

            // Assert
            Assert.Equal(_cloudEvent.Id, result);
            traceLogRepositoryMock.Verify(
                x => x.CreateTraceLogEntry(It.IsAny<TraceLog>()), Times.Once);
        }

        /// <summary>
        /// Scenario: Save a log entry upon saving a cloud event to persistence
        /// Expected result:
        ///   Cloud id is returned
        /// Success criteria:
        ///   Activity is passed to repository layer
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Create_TraceLogRegisteredEntry_RegisteredActivityPassed()
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, NullLogger<TraceLogService>.Instance);

            // Act
            var result = await traceLogService.CreateRegisteredEntry(_cloudEvent);

            // Assert
            Assert.Equal(_cloudEvent.Id, result);
            traceLogRepositoryMock.Verify(
                x => x.CreateTraceLogEntry(It.Is<TraceLog>(y => y.Activity == TraceLogActivity.Registered)), Times.Once);
        }

        /// <summary>
        /// Scenario: Save a log entry upon receiving a response status code from consumer endpoint
        /// Expected result:
        ///   Cloud id is returnde
        /// Success criteria:
        ///   Status code is passed to repository layer
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Create_TraceLogWebhookResponseEntry_StatusCodeIsPassed()
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, NullLogger<TraceLogService>.Instance);
            var statusCode = 200;

            // Act
            var result = await traceLogService.CreateWebhookResponseEntry(_cloudEvent, 1, "consumer", new Uri("http://localhost"), statusCode);

            // Assert
            Assert.Equal(_cloudEvent.Id, result);
            traceLogRepositoryMock.Verify(
                x => x.CreateTraceLogEntry(It.Is<TraceLog>(y => y.ResponseCode == statusCode)), Times.Once);
        }

        /// <summary>
        /// Scenario: 
        ///   Save a log entry with an invalid cloud id should result in an empty string and an error logged
        /// Expected result:
        ///   empty string
        /// Success criteria:
        ///   Error is logged and empty string is returned
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Create_TraceLogWithSubscriptionDetailsFailsWithInvalidId_IsLoggedReturnsEmptyStringResult()
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            Mock<ILogger<ITraceLogService>> loggerMock = new Mock<ILogger<ITraceLogService>>();

            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, loggerMock.Object);
            var cloudEvent = new CloudEvent
            {
                Id = "123"
            };

            // Act
            var response = await traceLogService.CreateLogEntryWithSubscriptionDetails(cloudEvent, new Subscription(), TraceLogActivity.OutboundQueue);

            // Assert
            Assert.Equal(response, string.Empty);
            loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }
    }
}
