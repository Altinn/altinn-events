﻿using System;
using System.Net;
using System.Runtime.InteropServices;
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
        /// Scenario:
        ///   A cloud event is saved as log even if guid is invalid.
        /// Expected result:
        ///   empty string is returned
        /// Success criteria:
        ///   Log entry for cloud id is set to null if Guid can't be parsed
        /// </summary>
        /// <param name="guid">string representation of a guid</param>
        /// <param name="expectedResult">the expected response based on whether the guid can be parsed or not</param>
        /// <returns></returns>
        [Theory]
        [InlineData(null, "")]
        [InlineData("d1525c79-cda8-4fef-b95c-feb3e7be89ec", "d1525c79-cda8-4fef-b95c-feb3e7be89ec")]
        public async Task Create_TraceLogRegisteredEntryWithGuid_ShouldSaveWithIdFieldAsNullOnlyWhenInvalid(string guid, string expectedResult)
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, NullLogger<TraceLogService>.Instance);
            var cloudEvent = new CloudEvent
            {
                Id = guid
            };
            
            // Act
            var result = await traceLogService.CreateRegisteredEntry(cloudEvent);
            
            // Assert
            Assert.Equal(expectedResult, result);
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
        /// Scenario: Save a log entry results in an exception thrown by the repository should result in an empty string and an error logged
        /// Expected result:
        ///   empty string is returned
        /// Success criteria:
        ///   Error is logged and empty string is returned
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Create_TraceLogRegisteredEntryWithInvalidParameter_ExceptionIsCaughtAndErrorLogged()
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            traceLogRepositoryMock.Setup(x => x.CreateTraceLogEntry(It.IsAny<TraceLog>())).Throws(new Exception());
            Mock<ILogger<TraceLogService>> loggerMock = new Mock<ILogger<TraceLogService>>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, loggerMock.Object);

            // Act
            var result = await traceLogService.CreateRegisteredEntry(_cloudEvent);

            // Assert
            Assert.Equal(string.Empty, result);
            loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
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
            LogEntryDto logEntry = new()
            {
                CloudEventId = _cloudEvent.Id,
                CloudEventResource = _cloudEvent["resource"]?.ToString(),
                Consumer = "consumer",
                SubscriptionId = 1,
                Endpoint = new Uri("https://localhost:3000"),
                StatusCode = HttpStatusCode.OK
            };

            // Act
            var result = await traceLogService.CreateWebhookResponseEntry(logEntry);

            // Assert
            Assert.Equal(_cloudEvent.Id, result);
            traceLogRepositoryMock.Verify(
                x => x.CreateTraceLogEntry(It.Is<TraceLog>(y => y.ResponseCode == statusCode)), Times.Once);
        }

        /// <summary>
        /// Scenario: 
        ///   Save a log entry with an invalid cloud id / GUID should result in an empty string returned
        /// Expected result:
        ///   empty string
        /// Success criteria:
        ///   empty string is returned
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
            loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Never);
        }

        /// <summary>
        /// Scenario: 
        ///   Save a log entry which results in an exception thrown, should result in an empty string returned, and an error logged
        /// Expected result:
        ///   empty string
        /// Success criteria:
        ///   empty string is returned and error is logged
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Create_TraceLogWithSubscriptionDetailsExceptionThrown_IsLoggedReturnsEmptyStringResult()
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            traceLogRepositoryMock.Setup(x => x.CreateTraceLogEntry(It.IsAny<TraceLog>())).Throws(new Exception());
            Mock<ILogger<ITraceLogService>> loggerMock = new Mock<ILogger<ITraceLogService>>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, loggerMock.Object);
            
            // Act
            var response = await traceLogService.CreateLogEntryWithSubscriptionDetails(_cloudEvent, new Subscription(), TraceLogActivity.OutboundQueue);
            
            // Assert
            Assert.Equal(response, string.Empty);
            loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        /// <summary>
        /// Scenario: 
        ///   Save a log entry with a valid cloud event. Guid is returned as string
        /// Expected result:
        ///   string representation of the GUID
        /// Success criteria:
        ///   No error is logged and GUID is returned
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Create_TraceLogWithSubscriptionDetails_ReturnsValidGuidIsNotLogged()
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            Mock<ILogger<ITraceLogService>> loggerMock = new Mock<ILogger<ITraceLogService>>();

            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, loggerMock.Object);

            // Act
            var response = await traceLogService.CreateLogEntryWithSubscriptionDetails(_cloudEvent, new Subscription(), TraceLogActivity.OutboundQueue);

            // Assert
            Assert.Equal(response, _cloudEvent.Id.ToString());
            loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Never);
        }

        /// <summary>
        /// Scenario: 
        ///   Save a log entry with an invalid dto object, missing essential properties, should result in an empty string and an error logged
        /// Expected result:
        ///   Empty string
        /// Success criteria:
        ///   Error is logged and empty string is returned
        /// </summary>
        /// <returns></returns>
        [Theory]
        [InlineData(null, HttpStatusCode.OK, "ce2b205a-4980-4ac2-bbc2-159d77478b87")]
        [InlineData("http://localhost", null, "ce2b205a-4980-4ac2-bbc2-159d77478b87")]
        [InlineData("http://localhost", HttpStatusCode.OK, null)]
        public async Task Create_TraceLogFromWebhookWithInvalidDto_ReturnsEmptyString(string endpoint, HttpStatusCode? httpStatusCode, string cloudEventId)
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            Mock<ILogger<ITraceLogService>> loggerMock = new Mock<ILogger<ITraceLogService>>();

            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, loggerMock.Object);
            LogEntryDto logEntry = new()
            {
                CloudEventId = cloudEventId,
                Endpoint = endpoint != null ? new Uri(endpoint) : null,
                StatusCode = httpStatusCode
            };

            // Act
            var response = await traceLogService.CreateWebhookResponseEntry(logEntry);

            // Assert
            Assert.Equal(response, string.Empty);
            loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Log entry should be saved with activity based on cloud event type and status code success, as returned by the http response
        /// Returns:
        ///   Either valid Guid or empty string if guid can't be parsed
        /// Success criteria:
        ///   Cloud event type platform.events.validatesubscription should result in activity EndpointValidationSuccess or EndpointValidationFailed, depending on status code.
        /// </summary>
        /// <param name="isSuccessStatusCode">Whether the status code response is successful or not</param>
        /// <returns></returns>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Create_TraceLogFromWebhookWithCloudEventType_ShouldSetActivityBasedOnSuccessStatusCode(bool isSuccessStatusCode)
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, NullLogger<TraceLogService>.Instance);

            // Act
            await traceLogService.CreateWebhookResponseEntry(new LogEntryDto
            {
                CloudEventId = _cloudEvent.Id,
                CloudEventResource = _cloudEvent["resource"]?.ToString(),
                Consumer = "consumer",
                SubscriptionId = 1,
                IsSuccessStatusCode = isSuccessStatusCode,
                Endpoint = new Uri("https://localhost:3000"),
                StatusCode = HttpStatusCode.OK,
                CloudEventType = "platform.events.validatesubscription"
            });

            traceLogRepositoryMock.Verify(
                x => x.CreateTraceLogEntry(It.Is<TraceLog>(y => y.Activity == TraceLogActivity.EndpointValidationSuccess)), isSuccessStatusCode ? Times.Once : Times.Never);

            traceLogRepositoryMock.Verify(
                x => x.CreateTraceLogEntry(It.Is<TraceLog>(y => y.Activity == TraceLogActivity.EndpointValidationFailed)), !isSuccessStatusCode ? Times.Once : Times.Never);
        }

        /// <summary>
        /// Scenario:
        ///   Log entry should be saved with activity based on cloud event type and status code success, as returned by the http response
        /// Returns:
        ///   Either valid Guid or empty string if guid can't be parsed
        /// Success criteria:
        ///   Cloud event type platform.events.validatesubscription should result in activity EndpointValidationSuccess or EndpointValidationFailed, depending on status code. Else: WebhookPostResponse
        /// </summary>
        /// <param name="type">string representation of a cloud event type</param>
        /// <returns></returns>
        [Theory]
        [InlineData("platform.events.validatesubscription")]
        [InlineData("platform.events")]
        [InlineData("")]
        [InlineData(null)]
        public async Task Create_TraceLogFromWebhookWithCloudEventType_ShouldSetActivityBasedOnType(string type)
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, NullLogger<TraceLogService>.Instance);

            // Act
            await traceLogService.CreateWebhookResponseEntry(new LogEntryDto
            {
                CloudEventId = _cloudEvent.Id,
                CloudEventResource = _cloudEvent["resource"]?.ToString(),
                Consumer = "consumer",
                SubscriptionId = 1,
                Endpoint = new Uri("https://localhost:3000"),
                StatusCode = HttpStatusCode.OK,
                CloudEventType = type
            });

            // Check for WebhookPostResponse activity only when NOT a validation event
            var expectedTimes = type != null && string.Equals(type, "platform.events.validatesubscription", StringComparison.Ordinal) ? Times.Never() : Times.Once();
        
            traceLogRepositoryMock.Verify(
            x => x.CreateTraceLogEntry(It.Is<TraceLog>(y => y.Activity == TraceLogActivity.WebhookPostResponse)), expectedTimes);
        }

        /// <summary>
        /// Scenario: 
        ///   Save a log entry with an invalid cloudEventId, should result in an empty string and an error logged
        /// Expected result:
        ///   Empty string
        /// Success criteria:
        ///   Error is logged and empty string is returned
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Create_TraceLogFromWebhookWithInvalidGuid_ReturnsEmptyString()
        {
            // Arrange
            var traceLogRepositoryMock = new Mock<ITraceLogRepository>();
            Mock<ILogger<ITraceLogService>> loggerMock = new Mock<ILogger<ITraceLogService>>();
            var traceLogService = new TraceLogService(traceLogRepositoryMock.Object, loggerMock.Object);
            LogEntryDto logEntry = new()
            {
                CloudEventId = "123"
            };

            // Act
            var response = await traceLogService.CreateWebhookResponseEntry(logEntry);

            // Assert
            Assert.Equal(response, string.Empty);
            loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }
    }
}
