using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.Wolverine.Commands;
using Altinn.Platform.Events.Wolverine.Publishers;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;

using Moq;
using Wolverine;
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
        private readonly Mock<ITraceLogService> _traceLogServiceMock;
        private readonly Mock<IRegisterService> _registerMock;
        private readonly Mock<IAuthorization> _authorizationMock;
        private readonly Mock<IMessageBus> _messageBusMock;
        private readonly Mock<ILogger<EventsService>> _loggerMock;
        private readonly Mock<IRegistrationEventPublisher> _registrationPublisherMock;

        public EventsServiceTest()
        {
            _repositoryMock = new CloudEventRepositoryMock();
            _queueMock = new EventsQueueClientMock();
            _traceLogServiceMock = new();
            _registerMock = new();
            _authorizationMock = new();
            _messageBusMock = new();
            _loggerMock = new();
            _registrationPublisherMock = new();
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
            string actual = await eventsService.PostInbound(GetCloudEvent());

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
            Mock<IRegistrationEventPublisher> registrationPublisher = new();
            registrationPublisher.Setup(p => p.PublishRegistrationEvent(It.IsAny<CloudEvent>(), It.IsAny<string>())).ThrowsAsync(new Exception("The bus failed due to something"));

            Mock<ILogger<EventsService>> logger = new Mock<ILogger<EventsService>>();
            EventsService eventsService = GetEventsService(loggerMock: logger, registrationPublisherMock: registrationPublisher);

            // Act
            await Assert.ThrowsAsync<Exception>(() => eventsService.RegisterNew(GetCloudEvent(), It.IsAny<string>()));

            // Assert
            logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Store an event that succeeds, results in a call to register log entry.
        /// Expected result:
        ///   Event is stored and eventId returned. Log entry is created.
        /// Success criteria:
        ///   No Error logged. traceLogService called once
        /// </summary>
        [Fact]
        public async Task RegisterNewEvent_PushEventSucceeds_LogIsCreated()
        {
            // Arrange
            Mock<IRegistrationEventPublisher> registrationPublisher = new();
            registrationPublisher.Setup(p => p.PublishRegistrationEvent(It.IsAny<CloudEvent>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            Mock<ILogger<EventsService>> logger = new();
            Mock<ITraceLogService> traceLogServiceMock = new();
            traceLogServiceMock.Setup(t => t.CreateRegisteredEntry(It.IsAny<CloudEvent>()));
            EventsService eventsService = GetEventsService(traceLogServiceMock: traceLogServiceMock, loggerMock: logger, registrationPublisherMock: registrationPublisher);

            // Act
            await eventsService.RegisterNew(GetCloudEvent(), It.IsAny<string>());

            // Assert
            logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Never);
            traceLogServiceMock.Verify(t => t.CreateRegisteredEntry(It.IsAny<CloudEvent>()), Times.Once);
            registrationPublisher.Verify(p => p.PublishRegistrationEvent(It.IsAny<CloudEvent>(), It.IsAny<string>()), Times.Once);
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
            await Assert.ThrowsAsync<Exception>(() => eventsService.PostInbound(GetCloudEvent()));
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
            List<CloudEvent> actual = await eventsService.GetAppEvents(string.Empty, new DateTime(2020, 06, 17, 0, 0, 0, DateTimeKind.Utc), null, 54321, [], null, [], null, null);

            // Assert
            Assert.Equal(expectedCount, actual.Count);
            Assert.Equal(expectedSubject, actual[0].Subject);
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
            List<CloudEvent> actual = await eventsService.GetAppEvents("e31dbb11-2208-4dda-a549-92a0db8c8808", null, null, 0, [], null, [], null, null);

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
                .ReturnsAsync([]);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            await eventsService.GetAppEvents(null, null, null, partyId, ["https://ttd.apps.tt02.altinn.no/ttd/apps-test/"], null, ["instance.completed"], null, null);

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
                .ReturnsAsync([]);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            await eventsService.GetAppEvents(null, null, null, 0, [], null, [], null, null);

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
            await eventsService.GetAppEvents("1", null, null, 0, ["https://ttd.apps.at22.altinn.cloud/ttd/app-test/"], null, [], null, null);

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
            List<CloudEvent> actual = await eventsService.GetEvents(null, "e31dbb11-2208-4dda-a549-92a0db8c0008", expectedSubject, null, [], 50, CancellationToken.None);

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
            List<CloudEvent> actual = await eventsService.GetEvents(null, "e31dbb11-2208-4dda-a549-92a0db8c8808", null, null, [], 50, CancellationToken.None);

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
                .ReturnsAsync([]);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            await eventsService.GetEvents("urn:altinn:resource:app_ttd_apps-test", null, expectedSubject, string.Empty, ["instance.completed"], 50, CancellationToken.None);

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
                .ReturnsAsync([]);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            await eventsService.GetEvents(null, null, string.Empty, string.Empty, [], 50, CancellationToken.None);

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
            await eventsService.GetEvents("1", "https://ttd.apps.at22.altinn.cloud/ttd/app-test/", null, null, [], 50, CancellationToken.None);

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
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            bool actual = await eventsService.Save(GetCloudEvent(), It.IsAny<string>());

            // Assert
            Assert.True(actual);
            repositoryMock.Verify(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
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
            repoMock.Setup(q => q.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("// EventsService // Save // Failed to save eventId"));

            Mock<ILogger<EventsService>> logger = new Mock<ILogger<EventsService>>();
            EventsService eventsService = GetEventsService(loggerMock: logger, repositoryMock: repoMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => eventsService.Save(GetCloudEvent(), It.IsAny<string>()));

            logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   SaveAndPublish is called with a cloud event
        /// Expected result:
        ///   Event is saved to repository and published to the message bus
        /// Success criteria:
        ///   Repository CreateEvent and MessageBus PublishAsync are both called once
        /// </summary>
        [Fact]
        public async Task SaveAndPublish_ValidCloudEvent_SavesAndPublishes()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(true));

            Mock<IMessageBus> messageBusMock = new();
            messageBusMock.Setup(m => m.SendAsync(It.IsAny<InboundEventCommand>())).Returns(ValueTask.CompletedTask);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object, messageBusMock: messageBusMock);

            // Act
            await eventsService.SaveAndPublish(GetCloudEvent(), null, CancellationToken.None);

            // Assert
            repositoryMock.Verify(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            messageBusMock.Verify(m => m.SendAsync(It.IsAny<InboundEventCommand>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   SaveAndPublish is called with an AltinnApp cloud event with dot notation in resource
        /// Expected result:
        ///   Resource format is corrected to use underscore, event is saved and published
        /// Success criteria:
        ///   Resource attribute is changed from altinnapp. to app_{org}_{app} format
        /// </summary>
        [Fact]
        public async Task SaveAndPublish_AltinnAppEventWithDotNotation_ResourceFormatCorrected()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            string capturedEvent = null;
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((e, s) => capturedEvent = e)
                .Returns(Task.FromResult(true));

            Mock<IMessageBus> messageBusMock = new();
            messageBusMock.Setup(m => m.SendAsync(It.IsAny<InboundEventCommand>())).Returns(ValueTask.CompletedTask);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object, messageBusMock: messageBusMock);

            CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "instance.created",
                Source = new Uri("https://ttd.apps.altinn.no/ttd/apps-test/instances/1234/5678"),
                Time = DateTime.Now,
                Subject = "/party/456456"
            };
            cloudEvent.SetAttributeFromString("resource", "urn:altinn:resource:altinnapp.ttd.apps-test");

            // Act
            await eventsService.SaveAndPublish(cloudEvent, null, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedEvent);
            Assert.Contains("urn:altinn:resource:app_ttd_apps-test", capturedEvent);
            repositoryMock.Verify(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            messageBusMock.Verify(m => m.SendAsync(It.IsAny<InboundEventCommand>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   SaveAndPublish is called with a non-AltinnApp cloud event
        /// Expected result:
        ///   Resource format is unchanged, event is saved and published
        /// Success criteria:
        ///   Resource attribute remains unchanged
        /// </summary>
        [Fact]
        public async Task SaveAndPublish_NonAltinnAppEvent_ResourceFormatUnchanged()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            string capturedEvent = null;
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((e, s) => capturedEvent = e)
                .Returns(Task.FromResult(true));

            Mock<IMessageBus> messageBusMock = new();
            messageBusMock.Setup(m => m.SendAsync(It.IsAny<InboundEventCommand>())).Returns(ValueTask.CompletedTask);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object, messageBusMock: messageBusMock);

            CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "dom.avsagt",
                Source = new Uri("urn:isbn:00939963"),
                Time = DateTime.Now,
                Subject = "/person/16069412345"
            };
            cloudEvent.SetAttributeFromString("resource", "urn:altinn:resource:some-other-resource");

            // Act
            await eventsService.SaveAndPublish(cloudEvent, null, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedEvent);
            Assert.Contains("urn:altinn:resource:some-other-resource", capturedEvent);
            repositoryMock.Verify(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            messageBusMock.Verify(m => m.SendAsync(It.IsAny<InboundEventCommand>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   SaveAndPublish fails during save operation
        /// Expected result:
        ///   Exception is propagated and message is not published
        /// Success criteria:
        ///   InvalidOperationException is thrown and PublishAsync is never called
        /// </summary>
        [Fact]
        public async Task SaveAndPublish_SaveFails_ExceptionThrownAndNotPublished()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database error"));

            Mock<IMessageBus> messageBusMock = new();
            Mock<ILogger<EventsService>> logger = new();

            EventsService eventsService = GetEventsService(
                repositoryMock: repositoryMock.Object, 
                messageBusMock: messageBusMock,
                loggerMock: logger);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => eventsService.SaveAndPublish(GetCloudEvent(), null, CancellationToken.None));

            repositoryMock.Verify(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            messageBusMock.Verify(m => m.SendAsync(It.IsAny<InboundEventCommand>()), Times.Never);
            logger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAppEvents_PersonProvided_PartyLookupCalled()
        {
            // Arrange
            int expectedPartyId = 12345;
            string person = "01039012345";

            Mock<IRegisterService> registerMock = new();
            registerMock.Setup(r => r.PartyLookup(null, person)).ReturnsAsync(expectedPartyId);

            var repositoryMock = new Mock<ICloudEventRepository>();
            repositoryMock.Setup(r => r.GetAppEvents(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.Is<string>(s => s == $"/party/{expectedPartyId}"),
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<int>()))
                .ReturnsAsync([]);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object, registerMock: registerMock);

            // Act
            await eventsService.GetAppEvents(null, null, null, 0, [], null, [], null, person);

            // Assert
            registerMock.Verify(r => r.PartyLookup(null, person), Times.Once);
        }

        [Fact]
        public async Task GetAppEvents_UnitProvided_PartyLookupCalled()
        {
            // Arrange
            int expectedPartyId = 54321;
            string unit = "897069650";

            Mock<IRegisterService> registerMock = new();
            registerMock.Setup(r => r.PartyLookup(unit, null)).ReturnsAsync(expectedPartyId);

            var repositoryMock = new Mock<ICloudEventRepository>();
            repositoryMock.Setup(r => r.GetAppEvents(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.Is<string>(s => s == $"/party/{expectedPartyId}"),
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<int>()))
                .ReturnsAsync([]);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object, registerMock: registerMock);

            // Act
            await eventsService.GetAppEvents(null, null, null, 0, [], null, [], unit, null);

            // Assert
            registerMock.Verify(r => r.PartyLookup(unit, null), Times.Once);
        }

        [Fact]
        public async Task GetAppEvents_ReturnsEmptyList_ReturnsWithoutAuthorizationCall()
        {
            // Arrange
            var repositoryMock = new Mock<ICloudEventRepository>();
            repositoryMock.Setup(r => r.GetAppEvents(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<int>()))
                .ReturnsAsync([]);

            Mock<IAuthorization> authMock = new();

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object, authorizationMock: authMock);

            // Act
            List<CloudEvent> result = await eventsService.GetAppEvents(null, null, null, 0, [], null, [], null, null);

            // Assert
            Assert.Empty(result);
            authMock.Verify(a => a.AuthorizeAltinnAppEvents(It.IsAny<List<CloudEvent>>()), Times.Never);
        }

        [Fact]
        public async Task GetEvents_ReturnsEmptyList_ReturnsWithoutAuthorizationCall()
        {
            // Arrange
            var repositoryMock = new Mock<ICloudEventRepository>();
            repositoryMock.Setup(r => r.GetEvents(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<int>()))
                .ReturnsAsync([]);

            Mock<IAuthorization> authMock = new();

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object, authorizationMock: authMock);

            // Act
            List<CloudEvent> result = await eventsService.GetEvents("urn:altinn:resource:test", null, null, null, [], 50, CancellationToken.None);

            // Assert
            Assert.Empty(result);
            authMock.Verify(a => a.AuthorizeEvents(It.IsAny<List<CloudEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetEvents_TypesProvided_PassedToRepository()
        {
            // Arrange
            var types = new List<string> { "instance.created", "instance.completed" };
            var repositoryMock = new Mock<ICloudEventRepository>();
            repositoryMock.Setup(r => r.GetEvents(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<List<string>>(t => t != null && t.Count == 2),
                It.IsAny<int>()))
                .ReturnsAsync([]);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            await eventsService.GetEvents("urn:altinn:resource:test", null, null, null, types, 50, CancellationToken.None);

            // Assert
            repositoryMock.VerifyAll();
        }

        [Fact]
        public async Task SaveAndPublish_AltinnAppEventWithShortPath_OrgAndAppAreNull()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            string capturedEvent = null;
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((e, s) => capturedEvent = e)
                .Returns(Task.FromResult(true));

            Mock<IMessageBus> messageBusMock = new();
            messageBusMock.Setup(m => m.SendAsync(It.IsAny<InboundEventCommand>())).Returns(ValueTask.CompletedTask);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object, messageBusMock: messageBusMock);

            CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "instance.created",
                Source = new Uri("https://ttd.apps.altinn.no/short"),
                Time = DateTime.Now,
                Subject = "/party/456456"
            };
            cloudEvent.SetAttributeFromString("resource", "urn:altinn:resource:altinnapp.ttd.apps-test");

            // Act
            await eventsService.SaveAndPublish(cloudEvent, null, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedEvent);

            // With short path (<=5 segments), org and app should be null in the resource
            Assert.Contains("urn:altinn:resource:app__", capturedEvent);
        }

        [Fact]
        public async Task GetAppEvents_NullSourceList_PassesNullToRepository()
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
                .ReturnsAsync([]);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            await eventsService.GetAppEvents(null, null, null, 0, null, null, [], null, null);

            // Assert
            repositoryMock.VerifyAll();
        }

        [Fact]
        public async Task SaveAndPublish_NoResourceAttribute_SkipsResourceFormatting()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            string capturedEvent = null;
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((e, s) => capturedEvent = e)
                .Returns(Task.FromResult(true));

            Mock<IMessageBus> messageBusMock = new();
            messageBusMock.Setup(m => m.SendAsync(It.IsAny<InboundEventCommand>())).Returns(ValueTask.CompletedTask);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object, messageBusMock: messageBusMock);

            // CloudEvent without resource attribute - covers `resource is not null` false branch
            CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
            {
                Id = Guid.NewGuid().ToString(),
                Type = "instance.created",
                Source = new Uri("https://ttd.apps.altinn.no/ttd/test"),
                Time = DateTime.Now,
                Subject = "/party/456456"
            };

            // Act
            await eventsService.SaveAndPublish(cloudEvent, null, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedEvent);
            Assert.DoesNotContain("urn:altinn:resource:app_", capturedEvent);
        }

        /// <summary>
        /// Scenario:
        ///   SaveAndPublish is called and the repository reports the event was newly persisted (not a duplicate).
        /// Expected result:
        ///   The event is serialized and sent to the message bus as an InboundEventCommand.
        /// Success criteria:
        ///   IMessageBus.SendAsync is called once with an InboundEventCommand and no duplicate trace log is created.
        /// </summary>
        [Fact]
        public async Task SaveAndPublish_EventPersisted_PublishesInboundEventCommand()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            Mock<IMessageBus> messageBusMock = new();
            messageBusMock.Setup(m => m.SendAsync(It.IsAny<InboundEventCommand>())).Returns(ValueTask.CompletedTask);

            Mock<ITraceLogService> traceLogServiceMock = new();

            EventsService eventsService = GetEventsService(
                repositoryMock: repositoryMock.Object,
                messageBusMock: messageBusMock,
                traceLogServiceMock: traceLogServiceMock);

            CloudEvent cloudEvent = GetCloudEvent();

            // Act
            await eventsService.SaveAndPublish(cloudEvent, "d1525c79-cda8-4fef-b95c-feb3e7be89ec", CancellationToken.None);

            // Assert
            messageBusMock.Verify(m => m.SendAsync(It.IsAny<InboundEventCommand>()), Times.Once);
            traceLogServiceMock.Verify(
                t => t.CreateLogEntryDuplicateIdempotencyIdSkipped(It.IsAny<CloudEvent>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Scenario:
        ///   SaveAndPublish is called and the repository reports the event as a duplicate (idempotency conflict).
        /// Expected result:
        ///   The event is NOT sent to the message bus.
        /// Success criteria:
        ///   IMessageBus.SendAsync is never called and a duplicate-idempotency trace log entry is created once.
        /// </summary>
        [Fact]
        public async Task SaveAndPublish_DuplicateIdempotencyId_SkipsQueuePublish_LogsDuplicate()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            Mock<IMessageBus> messageBusMock = new();

            Mock<ITraceLogService> traceLogServiceMock = new();
            traceLogServiceMock
                .Setup(t => t.CreateLogEntryDuplicateIdempotencyIdSkipped(It.IsAny<CloudEvent>(), It.IsAny<string>()))
                .ReturnsAsync(string.Empty);

            EventsService eventsService = GetEventsService(
                repositoryMock: repositoryMock.Object,
                messageBusMock: messageBusMock,
                traceLogServiceMock: traceLogServiceMock);

            CloudEvent cloudEvent = GetCloudEvent();
            const string idempotencyId = "d1525c79-cda8-4fef-b95c-feb3e7be89ec";

            // Act
            await eventsService.SaveAndPublish(cloudEvent, idempotencyId, CancellationToken.None);

            // Assert
            messageBusMock.Verify(m => m.SendAsync(It.IsAny<InboundEventCommand>()), Times.Never);
            traceLogServiceMock.Verify(
                t => t.CreateLogEntryDuplicateIdempotencyIdSkipped(cloudEvent, idempotencyId), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Save is called with an idempotency id.
        /// Expected result:
        ///   The idempotency id is forwarded unchanged to the repository.
        /// Success criteria:
        ///   ICloudEventRepository.CreateEvent is called once with the same idempotency id.
        /// </summary>
        [Fact]
        public async Task Save_WithIdempotencyId_ForwardsIdToRepository()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);
            CloudEvent cloudEvent = GetCloudEvent();
            const string idempotencyId = "d1525c79-cda8-4fef-b95c-feb3e7be89ec";

            // Act
            bool result = await eventsService.Save(cloudEvent, idempotencyId);

            // Assert
            Assert.True(result);
            repositoryMock.Verify(r => r.CreateEvent(It.IsAny<string>(), idempotencyId), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Save throws because the repository failed unexpectedly (not an idempotency conflict).
        /// Expected result:
        ///   An InvalidOperationException wrapping the original exception is thrown and logged.
        /// Success criteria:
        ///   Assert.ThrowsAsync&lt;InvalidOperationException&gt; succeeds and error is logged.
        /// </summary>
        [Fact]
        public async Task Save_RepositoryThrows_WrapsAndLogsException()
        {
            // Arrange
            Mock<ICloudEventRepository> repositoryMock = new();
            repositoryMock.Setup(r => r.CreateEvent(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("db failure"));

            Mock<ILogger<EventsService>> logger = new();
            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object, loggerMock: logger);
            CloudEvent cloudEvent = GetCloudEvent();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => eventsService.Save(cloudEvent, null));
            logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
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

        private EventsService GetEventsService(
            ICloudEventRepository repositoryMock = null,
            IEventsQueueClient queueMock = null,
            Mock<ITraceLogService> traceLogServiceMock = null,
            Mock<IRegisterService> registerMock = null,
            Mock<IAuthorization> authorizationMock = null,
            Mock<IMessageBus> messageBusMock = null,
            Mock<ILogger<EventsService>> loggerMock = null,
            Mock<IRegistrationEventPublisher> registrationPublisherMock = null)
        {
            if (authorizationMock == null)
            {
                _authorizationMock
                    .Setup(a => a.AuthorizeAltinnAppEvents(It.IsAny<List<CloudEvent>>()))
                    .ReturnsAsync((List<CloudEvent> events) => events);

                _authorizationMock
                  .Setup(a => a.AuthorizeEvents(It.IsAny<List<CloudEvent>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((List<CloudEvent> events, CancellationToken cancellationToken) => events);

                authorizationMock = _authorizationMock;
            }

            return new EventsService(
                repositoryMock ?? _repositoryMock,
                (traceLogServiceMock ?? _traceLogServiceMock).Object,
                queueMock ?? _queueMock,
                (registerMock ?? _registerMock).Object,
                (authorizationMock ?? _authorizationMock).Object,
                (messageBusMock ?? _messageBusMock).Object,
                (loggerMock ?? _loggerMock).Object,
                (registrationPublisherMock ?? _registrationPublisherMock).Object);
        }
    }
}
