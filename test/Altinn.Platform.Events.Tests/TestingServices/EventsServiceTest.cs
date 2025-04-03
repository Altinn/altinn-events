using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Profile.Tests.Testdata;
using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    /// <summary>
    /// A collection of tests related to <see cref="EventsService"/>.
    /// </summary>
    public class EventsServiceTest
    {
        private readonly ICloudEventRepository _repositoryMock;
        private readonly IEventsQueueClient _queueMock;
        private readonly Mock<IRegisterService> _registerMock;
        private readonly Mock<IAuthorization> _authorizationMock;
        private readonly Mock<ILogger<EventsService>> _loggerMock;

        public EventsServiceTest()
        {
            _repositoryMock = new CloudEventRepositoryMock();
            _queueMock = new EventsQueueClientMock();
            _registerMock = new();
            _authorizationMock = new();
            _loggerMock = new();
        }

        /// <summary>
        /// Scenario:
        ///   Push cloud event to events-inbound queue.
        /// Expected result:
        ///   Returns the id of the previously created document.
        /// Success criteria:
        ///   The response is a non-empty string.
        /// </summary>
        [Fact]
        public async Task PushSavedEvent_EventSuccessfullyPushed_IdReturned()
        {
            // Arrange
            EventsService eventsService = GetEventsService();

            // Act
            string actual = await eventsService.PostInbound(GetCloudEventFromApp());

            // Assert
            Assert.NotEmpty(actual);
        }

        /// <summary>
        /// Scenario:
        ///   Store an event, but push to queue fails.
        /// Expected result:
        /// Event is stored and eventId returned.
        /// Success criteria:
        ///  Error is logged.
        /// </summary>
        [Fact]
        public async Task RegisterNewEvent_PushEventFails_ErrorIsLogged()
        {
            // Arrange
            Mock<IEventsQueueClient> queueMock = new Mock<IEventsQueueClient>();
            queueMock.Setup(q => q.EnqueueRegistration(It.IsAny<string>())).ReturnsAsync(new QueuePostReceipt { Success = false, Exception = new Exception("The push failed due to something") });

            Mock<ILogger<EventsService>> logger = new Mock<ILogger<EventsService>>();
            EventsService eventsService = GetEventsService(loggerMock: logger, queueMock: queueMock.Object);

            // Act
            await Assert.ThrowsAsync<Exception>(() => eventsService.RegisterNew(GetCloudEventFromApp()));

            // Assert
            logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Push to Inbound queue fails.
        /// Expected result:
        ///   Error returned to caller.
        /// Success criteria:
        ///  Error is logged.
        /// </summary>
        [Fact]
        public async Task PushSavedEvent_PushEventFails_ErrorIsLogged()
        {
            // Arrange
            Mock<IEventsQueueClient> queueMock = new Mock<IEventsQueueClient>();
            queueMock.Setup(q => q.EnqueueInbound(It.IsAny<string>())).ReturnsAsync(new QueuePostReceipt { Success = false, Exception = new Exception("The push failed due to something") });

            Mock<ILogger<EventsService>> logger = new Mock<ILogger<EventsService>>();
            EventsService eventsService = GetEventsService(loggerMock: logger, queueMock: queueMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => eventsService.PostInbound(GetCloudEventFromApp()));
            logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Get events based on from and party Id
        /// Expected result:
        ///   A single event is returned.
        /// Success criteria:
        ///  PartyId is coverted to correct subject and matched in the repository.
        /// </summary>
        [Fact]
        public async Task GetAppEvents_QueryIncludesFromAndPartyId_RetrievesCorrectNumberOfEvents()
        {
            // Arrange
            int expectedCount = 2;
            string expectedSubject = "/party/54321";

            EventsService eventsService = GetEventsService(repositoryMock: new CloudEventRepositoryMock(2));

            // Act
            List<CloudEvent> actual = await eventsService.GetAppEvents(string.Empty, new DateTime(2020, 06, 17), null, 54321, new List<string>() { }, null, new List<string>() { }, null, null);

            // Assert
            Assert.Equal(expectedCount, actual.Count);
            Assert.Equal(expectedSubject, actual.First().Subject);
        }

        /// <summary>
        /// Scenario:
        ///   Get events based on after.
        /// Expected result:
        ///   A single event is returned.
        /// Success criteria:
        ///  Passes on the after parameter to the repository.
        /// </summary>
        [Fact]
        public async Task GetAppEvents_QueryIncludesAfter_RetrievesCorrectNumberOfEvents()
        {
            // Arrange
            int expectedCount = 3;
            EventsService eventsService = GetEventsService(repositoryMock: new CloudEventRepositoryMock(2));

            // Act
            List<CloudEvent> actual = await eventsService.GetAppEvents("e31dbb11-2208-4dda-a549-92a0db8c8808", null, null, 0, new List<string>() { }, null, new List<string>() { }, null, null);

            // Assert
            Assert.Equal(expectedCount, actual.Count);
        }

        /// <summary>
        /// Scenario:
        ///   Get events with various input 
        /// Expected result:
        ///   Conditions to evaluate input to repository method are evaluated.
        /// Success criteria:
        ///  Conditions are evaluated correctly.
        /// </summary>
        [Fact]
        public async Task GetAppEvents_AllConditionsHaveValues_ConditionsEvaluatedCorrectly()
        {
            // Arrange
            int partyId = 50;
            var repositoryMock = new Mock<ICloudEventRepository>();
            repositoryMock.Setup(r => r.GetAppEvents(
                It.IsAny<string>(), // afer
                It.IsAny<DateTime?>(), // from
                It.IsAny<DateTime?>(), // to
                It.Is<string>(subject => subject.Equals($"/party/{partyId}")),
                It.Is<List<string>>(sourceFilter => sourceFilter != null),
                It.IsAny<string>(), // resource
                It.Is<List<string>>(typeFiler => typeFiler != null),
                It.IsAny<int>())) // size
                .ReturnsAsync(new List<CloudEvent>());

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            List<CloudEvent> actual = await eventsService.GetAppEvents(null, null, null, partyId, new List<string>() { "https://ttd.apps.tt02.altinn.no/ttd/apps-test/" }, null, new List<string>() { "instance.completed" }, null, null);

            // Assert
            repositoryMock.VerifyAll();
        }

        /// <summary>
        /// Scenario:
        ///   Get events with various input 
        /// Expected result:
        ///   Conditions to evaluate input to repository method are evaluated.
        /// Success criteria:
        ///  Conditions are evaluated correctly.
        /// </summary>
        [Fact]
        public async Task GetAppEvents_AllConditionsAreNull_ConditionsEvaluatedCorrectly()
        {
            // Arrange
            var repositoryMock = new Mock<ICloudEventRepository>();
            repositoryMock.Setup(r => r.GetAppEvents(
                It.IsAny<string>(), // afer
                It.IsAny<DateTime?>(), // from
                It.IsAny<DateTime?>(), // to
                It.IsAny<string>(), // subject
                It.IsAny<List<string>>(), // sourceFilter
                It.IsAny<string>(), // resource
                It.IsAny<List<string>>(), // typeFilter
                It.IsAny<int>())) // size
                .ReturnsAsync(new List<CloudEvent>());

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            List<CloudEvent> actual = await eventsService.GetAppEvents(null, null, null, 0, new List<string>(), null, new List<string>(), null, null);

            // Assert
            repositoryMock.VerifyAll();
        }

        /// <summary>
        /// Scenario:
        ///   Get events without specifying source
        /// Expected result:
        ///   No events are returned
        /// Success criteria:
        ///  Register service is not called to lookup party
        /// </summary>
        [Fact]
        public async Task GetAppEvents_NoSubjectInfoAvailable_RegisterLookupIsNotCalled()
        {
            // Arrange
            Mock<IRegisterService> registerMock = new();

            EventsService eventsService = GetEventsService(registerMock: registerMock);

            // Act
            List<CloudEvent> actual = await eventsService.GetAppEvents("1", null, null, 0, new List<string>() { "https://ttd.apps.at22.altinn.cloud/ttd/app-test/" }, null, new List<string>() { }, null, null);

            // Assert
            registerMock.Verify(r => r.PartyLookup(It.Is<string>(s => string.IsNullOrEmpty(s)), It.Is<string>(s => string.IsNullOrEmpty(s))), Times.Never);
        }

        #region Generic events

        /// <summary>
        /// Scenario:
        ///   Get events based on after and party Id
        /// Expected result:
        ///   A single event is returned.
        /// Success criteria:
        ///  PartyId is coverted to correct subject and matched in the repository.
        /// </summary>
        [Fact]
        public async Task GetEvents_QueryIncludesAfterAndSubject_RetrievesCorrectNumberOfEvents()
        {
            // Arrange
            int expectedCount = 1;
            string expectedSubject = "/party/54321";

            EventsService eventsService = GetEventsService(repositoryMock: new CloudEventRepositoryMock(2));

            // Act
            List<CloudEvent> actual = await eventsService.GetEvents(null, "e31dbb11-2208-4dda-a549-92a0db8c0008", expectedSubject, null, new List<string>() { }, 50, CancellationToken.None);

            // Assert
            Assert.Equal(expectedCount, actual.Count);
            Assert.Equal(expectedSubject, actual[0].Subject);
        }

        /// <summary>
        /// Scenario:
        ///   Get events based on after.
        /// Expected result:
        ///   Three events are returned.
        /// Success criteria:
        ///  Passes on the after parameter to the repository.
        /// </summary>
        [Fact]
        public async Task GetEvents_QueryIncludesAfter_RetrievesCorrectNumberOfEvents()
        {
            // Arrange
            int expectedCount = 3;
            EventsService eventsService = GetEventsService(repositoryMock: new CloudEventRepositoryMock(2));

            // Act
            List<CloudEvent> actual = await eventsService.GetEvents(null, "e31dbb11-2208-4dda-a549-92a0db8c8808", null, null, new List<string>() { }, 50, CancellationToken.None);

            // Assert
            Assert.Equal(expectedCount, actual.Count);
        }

        /// <summary>
        /// Scenario:
        ///   Get events with various input 
        /// Expected result:
        ///   Conditions to evaluate input to repository method are evaluated.
        /// Success criteria:
        ///  Conditions are evaluated correctly.
        /// </summary>
        [Fact]
        public async Task GetEvents_AllConditionsHaveValues_ConditionsEvaluatedCorrectly()
        {
            // Arrange
            string expectedSubject = "/party/50";
            var repositoryMock = new Mock<ICloudEventRepository>();
            repositoryMock.Setup(r => r.GetEvents(
                It.Is<string>(resource => resource != null),
                It.IsAny<string>(), // after
                It.Is<string>(subject => subject.Equals(subject)),
                It.IsAny<string>(), // alternativesubject
                It.Is<List<string>>(typeFiler => typeFiler != null),
                It.IsAny<int>())) // size
                .ReturnsAsync(new List<CloudEvent>());

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            List<CloudEvent> actual = await eventsService.GetEvents("urn:altinn:resource:app_ttd_apps-test", null, expectedSubject, string.Empty, new List<string>() { "instance.completed" }, 50, CancellationToken.None);

            // Assert
            repositoryMock.VerifyAll();
        }

        /// <summary>
        /// Scenario:
        ///   Get events with various input 
        /// Expected result:
        ///   Conditions to evaluate input to repository method are evaluated.
        /// Success criteria:
        ///  Conditions are evaluated correctly.
        /// </summary>
        [Fact]
        public async Task GetEvents_AllConditionsAreNull_ConditionsEvaluatedCorrectly()
        {
            // Arrange
            var repositoryMock = new Mock<ICloudEventRepository>();
            repositoryMock.Setup(r => r.GetEvents(
                It.IsAny<string>(), // resource
                It.IsAny<string>(), // after
                string.Empty, // subject
                string.Empty, // alternativesubject
                null, // typeFilter
                It.IsAny<int>())) // size
                .ReturnsAsync(new List<CloudEvent>());

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            List<CloudEvent> actual = await eventsService.GetEvents(null, null, string.Empty, string.Empty, new List<string>(), 50, CancellationToken.None);

            // Assert
            repositoryMock.VerifyAll();
        }

        /// <summary>
        /// Scenario:
        ///   Get events without specifying source
        /// Expected result:
        ///   No events are returned
        /// Success criteria:
        ///  Register service is not called to lookup party
        /// </summary>
        [Fact]
        public async Task GetEvents_NoSubjectInfoAvailable_RegisterLookupIsNotCalled()
        {
            // Arrange
            Mock<IRegisterService> registerMock = new();

            EventsService eventsService = GetEventsService(registerMock: registerMock);

            // Act
            List<CloudEvent> actual = await eventsService.GetEvents("1", "https://ttd.apps.at22.altinn.cloud/ttd/app-test/", null, null, new List<string>(), 50, CancellationToken.None);

            // Assert
            registerMock.Verify(r => r.PartyLookup(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        /// <summary>
        /// Scenario:
        ///   Save a CloudEvent from an external source
        /// Expected result:
        ///   Returns the id of the newly created document.
        /// Success criteria:
        ///   The repository is called one to create a cloud event in the repository.
        /// </summary>
        [Fact]
        public async Task Save_CloudEvent_CreateEventMethodCalled()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>()));

            EventsService eventsService = GetEventsService(repositoryMock.Object);

            // Act
            string actual = await eventsService.Save(GetCloudEvent());

            // Assert
            Assert.NotEmpty(actual);
            repositoryMock.Verify(r => r.CreateEvent(It.IsAny<string>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Save cloud event from app to db fails when storing to events table.
        /// Expected result:
        ///   Error returned to caller.
        /// Success criteria:
        ///   Error is logged.
        /// </summary>
        [Fact]
        public async Task Save_CreateEventThrowsException_ErrorIsLogged()
        {
            // Arrange
            Mock<ICloudEventRepository> repoMock = new Mock<ICloudEventRepository>();
            repoMock.Setup(q => q.CreateEvent(It.IsAny<string>()))
                .ThrowsAsync(new Exception("// EventsService // Save // Failed to save eventId"));

            Mock<ILogger<EventsService>> logger = new Mock<ILogger<EventsService>>();
            EventsService eventsService = GetEventsService(loggerMock: logger, repositoryMock: repoMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => eventsService.Save(GetCloudEvent()));

            logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task GetEvents_InputEventsFiveSubjects_LogicManipluateSubjectCorrectly()
        {
            List<CloudEvent> cloudEvents = [
                GetCloudEvent("urn:altinn:resource:super-simple-service", "urn:altinn:person:identifier-no:02056241046"),
                GetCloudEvent("urn:altinn:resource:super-simple-service", "urn:altinn:organization:identifier-no:312508729"),
                GetCloudEvent("urn:altinn:resource:super-simple-service", "urn:altinn:person:identifier-no:31073102351"),
                GetCloudEvent("urn:altinn:resource:super-simple-service", "urn:altinn:person:identifier-no:31073102351"),
                GetCloudEvent("urn:altinn:resource:super-simple-service", "urn:altinn:person:identifier-no:notfound")];

            Mock<ICloudEventRepository> repoMock = new();
            repoMock.Setup(q => q.GetEvents(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<int>())).ReturnsAsync(cloudEvents);

            List<PartyIdentifiers> partyIdentifiers =
                (await TestDataLoader.Load<PartiesRegisterQueryResponse>("twopersons")).Data;

            Mock<IRegisterService> registerMock = new();
            registerMock.Setup(r => r.PartyLookup(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Callback((IEnumerable<string> requestedUrnList, CancellationToken cancellationToken) =>
                {
                    Assert.Equal(3, requestedUrnList.Count());
                    Assert.Equal("urn:altinn:person:identifier-no:02056241046", requestedUrnList.ElementAt(0));
                    Assert.Equal("urn:altinn:person:identifier-no:31073102351", requestedUrnList.ElementAt(1));
                    Assert.Equal("urn:altinn:person:identifier-no:notfound", requestedUrnList.ElementAt(2));
                })
                .ReturnsAsync(partyIdentifiers);

            Mock<IAuthorization> authorizationMock = new();
            authorizationMock.Setup(a => a.AuthorizeEvents(It.IsAny<List<CloudEvent>>()))
                .Callback((List<CloudEvent> cloudEventsForAuth) =>
                {
                    Assert.Equal(5, cloudEventsForAuth.Count);
                    Assert.StartsWith("urn:altinn:party:uuid:", cloudEventsForAuth[0].Subject);
                    Assert.Equal(
                        "urn:altinn:person:identifier-no:02056241046", 
                        cloudEventsForAuth[0]["originalsubjectreplacedforauthorization"].ToString());

                    Assert.Equal("urn:altinn:organization:identifier-no:312508729", cloudEventsForAuth[1].Subject);
                    Assert.Null(cloudEventsForAuth[1]["originalsubjectreplacedforauthorization"]);

                    Assert.StartsWith("urn:altinn:party:uuid:", cloudEventsForAuth[2].Subject);
                    Assert.Equal(
                        "urn:altinn:person:identifier-no:31073102351", 
                        cloudEventsForAuth[2]["originalsubjectreplacedforauthorization"].ToString());

                    Assert.StartsWith("urn:altinn:party:uuid:", cloudEventsForAuth[3].Subject);
                    Assert.Equal(
                        "urn:altinn:person:identifier-no:31073102351",
                        cloudEventsForAuth[3]["originalsubjectreplacedforauthorization"].ToString());

                    Assert.Null(cloudEventsForAuth[4].Subject);
                    Assert.Equal(
                        "urn:altinn:person:identifier-no:notfound",
                        cloudEventsForAuth[4]["originalsubjectreplacedforauthorization"].ToString());
                })
                .ReturnsAsync((List<CloudEvent> events) => events);

            Mock<ILogger<EventsService>> logger = new Mock<ILogger<EventsService>>();
            EventsService target = GetEventsService(
                repositoryMock: repoMock.Object,
                registerMock: registerMock,
                authorizationMock: authorizationMock,
                loggerMock: logger);

            // Act
            List<CloudEvent> finalCloudEvents = await target.GetEvents(string.Empty, string.Empty, string.Empty, string.Empty, [], 50, CancellationToken.None);

            Assert.NotNull(finalCloudEvents);

            Assert.Equal(5, finalCloudEvents.Count);
            Assert.StartsWith("urn:altinn:person:identifier-no:02056241046", finalCloudEvents[0].Subject);
            Assert.Null(finalCloudEvents[0]["originalsubjectreplacedforauthorization"]);

            Assert.StartsWith("urn:altinn:organization:identifier-no:", finalCloudEvents[1].Subject);
            Assert.Null(finalCloudEvents[1]["originalsubjectreplacedforauthorization"]);

            Assert.Equal("urn:altinn:person:identifier-no:31073102351", finalCloudEvents[2].Subject);
            Assert.Null(finalCloudEvents[2]["originalsubjectreplacedforauthorization"]);

            Assert.Equal("urn:altinn:person:identifier-no:31073102351", finalCloudEvents[3].Subject);
            Assert.Null(finalCloudEvents[3]["originalsubjectreplacedforauthorization"]);

            Assert.Equal("urn:altinn:person:identifier-no:notfound", finalCloudEvents[4].Subject);
            Assert.Null(finalCloudEvents[4]["originalsubjectreplacedforauthorization"]);
        }

        private EventsService GetEventsService(
            ICloudEventRepository repositoryMock = null,
            IEventsQueueClient queueMock = null,
            Mock<IRegisterService> registerMock = null,
            Mock<IAuthorization> authorizationMock = null,
            Mock<ILogger<EventsService>> loggerMock = null)
        {
            repositoryMock ??= _repositoryMock;
            registerMock ??= _registerMock;
            queueMock ??= _queueMock;
            loggerMock ??= _loggerMock;

            // default mocked authorization logic. All elements are returned
            if (authorizationMock == null)
            {
                _authorizationMock
                    .Setup(a => a.AuthorizeAltinnAppEvents(It.IsAny<List<CloudEvent>>()))
                    .ReturnsAsync((List<CloudEvent> events) => events);

                _authorizationMock
                  .Setup(a => a.AuthorizeEvents(It.IsAny<List<CloudEvent>>()))
                  .ReturnsAsync((List<CloudEvent> events) => events);

                authorizationMock = _authorizationMock;
            }

            return new EventsService(
                repositoryMock,
                queueMock,
                registerMock.Object,
                authorizationMock.Object,
                loggerMock.Object);
        }

        private static CloudEvent GetCloudEventFromApp()
        {
            CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "instance.created",
                Source = new Uri("https://brg.apps.altinn.no/brg/something/232243423"),
                Time = DateTime.Now,
                Subject = "/party/456456",
                Data = "something/extra",
            };

            return cloudEvent;
        }

        private static CloudEvent GetCloudEvent()
        {
            CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "dom.avsagt",
                Source = new Uri("urn:isbn:00939963"),
                Time = DateTime.Now,
                Subject = "/person/16069412345"
            };

            return cloudEvent;
        }

        private static CloudEvent GetCloudEvent(string resource, string subject)
        {
            CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "something.important.happened",
                Time = DateTime.Now,
                Subject = subject
            };

            cloudEvent["resource"] = resource;

            return cloudEvent;
        }
    }
}
