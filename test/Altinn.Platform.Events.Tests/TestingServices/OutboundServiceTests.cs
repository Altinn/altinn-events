using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.UnitTest.Mocks;

using CloudNative.CloudEvents;

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
        [Fact]
        public async Task PostOutbound_AppEvent_SourceFilterIsSimplified()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.completed");

            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock
                .Setup(r => r.GetSubscriptions(
                    It.IsAny<List<string>>(),
                    It.Is<string>(s => s.Equals("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2")),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Subscription>() { GetSubscription() });

            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueOutbound(It.IsAny<string>())).ReturnsAsync(new QueuePostReceipt { Success = true });

            var service = GetOutboundService(queueMock: queueMock.Object, repositoryMock: repositoryMock.Object);

            // Act
            await service.PostOutbound(cloudEvent);

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
                .Setup(r => r.GetSubscriptions(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            await service.PostOutbound(cloudEvent);

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
        public async void PostOutboundEventWithoutSubject_TwoMatchingSubscriptions_AddedToQueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("urn:testing-events:test-source"), null, "app.instance.process.completed");

            Mock<IEventsQueueClient> queueMock = new();
            queueMock.Setup(q => q.EnqueueOutbound(It.IsAny<string>()))
                .ReturnsAsync(new QueuePostReceipt { Success = true });

            Mock<IAuthorization> authorizationMock = new();
            authorizationMock
                .Setup(a => a.AuthorizeConsumerForGenericEvent(It.IsAny<CloudEvent>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var service = GetOutboundService(queueMock.Object, authorizationMock: authorizationMock.Object);

            // Act
            await service.PostOutbound(cloudEvent);

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
        public async void PostOutboundEventWithoutSubject_ConsumerNotAuthorized_NotAddedToQueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("urn:testing-events:test-source"), null, "app.instance.process.completed");

            Mock<IEventsQueueClient> queueMock = new();
            queueMock.Setup(q => q.EnqueueOutbound(It.IsAny<string>()))
                .ReturnsAsync(new QueuePostReceipt { Success = true });

            Mock<IAuthorization> authorizationMock = new();
            authorizationMock
                .Setup(a => a.AuthorizeConsumerForGenericEvent(It.IsAny<CloudEvent>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            var service = GetOutboundService(queueMock.Object, authorizationMock: authorizationMock.Object);

            // Act
            await service.PostOutbound(cloudEvent);

            // Assert
            queueMock.Verify(r => r.EnqueueOutbound(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Scenario: Subscription Repository finds a single match for an outbound event
        /// Expected result: Consumer is not authorized against the event source 
        /// Success criteria: QueueClient is never called to send the outbound event
        /// </summary>
        [Fact]
        public async void PostOutbound_ConsumerNotAuthorized_QueueClientNeverCalled()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.completed");

            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock
                .Setup(r => r.GetSubscriptions(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Subscription>() { new Subscription { Consumer = "/org/nav" } });
            Mock<IEventsQueueClient> queueMock = new();
            queueMock
                .Setup(q => q.EnqueueOutbound(It.IsAny<string>()));

            var service = GetOutboundService(queueMock: queueMock.Object, repositoryMock: repositoryMock.Object);

            // Act
            await service.PostOutbound(cloudEvent);

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
        public async void Push_TwoMatchingAndValidSubscriptions_AddedToQueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.completed");

            Mock<IEventsQueueClient> queueMock = new();
            queueMock.Setup(q => q.EnqueueOutbound(It.IsAny<string>()))
                .ReturnsAsync(new QueuePostReceipt { Success = true });

            var service = GetOutboundService(queueMock.Object);

            // Act
            await service.PostOutbound(cloudEvent);

            // Assert
            queueMock.Verify(r => r.EnqueueOutbound(It.IsAny<string>()), Times.Exactly(2));
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
        public async void Push_QueueReportsFailure_ErrorIsLogged()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.movedTo.task_1");

            Mock<IEventsQueueClient> queueMock = new();
            queueMock.Setup(q => q.EnqueueOutbound(It.IsAny<string>()))
                .ReturnsAsync(new QueuePostReceipt { Success = false });

            var loggerMock = new Mock<ILogger<IOutboundService>>();

            var service = GetOutboundService(queueMock: queueMock.Object, loggerMock: loggerMock.Object);

            // Act
            await service.PostOutbound(cloudEvent);

            // Assert
            loggerMock.Verify(
               x => x.Log(
                   LogLevel.Error,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((o, t) => o.ToString().StartsWith("// OutboundService // EnqueueOutbound // Failed to send event envelope", StringComparison.InvariantCultureIgnoreCase)),
                   It.IsAny<Exception>(),
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
               Times.Once);
            queueMock.VerifyAll();
        }

        private static IOutboundService GetOutboundService(
            IEventsQueueClient queueMock = null,
            ISubscriptionRepository repositoryMock = null,
            IAuthorization authorizationMock = null,
            MemoryCache memoryCache = null,
            ILogger<IOutboundService> loggerMock = null)
        {
            if (loggerMock == null)
            {
                loggerMock = new Mock<ILogger<IOutboundService>>().Object;
            }

            if (queueMock == null)
            {
                queueMock = new EventsQueueClientMock();
            }

            if (repositoryMock == null)
            {
                repositoryMock = new SubscriptionRepositoryMock();
            }

            if (authorizationMock == null)
            {
                Mock<IClaimsPrincipalProvider> claimsPrincipalMock = new Mock<IClaimsPrincipalProvider>();

                authorizationMock = new AuthorizationService(new PepWithPDPAuthorizationMockSI(), claimsPrincipalMock.Object);
            }

            if (memoryCache == null)
            {
                memoryCache = new MemoryCache(new MemoryCacheOptions());
            }

            var service = new OutboundService(
                queueMock,
                repositoryMock,
                authorizationMock,
                Options.Create(new PlatformSettings
                {
                    SubscriptionCachingLifetimeInSeconds = 60,
                    AppsDomain = "apps.altinn.no"
                }),
                memoryCache,
                loggerMock);

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

        private static Subscription GetGenericEventsSubscription()
        {
            return new Subscription()
            {
                Id = 16,
                SourceFilter = new Uri("urn:testing-events:test-source"),
                Consumer = "/org/ttd",
                CreatedBy = "/org/ttd",
                TypeFilter = "app.instance.process.completed"
            };
        }

        private static CloudEvent GetCloudEvent(Uri source, string subject, string type)
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

            cloudEvent.SetResourceIfNotDefined("urn:altinn:resource:testresource");

            return cloudEvent;
        }
    }
}
