using System;
using System.Collections.Generic;
using System.Threading;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.UnitTest.Mocks;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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
        /// <summary>
        /// Scenario: Event generated by an Altinn app is to be pushed
        /// Expected result: Event source is modified to match sourceFilter in DB
        /// Success criteria: Event source input to repository method has correct format
        /// </summary>
        [Fact]
        public async void PostOutbound_AppSource_CorrectlyModifiedForRepositoryInput()
        {
            // Arrange
            string expectedSimplified = "https://ttd.apps.altinn.no/ttd/endring-av-navn-v2";

            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.completed");

            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock
                .Setup(r => r.GetSubscriptions(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Subscription>());

            var service = GetOutboundService(repositoryMock: repositoryMock.Object);

            // Act
            await service.PostOutbound(cloudEvent);

            // Assert
            repositoryMock.Verify(r => r.GetSubscriptions(It.Is<string>(s => s.Equals(expectedSimplified)), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Scenario: Event generated by an external source is to be pushed
        /// Expected result: Event source is not modified
        /// Success criteria: Event source input to repository method matches event.source
        /// </summary>
        [Fact]
        public async void PostOutbound_ExternalSource_UseEventSourceAsRepositoryInput()
        {
            // Arrange
            string expectedSimplified = "urn:testing-events:test-source";

            CloudEvent cloudEvent = GetCloudEvent(new Uri("urn:testing-events:test-source"), "/party/1337/", "app.instance.process.completed");

            Mock<ISubscriptionRepository> repositoryMock = new();
            repositoryMock
                .Setup(r => r.GetSubscriptions(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Subscription>());

            var service = GetOutboundService(repositoryMock: repositoryMock.Object);

            // Act
            await service.PostOutbound(cloudEvent);

            // Assert
            repositoryMock.Verify(r => r.GetSubscriptions(It.Is<string>(s => s.Equals(expectedSimplified)), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));
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
                .Setup(r => r.GetSubscriptions(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
        ///   Push an event. Three subscriptions are matching and is authorized
        /// Expected result:
        ///   The event are pushed to three different subscribers
        /// Success criteria:
        ///   The event is pushed to three subscribers
        /// </summary>
        [Fact]
        public async void Push_TwoMatchingAndValidSubscriptions_AddedToQueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.completed");

            EventsQueueClientMock queueClientMock = new();
            var service = GetOutboundService(queueClientMock);

            // Act
            await service.PostOutbound(cloudEvent);

            // Assert
            Assert.True(queueClientMock.OutboundQueue.ContainsKey(cloudEvent.Id));
            Assert.Equal(3, queueClientMock.OutboundQueue[cloudEvent.Id].Count);
        }

        /// <summary>
        /// Scenario:
        ///   Post an event for outbound push. One subscriptions are matching and is authorized
        /// Expected result:
        ///   The event are pushed to two different subscribers
        /// Success criteria:
        ///   The event is pushed to two subscribers
        /// </summary>
        [Fact]
        public async void Push_OneMatchingAndValidSubscriptions_AddedToQueue()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.movedTo.task_1");

            EventsQueueClientMock queueClientMock = new();
            var service = GetOutboundService(queueClientMock);

            // Act
            await service.PostOutbound(cloudEvent);

            // Assert
            Assert.True(queueClientMock.OutboundQueue.ContainsKey(cloudEvent.Id));
            Assert.Single(queueClientMock.OutboundQueue[cloudEvent.Id]);
        }

        /// <summary>
        /// Scenario:
        ///   Post an event for outbound push. One subscriptions are matching and is authorized
        /// Expected result:
        ///   The event are pushed to two different subscribers
        /// Success criteria:
        ///   The event is pushed to two subscribers
        /// </summary>
        [Fact]
        public async void Push_QueueReportsFailure_ErrorIsLogged()
        {
            // Arrange
            CloudEvent cloudEvent = GetCloudEvent(new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/123124"), "/party/1337/", "app.instance.process.movedTo.task_1");

            var queueMock = new Mock<IEventsQueueClient>();
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

        private static IOutboundService GetOutboundService(IEventsQueueClient queueMock = null, ISubscriptionRepository repositoryMock = null, ILogger<IOutboundService> loggerMock = null)
        {
            var services = new ServiceCollection();
            services.AddMemoryCache();
            var serviceProvider = services.BuildServiceProvider();
            var memoryCache = serviceProvider.GetService<IMemoryCache>();

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

            IAuthorization authorizationMock = new AuthorizationService(new PepWithPDPAuthorizationMockSI());
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

            return cloudEvent;
        }
    }
}
