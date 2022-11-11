using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        private readonly Mock<IClaimsPrincipalProvider> _claimsPrincipalProviderMock;
        private readonly Mock<ILogger<IEventsService>> _loggerMock;

        public EventsServiceTest()
        {
            _repositoryMock = new CloudEventRepositoryMock();
            _queueMock = new EventsQueueClientMock();
            _registerMock = new();
            _authorizationMock = new();
            _claimsPrincipalProviderMock = new();
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
        ///   Store a CloudEvent in postgres DB.
        /// Expected result:
        ///   Returns the id of the newly created document.
        /// Success criteria:
        ///   The response is a non-empty string.
        /// </summary>
        [Fact]
        public async Task SaveNewEvent_EventSuccessfullyStored_IdReturned()
        {
            // Arrange
            EventsService eventsService = GetEventsService();

            // Act
            string actual = await eventsService.Save(GetCloudEventFromApp());

            // Assert
            Assert.NotEmpty(actual);
        }

        /// <summary>
        /// Scenario:
        ///   Store a cloud event in postgres DB when id is null.
        /// Expected result:
        ///   Returns the id of the newly created document.
        /// Success criteria:
        ///   The response is a non-empty string.
        /// </summary>
        [Fact]
        public async Task RegisterNewEvent_CheckIdCreatedByService_IdReturned()
        {
            // Arrange
            EventsService eventsService = GetEventsService();

            CloudEvent item = GetCloudEventFromApp();
            item.Id = null;

            // Act
            string actual = await eventsService.RegisterNew(item);

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

            Mock<ILogger<IEventsService>> logger = new Mock<ILogger<IEventsService>>();
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

            Mock<ILogger<IEventsService>> logger = new Mock<ILogger<IEventsService>>();
            EventsService eventsService = GetEventsService(loggerMock: logger, queueMock: queueMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => eventsService.PostInbound(GetCloudEventFromApp()));
            logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Save cloud event to db fails.
        /// Expected result:
        ///   Error returned to caller.
        /// Success criteria:
        ///   Error is logged.
        /// </summary>
        [Fact]
        public async Task SaveNewEvent_SaveToDatabaseFails_ErrorIsLogged()
        {
            // Arrange
            Mock<ICloudEventRepository> repoMock = new Mock<ICloudEventRepository>();
            repoMock.Setup(q => q.CreateAppEvent(It.IsAny<CloudEvent>()))
                .ThrowsAsync(new Exception("// EventsService // Save // Failed to save eventId"));

            Mock<ILogger<IEventsService>> logger = new Mock<ILogger<IEventsService>>();
            EventsService eventsService = GetEventsService(loggerMock: logger, repositoryMock: repoMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => eventsService.Save(GetCloudEventFromApp()));

            logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Get instances based on from and party Id
        /// Expected result:
        ///   A single instance is returned.
        /// Success criteria:
        ///  PartyId is coverted to correct subject and matched in the repository.
        /// </summary>
        [Fact]
        public async Task GetAppEvents_QueryIncludesFromAndPartyId_RetrievesCorrectNumberOfEvents()
        {
            // Arrange
            int expectedCount = 1;
            string expectedSubject = "/party/54321";

            EventsService eventsService = GetEventsService(repositoryMock: new CloudEventRepositoryMock(2));

            // Act
            List<CloudEvent> actual = await eventsService.GetAppEvents(string.Empty, new DateTime(2020, 06, 17), null, 54321, new List<string>() { }, new List<string>() { }, null, null);

            // Assert
            Assert.Equal(expectedCount, actual.Count);
            Assert.Equal(expectedSubject, actual.First().Subject);
        }

        /// <summary>
        /// Scenario:
        ///   Get instances based on after.
        /// Expected result:
        ///   A single instance is returned.
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
            List<CloudEvent> actual = await eventsService.GetAppEvents("e31dbb11-2208-4dda-a549-92a0db8c8808", null, null, 0, new List<string>() { }, new List<string>() { }, null, null);

            // Assert
            Assert.Equal(expectedCount, actual.Count);
        }

        /// <summary>
        /// Scenario:
        ///   Get instances with various input 
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
            repositoryMock.Setup(r => r.GetAppEvent(
                It.IsAny<string>(), // afer
                It.IsAny<DateTime?>(), // from
                It.IsAny<DateTime?>(), // to
                It.Is<string>(subject => subject.Equals($"/party/{partyId}")),
                It.Is<List<string>>(sourceFilter => sourceFilter != null),
                It.Is<List<string>>(typeFiler => typeFiler != null),
                It.IsAny<int>())) // size
                .ReturnsAsync(new List<CloudEvent>());

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            List<CloudEvent> actual = await eventsService.GetAppEvents(null, null, null, partyId, new List<string>() { "https://ttd.apps.tt02.altinn.no/ttd/apps-test/" }, new List<string>() { "instance.completed" }, null, null);

            // Assert
            repositoryMock.VerifyAll();
        }

        /// <summary>
        /// Scenario:
        ///   Get instances with various input 
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
            repositoryMock.Setup(r => r.GetAppEvent(
                It.IsAny<string>(), // afer
                It.IsAny<DateTime?>(), // from
                It.IsAny<DateTime?>(), // to
                string.Empty, // subject
                null, // sourceFilter
                null, // typeFilter
                It.IsAny<int>())) // size
                .ReturnsAsync(new List<CloudEvent>());

            EventsService eventsService = GetEventsService(repositoryMock: repositoryMock.Object);

            // Act
            List<CloudEvent> actual = await eventsService.GetAppEvents(null, null, null, 0, new List<string>(), new List<string>(), null, null);

            // Assert
            repositoryMock.VerifyAll();
        }

        /// <summary>
        /// Scenario:
        ///   Get instances without specifying source
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
            List<CloudEvent> actual = await eventsService.GetAppEvents("1", null, null, 0, new List<string>() { "https://ttd.apps.at22.altinn.cloud/ttd/app-test/" }, new List<string>() { }, null, null);

            // Assert
            registerMock.Verify(r => r.PartyLookup(It.Is<string>(s => string.IsNullOrEmpty(s)), It.Is<string>(s => string.IsNullOrEmpty(s))), Times.Never);
        }

        private EventsService GetEventsService(
            ICloudEventRepository repositoryMock = null,
            IEventsQueueClient queueMock = null,
            Mock<IRegisterService> registerMock = null,
            Mock<IAuthorization> authorizationMock = null,
            Mock<ILogger<IEventsService>> loggerMock = null)
        {
            repositoryMock ??= _repositoryMock;
            registerMock ??= _registerMock;
            queueMock ??= _queueMock;
            loggerMock ??= _loggerMock;

            // default mocked authorization logic. All elements are returned
            if (authorizationMock == null)
            {
                _authorizationMock
                    .Setup(a => a.AuthorizeEvents(It.IsAny<ClaimsPrincipal>(), It.IsAny<List<CloudEvent>>()))
                    .ReturnsAsync((ClaimsPrincipal user, List<CloudEvent> events) => events);

                authorizationMock = _authorizationMock;
            }

            IOptions<PlatformSettings> settingsIOption = Options.Create(
                new PlatformSettings()
                {
                    AppsDomain = "apps.altinn.no"
                });

            return new EventsService(
                repositoryMock,
                queueMock,
                registerMock.Object,
                authorizationMock.Object,
                _claimsPrincipalProviderMock.Object,
                settingsIOption,
                loggerMock.Object);
        }

        private static CloudEvent GetCloudEventFromApp()
        {
            CloudEvent cloudEvent = new()
            {
                Id = Guid.NewGuid().ToString(),
                SpecVersion = "1.0",
                Type = "instance.created",
                Source = new Uri("https://brg.apps.altinn.no/brg/something/232243423"),
                Time = DateTime.Now,
                Subject = "/party/456456",
                Data = "something/extra",
            };

            return cloudEvent;
        }
    }
}
