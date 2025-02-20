using System;
using System.Threading.Tasks;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using CloudNative.CloudEvents;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// Test class for the TraceLogService
    /// </summary>
    public class TraceLogServiceTest
    {
        public TraceLogServiceTest()
        {
        }

        [Fact]
        public async Task Create_TraceLogRegisteredEntry_CreateTraceLogEntryCalledOnce()
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object);
            CloudEvent cloudEvent = new() 
            {
                Id = Guid.NewGuid().ToString()
            };

            // Act
            await traceLogService.CreateTraceLogRegisteredEntry(cloudEvent);

            // Assert
            traceLogRepositoryMock.Verify(
                x => x.CreateTraceLogEntry(It.IsAny<TraceLog>()), Times.Once);
        }

        [Fact]
        public async Task Create_TraceLogRegisteredEntry_RegisteredActivityPassed()
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object);
            CloudEvent cloudEvent = new()
            {
                Id = Guid.NewGuid().ToString()
            };

            // Act
            await traceLogService.CreateTraceLogRegisteredEntry(cloudEvent);
            
            // Assert
            traceLogRepositoryMock.Verify(
                x => x.CreateTraceLogEntry(It.Is<TraceLog>(y => y.Activity == TraceLogActivity.Registered)), Times.Once);
        }
    }
}
