using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Common.Models;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Telemetry;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.UnitTest.Mocks;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// Represents a collection of integration tests of the <see cref="OutboundService"/>.
    /// </summary>
    public class OutboundServiceTests
    {
        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        [Fact]
        public async Task PostOutbound_AppEvent_SourceFilterIsSimplified()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.completed", "urn:altinn:resource:app_ttd_endring-av-navn-v2");

            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock
                .Setup(r => r.GetSubscriptions(
                    It.Is<string>(s => s.Equals("urn:altinn:resource:app_ttd_endring-av-navn-v2")),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Subscription>() { GetSubscription() });

            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueOutbound(It.IsAny<string>())).ReturnsAsync(new QueuePostReceipt { Success = true });

            var service = GetOutboundService(queueMock: queueMock.Object, repositoryMock: repositoryMock.Object);

            // Act
            await service.PostOutbound(cloudEvent, CancellationToken.None);

            // Assert
            repositoryMock.VerifyAll();

            queueMock.Verify(r => r.EnqueueOutbound(It.IsAny<string>()), Times.Once);
        }

        /// <summary>
        /// Scenario: Event without any matching subscriptions is registered
        /// Expected result: Method returns successfully
        /// Success criteria: No requests are sent through authorization as the event shouldn't be pushed
        /// </summary>
        [Fact]
        public async Task PostOutbound_NoMatchingSubscription_ExecutionStops()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://domstol.no"), "/person/16069412345/", "test.automated");

            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock
                .Setup(r => r.GetSubscriptions(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Subscription>());

            Mock<IAuthorization> authorizationMock = new();
            authorizationMock
                .Setup(a => a.AuthorizeConsumerForAltinnAppEvent(It.IsAny<CloudEvent>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueOutbound(It.IsAny<string>()));

            var service = GetOutboundService(queueMock: queueMock.Object, repositoryMock: repositoryMock.Object, authorizationMock: authorizationMock.Object);

            // Act
            await service.PostOutbound(cloudEvent, CancellationToken.None);

            // Assert
            repositoryMock.VerifyAll();
            authorizationMock.Verify(a => a.AuthorizeConsumerForAltinnAppEvent(It.IsAny<CloudEvent>(), It.IsAny<string>()), Times.Never);
            queueMock.Verify(r => r.EnqueueOutbound(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Scenario: Event without subject is to be pushed
        /// Expected result: Method returns successfully
        /// Success criteria: Subscription repository is called to retrieve subscriptions.
        /// </summary>
        /// <remarks>
        /// A workaround for generic events untill full support is in place. 
        /// This test should start failing once altinn/altinn-events#109 is implemented. 
        /// The test may then be removed.
        /// </remarks>
        [Fact]
        public async Task PostOutboundEventWithoutSubject_TwoMatchingSubscriptions_AddedToQueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("urn:testing-events:test-source"), null, "app.instance.process.completed", "urn:altinn:resource:test-source");

            Mock<IEventsQueueClient> queueMock = new();
            queueMock.Setup(q => q.EnqueueOutbound(It.IsAny<string>()))
                .ReturnsAsync(new QueuePostReceipt { Success = true });

            Mock<IAuthorization> authorizationMock = new();
            authorizationMock
                .Setup(a => a.AuthorizeConsumerForGenericEvent(
                    It.IsAny<CloudEvent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = GetOutboundService(queueMock.Object, authorizationMock: authorizationMock.Object);

            // Act
            await service.PostOutbound(cloudEvent, CancellationToken.None);

            // Assert
            queueMock.Verify(r => r.EnqueueOutbound(It.IsAny<string>()), Times.Exactly(1));
        }

        /// <summary>
        /// Scenario: Event without subject is to be pushed
        /// Expected result: Method returns successfully
        /// Success criteria: Subscription repository is called to retrieve subscriptions.
        /// </summary>
        /// <remarks>
        /// A workaround for generic events untill full support is in place. 
        /// This test should start failing once altinn/altinn-events#109 is implemented. 
        /// The test may then be removed.
        /// </remarks>
        [Fact]
        public async Task PostOutboundEventWithoutSubject_ConsumerNotAuthorized_NotAddedToQueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("urn:testing-events:test-source"), null, "app.instance.process.completed", "urn:altinn:resource:test-source");

            Mock<IEventsQueueClient> queueMock = new();
            queueMock.Setup(q => q.EnqueueOutbound(It.IsAny<string>()))
                .ReturnsAsync(new QueuePostReceipt { Success = true });

            Mock<IAuthorization> authorizationMock = new();
            authorizationMock
                .Setup(a => a.AuthorizeConsumerForGenericEvent(
                    It.IsAny<CloudEvent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var service = GetOutboundService(queueMock.Object, authorizationMock: authorizationMock.Object);

            // Act
            await service.PostOutbound(cloudEvent, CancellationToken.None);

            // Assert
            queueMock.Verify(r => r.EnqueueOutbound(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Scenario: Subscription Repository finds a single match for an outbound event
        /// Expected result: Consumer is not authorized against the event source 
        /// Success criteria: QueueClient is never called to send the outbound event
        /// </summary>
        [Fact]
        public async Task PostOutbound_ConsumerNotAuthorized_QueueClientNeverCalled()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.completed", "urn:altinn:resource:app_ttd_endring-av-navn-v2");

            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock
                .Setup(r => r.GetSubscriptions(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Subscription>() { new Subscription { Consumer = "/org/nav" } });
            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueOutbound(It.IsAny<string>()));

            Mock<IAuthorization> authorizationMock = new();
            authorizationMock
                .Setup(a => a.AuthorizeConsumerForGenericEvent(
                    It.IsAny<CloudEvent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var service = GetOutboundService(queueMock: queueMock.Object, repositoryMock: repositoryMock.Object, authorizationMock: authorizationMock.Object);

            // Act
            await service.PostOutbound(cloudEvent, CancellationToken.None);

            // Assert
            repositoryMock.VerifyAll();

            queueMock.Verify(r => r.EnqueueOutbound(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Scenario:
        ///   Push an event. Two subscriptions are matching and is authorized
        /// Expected result:
        ///   The event are pushed to three different subscribers
        /// Success criteria:
        ///   The enqueue is called for the queue client excactly twice
        /// </summary>
        [Fact]
        public async Task Push_TwoMatchingAndValidSubscriptions_AddedToQueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.completed", "urn:altinn:resource:app_ttd_endring-av-navn-v2");

            Mock<IEventsQueueClient> queueMock = new();
            queueMock.Setup(q => q.EnqueueOutbound(It.IsAny<string>()))
                .ReturnsAsync(new QueuePostReceipt { Success = true });

            Mock<IAuthorization> authorizationMock = new();
            authorizationMock
                .Setup(a => a.AuthorizeConsumerForAltinnAppEvent(It.IsAny<CloudEvent>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var service = GetOutboundService(queueMock.Object, authorizationMock: authorizationMock.Object);

            // Act
            await service.PostOutbound(cloudEvent, CancellationToken.None);

            // Assert
            queueMock.Verify(r => r.EnqueueOutbound(It.IsAny<string>()), Times.Exactly(4));
        }

        /// <summary>
        /// Scenario:
        ///   Post a valid event for outbound push. Queue client returns a non-success result.
        /// Expected result:
        ///   An error is logged.
        /// Success criteria:
        ///   Log message starts with expected string.
        /// </summary>
        [Fact]
        public async Task Push_QueueReportsFailure_ErrorIsLogged()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.movedTo.task_1", "urn:altinn:resource:app_ttd_endring-av-navn-v2");

            Mock<IEventsQueueClient> queueMock = new();
            queueMock.Setup(q => q.EnqueueOutbound(It.IsAny<string>()))
                .ReturnsAsync(new QueuePostReceipt { Success = false });

            Mock<IAuthorization> authorizationMock = new();
            authorizationMock
                .Setup(a => a.AuthorizeConsumerForAltinnAppEvent(It.IsAny<CloudEvent>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var loggerMock = new Mock<ILogger<OutboundService>>();

            var service = GetOutboundService(queueMock: queueMock.Object, loggerMock: loggerMock.Object, authorizationMock: authorizationMock.Object, telemetryClient: new TelemetryClient());

            // Act
            await service.PostOutbound(cloudEvent, CancellationToken.None);

            // Assert
            loggerMock.Verify(
               x => x.Log(
                   LogLevel.Error,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("OutboundService EnqueueOutbound Failed to send event envelope", StringComparison.InvariantCultureIgnoreCase)),
                   It.IsAny<Exception>(),
                   It.IsAny<Func<It.IsAnyType, Exception, string>>()),
               Times.Once);
            queueMock.VerifyAll();
        }

        /// <summary>
        /// Scenario:
        ///   Post a valid event for outbound push. The event is enqueued as a RetryableEventWrapper
        /// Expected result:
        ///   The event is wrapped and contains correct metadata
        /// Success criteria:
        ///   A single call is made to enqueue the outbound event
        /// </summary>
        [Fact]
        public async Task PostOutbound_EnqueuesRetryableEventWrapper_WithCorrectMetadata()
        {
            // Arrange
            var expectedSubject = "uniqueSubject";
            var expectedType = "app.instance.process.completed";
            var expectedResource = "urn:altinn:resource:app_ttd_endring-av-navn-v2";

            CloudEvent cloudEvent = GetCloudEvent(
                new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"),
                expectedSubject,
                expectedType,
                expectedResource);

            string capturedQueueMessage = null;

            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueOutbound(It.IsAny<string>()))
                .Callback<string>(msg => capturedQueueMessage = msg) // assign the captured message
                .ReturnsAsync(new QueuePostReceipt { Success = true });

            Mock<IAuthorization> authorizationMock = new();
            authorizationMock
                .Setup(a => a.AuthorizeConsumerForAltinnAppEvent(It.IsAny<CloudEvent>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var sut = GetOutboundService(
                queueMock: queueMock.Object,
                authorizationMock: authorizationMock.Object);

            // Act
            await sut.PostOutbound(cloudEvent, CancellationToken.None);

            // Assert
            queueMock.Verify(r => r.EnqueueOutbound(It.IsAny<string>()), Times.AtLeastOnce());

            Assert.NotNull(capturedQueueMessage);

            // Deserialize to RetryableEventWrapper
            var wrapper = DeserializeToRetryableEventWrapper(capturedQueueMessage);

            // Verify wrapper properties
            Assert.NotNull(wrapper);
            Assert.Equal(0, wrapper.DequeueCount);
            Assert.NotNull(wrapper.Payload);

            // Verify we can deserialize the payload back to CloudEventEnvelope
            var envelope = DeserializeToCloudEventEnvelope(wrapper.Payload);

            // Verify envelope contents
            Assert.NotNull(envelope);
            Assert.Equal(cloudEvent.Type, envelope.CloudEvent.Type);
            Assert.Equal(cloudEvent.Source.ToString(), envelope.CloudEvent.Source.ToString());
            Assert.Equal(cloudEvent.Subject, envelope.CloudEvent.Subject);
        }

        [Fact]
        public void DeserializeToRetryableEventWrapper_InvalidJsonAndEmptyString_ReturnNull()
        {
            // Arrange
            string invalidJson = "{\"CorrelationId\":\"test-id\", \"DequeueCount\":0, malformed json}";
            string emptyString = string.Empty;

            // Act
            var result = DeserializeToRetryableEventWrapper(invalidJson);
            var resultEmpty = DeserializeToRetryableEventWrapper(emptyString);

            // Assert
            Assert.Null(result);
            Assert.Null(resultEmpty);
        }

        private static IOutboundService GetOutboundService(
            IEventsQueueClient queueMock = null,
            Mock<ITraceLogService> traceLogServiceMock = null,
            ISubscriptionRepository repositoryMock = null,
            IAuthorization authorizationMock = null,
            MemoryCache memoryCache = null,
            ILogger<OutboundService> loggerMock = null,
            TelemetryClient telemetryClient = null)
        {
            if (loggerMock == null)
            {
                loggerMock = new Mock<ILogger<OutboundService>>().Object;
            }

            if (queueMock == null)
            {
                queueMock = new EventsQueueClientMock();
            }

            if (traceLogServiceMock == null)
            {
                traceLogServiceMock = new Mock<ITraceLogService>();
            }

            if (repositoryMock == null)
            {
                repositoryMock = new SubscriptionRepositoryMock();
            }

            if (authorizationMock == null)
            {
                Mock<IClaimsPrincipalProvider> claimsPrincipalMock = new();
                Mock<IRegisterService> registerServiceMock = new();

                authorizationMock = new AuthorizationService(new PepWithPDPAuthorizationMockSI(), claimsPrincipalMock.Object, registerServiceMock.Object);
            }

            if (memoryCache == null)
            {
                memoryCache = new MemoryCache(new MemoryCacheOptions());
            }

            var service = new OutboundService(
                queueMock,
                traceLogServiceMock.Object,
                repositoryMock,
                authorizationMock,
                Options.Create(new PlatformSettings
                {
                    SubscriptionCachingLifetimeInSeconds = 60,
                    AppsDomain = "apps.altinn.no"
                }),
                memoryCache,
                loggerMock,
                telemetryClient);

            return service;
        }

        private static Subscription GetSubscription()
        {
            return new Subscription()
            {
                Id = 16,
                SourceFilter = new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2"),
                Consumer = "/org/ttd",
                CreatedBy = "/org/ttd",
                TypeFilter = "app.instance.process.completed"
            };
        }

        private static CloudEvent GetCloudEvent(Uri source, string subject, string type, string resoure = "urn:altinn:resource:testresource")
        {
            CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                Source = source,
                Time = DateTime.Now,
                Subject = subject,
                Data = "something/extra",
            };

            cloudEvent.SetResourceIfNotDefined(resoure);

            return cloudEvent;
        }

        /// <summary>
        /// Deserializes a CloudEventEnvelope from a JSON string that embeds a CloudEvent
        /// in either "CloudEvent" or "cloudEvent".
        /// </summary>
        /// <param name="serializedEnvelope">The serialized envelope JSON.</param>
        /// <returns>The reconstructed CloudEventEnvelope with CloudEvent populated.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when JSON parsing fails or the "cloudEvent"/"CloudEvent" property is missing.
        /// </exception>
        private static CloudEventEnvelope DeserializeToCloudEventEnvelope(string serializedEnvelope)
        {
            var n = JsonNode.Parse(serializedEnvelope, new JsonNodeOptions { PropertyNameCaseInsensitive = true });

            if (n == null)
            {
                throw new ArgumentException("Failed to parse serialized envelope as JSON", nameof(serializedEnvelope));
            }

            var cloudEventNode = n["cloudEvent"] ?? n["CloudEvent"];
            if (cloudEventNode is null)
            {
                throw new ArgumentException("Serialized envelope does not contain a cloudEvent property", nameof(serializedEnvelope));
            }

            string serializedCloudEvent = cloudEventNode.ToString();
            var cloudEvent = DeserializeToCloudEvent(serializedCloudEvent);

            if (n is JsonObject obj)
            {
                // Remove both variants to be explicit regardless of parsed casing
                obj.Remove("cloudEvent");
                obj.Remove("CloudEvent");
            }

            CloudEventEnvelope cloudEventEnvelope = n.Deserialize<CloudEventEnvelope>()
                ?? throw new InvalidOperationException("Failed to deserialize CloudEventEnvelope");

            cloudEventEnvelope.CloudEvent = cloudEvent;

            return cloudEventEnvelope;
        }

        /// <summary>
        ///  Deserializes a json string to a the cloud event using a JsonEventFormatter
        /// </summary>
        /// <returns>The cloud event</returns>
        private static CloudEvent DeserializeToCloudEvent(string item)
        {
            var formatter = new JsonEventFormatter();

            var cloudEvent = formatter.DecodeStructuredModeMessage(new MemoryStream(Encoding.UTF8.GetBytes(item)), null, null);
            return cloudEvent;
        }

        private RetryableEventWrapper DeserializeToRetryableEventWrapper(string item)
        {
            try
            {
                var eventWrapper = JsonSerializer.Deserialize<RetryableEventWrapper>(item, _serializerOptions);
                return eventWrapper;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
