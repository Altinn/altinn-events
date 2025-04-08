using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;

using Altinn.Platform.Events.Controllers;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.Tests.Mocks.Authentication;
using Altinn.Platform.Events.Tests.Utils;
using Altinn.Platform.Events.UnitTest.Mocks;

using AltinnCore.Authentication.JwtCookie;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingControllers
{
    /// <summary>
    /// Represents a collection of integration tests.
    /// </summary>
    public partial class IntegrationTests
    {
        public class EventsControllerTests : IClassFixture<WebApplicationFactory<EventsController>>
        {
            private const string BasePath = "/events/api/v1";

            private readonly WebApplicationFactory<EventsController> _factory;
            private readonly JsonSerializerOptions _options;
            private readonly CloudEvent _validEvent;

            private readonly Mock<ITraceLogService> _traceLogService = new();

            /// <summary>
            /// Initializes a new instance of the <see cref="EventsControllerTests"/> class with the given <see cref="WebApplicationFactory{TEventsControllerTests}"/>.
            /// </summary>
            /// <param name="factory">The <see cref="WebApplicationFactory{TEventsController}"/> to use when setting up the test server.</param>
            public EventsControllerTests(WebApplicationFactory<EventsController> factory)
            {
                _factory = factory;

                _validEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "system.event.occurred",
                    Subject = "/person/16069412345",
                    Source = new Uri("urn:isbn:1234567890")
                };

                _validEvent["resource"] = "urn:nbib:bokoversikt.api";

                _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            }

            [Fact]
            public async Task Post_MissingBearerToken_UnauthorizedResponse()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                HttpClient client = GetTestClient(null);
                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(_validEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            [Fact]
            public async Task Post_ValidTokenInvalidScope_ForbiddenResponse()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                HttpClient client = GetTestClient(null);
                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(_validEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json")
                };

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events:invalid"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            [Fact]
            public async Task Post_EventMissingParameters_BadRequestResponse()
            {
                // Arrange
                string invalidEvent = "{ \"time\": \"2022-11-15T10:46:53.5339928Z\", \"type\": \"app.instance.created\", \"source\": \"https://ttd.apps.at21.altinn.cloud/ttd/apps-test/instances/50019855/428a4575-2c04-4400-89a3-1aaadd2579cd\", \"subject\": \"/party/50019855\", \"specversion\": \"1.0\", \"alternativesubject\": \"/person/stephanie\" }";
                string requestUri = $"{BasePath}/events";

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(invalidEvent, Encoding.UTF8, "application/cloudevents+json")
                };

                HttpClient client = GetTestClient(null, null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.publish"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseMessage = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("CloudEvent is missing required attributes: id (Parameter 'data')", responseMessage);
            }

            [Fact]
            public async Task Post_ResourceIsNotUrn_BadRequestResponse()
            {
                // Arrange
                string invalidEvent = "{ \"id\":\"random-id\", \"time\": \"2022-11-15T10:46:53.5339928Z\", \"type\": \"app.instance.created\", \"resource\":\"this-is.not-urn\", \"source\": \"https://ttd.apps.at21.altinn.cloud/ttd/apps-test/instances/50019855/428a4575-2c04-4400-89a3-1aaadd2579cd\", \"subject\": \"/party/50019855\", \"specversion\": \"1.0\", \"alternativesubject\": \"/person/stephanie\" }";
                string requestUri = $"{BasePath}/events";

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(invalidEvent, Encoding.UTF8, "application/cloudevents+json")
                };

                HttpClient client = GetTestClient(null, null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.publish"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseMessage = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("'Resource' must be a valid urn.", responseMessage);
            }

            [Fact]
            public async Task Post_IncorrectContentType_UnsupportedMediaType()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                HttpClient client = GetTestClient(null, null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.publish"));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(_validEvent.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            }

            [Fact]
            public async Task Post_PublishScopeIncluded_EventIsRegistered()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                Mock<IEventsService> eventMock = new();
                eventMock.Setup(em => em.RegisterNew(It.IsAny<CloudEvent>()))
                          .ReturnsAsync(Guid.NewGuid().ToString());

                Mock<IAuthorization> authorizationMock = new();
                authorizationMock.Setup(a => a.AuthorizePublishEvent(It.IsAny<CloudEvent>()))
                            .ReturnsAsync(true);

                HttpClient client = GetTestClient(eventMock.Object, authorizationMock.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.publish"));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(_validEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            [Fact]
            public async Task Post_PlatformAccessTokenIncluded_EventIsRegistered()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                Mock<IEventsService> eventMock = new();
                eventMock.Setup(em => em.RegisterNew(It.IsAny<CloudEvent>()))
                          .ReturnsAsync(Guid.NewGuid().ToString());

                Mock<IAuthorization> authorizationMock = new();
                authorizationMock.Setup(a => a.AuthorizePublishEvent(It.IsAny<CloudEvent>()))
                            .ReturnsAsync(true);

                HttpClient client = GetTestClient(eventMock.Object, authorizationMock.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(_validEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json")
                };
                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            [Fact]
            public async Task Post_OrgAuthenticatedNotAuthorized_ReturnsForbidden()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                Mock<IEventsService> eventMock = new();
                eventMock.Setup(em => em.RegisterNew(It.IsAny<CloudEvent>()))
                          .ReturnsAsync(Guid.NewGuid().ToString());

                Mock<IAuthorization> authorizationMock = new();
                authorizationMock.Setup(a => a.AuthorizePublishEvent(It.IsAny<CloudEvent>()))
                            .ReturnsAsync(false);

                HttpClient client = GetTestClient(eventMock.Object, authorizationMock.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(_validEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json")
                };
                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Retrieve a list of events, without bearer token.
            /// Expected result:
            ///   Returns HttpStatus Unauthorized.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetEvents_MissingBearerToken_ReturnsUnauthorized()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?after=0&subject=%2Fparty%2F1337";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            [Fact]
            public async Task Get_ValidTokenInvalidScope_ForbiddenResponse()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?after=e31dbb11-2208-4dda-a549-92a0db8c7708&size=5&subject=/party/1337&resource=urn:altinn:resource:test";

                HttpClient client = GetTestClient(null);

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, requestUri);

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events:invalid"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Get events with size > 1000
            /// Expected result:
            ///   Returns a list of maximum 1000 events and a next header
            /// Success criteria:
            ///   Event service gets size input 1000.
            /// </summary>
            [Fact]
            public async Task GetEvents_SizeMoreThan1000_ReturnsListOfMax1000Events()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?resource=urn:altinn:resource:systemx&after=0&subject=%2Fparty%2F1337&size=1500";
                Mock<IEventsService> eventsMock = new();
                eventsMock
                    .Setup(e => e.GetEvents(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.Is<int>(i => i == 1000), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<CloudEvent>());

                HttpClient client = GetTestClient(eventsMock.Object, null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                eventsMock.VerifyAll();
            }

            /// <summary>
            /// Scenario:
            ///   Get events with negative size.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetEvents_SizeIsLessThanZero_UsesSize50()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?resource=urn:altinn:resource:test&size=-5&after=e31dbb11-2208-4dda-a549-92a0db8c8808";
                Mock<IEventsService> eventsMock = new();
                eventsMock
                    .Setup(e => e.GetEvents(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.Is<int>(i => i == 50), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<CloudEvent>());

                HttpClient client = GetTestClient(eventsMock.Object, null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                eventsMock.VerifyAll();
            }

            /// <summary>
            /// Scenario:
            ///   Get events without defined after in query.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetEvents_MissingRequiredQueryParam_After_ReturnsBadRequest()
            {
                // Arrange   
                string expected = "The 'after' parameter must be defined.";

                string requestUri = $"{BasePath}/events?resource=urn:altinn:resource:systemx&size=5&subject=/party/1337";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object, null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string content = await response.Content.ReadAsStringAsync();
                ProblemDetails actual = JsonSerializer.Deserialize<ProblemDetails>(content, _options);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal(expected, actual.Detail);
            }

            /// <summary>
            /// Scenario:
            ///   Get events without defined resource in query.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetEvents_MissingRequiredQueryParam_Resource_ReturnsBadRequest()
            {
                // Arrange   
                string expected = "The resource field is required.";
                string requestUri = $"{BasePath}/events?after=e31dbb11-2208-4dda-a549-92a0db8c7708size=5&subject=/party/1337";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object, null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string content = await response.Content.ReadAsStringAsync();
                ProblemDetails actual = JsonSerializer.Deserialize<ProblemDetails>(content, _options);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains(expected, content);
                Assert.NotNull(actual.Extensions["errors"]);
            }

            /// <summary>
            /// Scenario:
            ///   Get events with invalid query parameter combination.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetEvents_InvalidCombinationOfQueryParams_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?resource=urn:altinn:resource:systemx&after=0&subject=%2Fparty%2F1337";
                HttpClient client = GetTestClient(new EventsServiceMock(3), null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
                httpRequestMessage.Headers.Add("Altinn-AlternativeSubject", "person:16069412345");

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string content = await response.Content.ReadAsStringAsync();
                ProblemDetails actual = JsonSerializer.Deserialize<ProblemDetails>(content, _options);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Only one of 'subject' or 'alternativeSubject' can be defined.", content);
            }

            /// <summary>
            /// Scenario:
            ///   Get events with a resource not matching required format
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetEvents_InvalidResourceFormat_ReturnsBadRequest()
            {
                // Arrange   
                string expected = "The 'resource' parameter must begin with `urn:altinn:resource:`";

                string requestUri = $"{BasePath}/events?resource=random&after=e31dbb11-2208-4dda-a549-92a0db8c7708size=5";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object, null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string content = await response.Content.ReadAsStringAsync();
                ProblemDetails actual = JsonSerializer.Deserialize<ProblemDetails>(content, _options);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal(expected, actual.Detail);
            }

            /// <summary>
            /// Scenario:
            ///   Get events with  a valid set of query parameters
            /// Expected result:
            ///   Returns a list of events and a next header
            /// Success criteria:
            ///   The response has correct count. Next header is correct.
            /// </summary>
            [Fact]
            public async Task GetEvents_ValidRequest_ReturnsListOfEventsAndNextUrl()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?resource=urn:altinn:resource:systemx&after=0&subject=%2Fparty%2F1337";
                string expectedNext = $"http://localhost:5080/events/api/v1/events?after=e31dbb11-2208-4dda-a549-92a0db8c8808&resource=urn:altinn:resource:systemx&subject=/party/1337";
                int expectedCount = 2;

                HttpClient client = GetTestClient(new EventsServiceMock(3), null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();
                List<CloudEvent> actual = JsonSerializer.Deserialize<List<CloudEvent>>(responseString);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(expectedCount, actual.Count);
                Assert.Equal(expectedNext, response.Headers.GetValues("next").First());
            }

            /// <summary>
            /// Scenario:
            ///   TTD org Get events with  a valid set of query parameters
            /// Expected result:
            ///   Returns a list of events and a next header
            /// Success criteria:
            ///   The response has correct count. Next header is corrcect.
            /// </summary>
            [Fact]
            public async Task GetEvents_ValidRequest_ReturnsNextHeaderWithReplacedAfterParameter()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?resource=urn:altinn:resource:systemx&after=e31dbb11-2208-4dda-a549-92a0db8c7708&subject=/party/1337";
                string expectedNext = $"http://localhost:5080/events/api/v1/events?after=e31dbb11-2208-4dda-a549-92a0db8c8808&resource=urn:altinn:resource:systemx&subject=/party/1337";
                int expectedCount = 1;

                HttpClient client = GetTestClient(new EventsServiceMock(3), null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();
                var actual = JsonSerializer.Deserialize<List<object>>(responseString);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(expectedCount, actual.Count);
                Assert.Equal(expectedNext, response.Headers.GetValues("next").First());
            }

            /// <summary>
            /// Scenario:
            ///   Get events, service throws exception.
            /// Expected result:
            ///   Status code is 500 Internal server error
            /// Success criteria:
            ///   Correct status code is returned.
            /// </summary>
            [Fact]
            public async Task GetEvents_ServiceThrowsException_ReturnsInternalServerError()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?resource=urn:altinn:resource:systemx&after=e31dbb11-2208-4dda-a549-92a0db8c7708&subject=/party/567890";
                Mock<IEventsService> eventsService = new Mock<IEventsService>();
                eventsService.Setup(es => es.GetAppEvents(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Throws(new Exception());
                HttpClient client = GetTestClient(eventsService.Object, null);

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Get events with a valid set of query parameters including alternative subject as header
            /// Expected result:
            ///   Returns a list of events and a next header
            /// Success criteria:
            ///   The response has correct count. Next header is correct.
            /// </summary>
            [Fact]
            public async Task GetEvents_AlternativeSubjectInHeader_ReturnsListOfEventsAndNextUrl()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?resource=urn:altinn:resource:systemx&after=0";
                string expectedNext = $"http://localhost:5080/events/api/v1/events?after=e31dbb11-2208-4dda-a549-92a0db8c8808&resource=urn:altinn:resource:systemx&subject=/party/1337";
                int expectedCount = 2;

                HttpClient client = GetTestClient(new EventsServiceMock(3), null);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, requestUri);
                httpRequestMessage.Headers.Add("Altinn-AlternativeSubject", "/person/01038712345");

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                List<CloudEvent> actual = JsonSerializer.Deserialize<List<CloudEvent>>(responseString);
                Assert.Equal(expectedCount, actual.Count);
                Assert.Equal(expectedNext, response.Headers.GetValues("next").First());
            }

            private HttpClient GetTestClient(IEventsService eventsService = null, IAuthorization authorizationService = null)
            {
                if (eventsService == null)
                {
                    eventsService = new EventsServiceMock();
                }

                if (authorizationService == null)
                {
                    var authorizationMock = new Mock<IAuthorization>();
                    authorizationService = authorizationMock.Object;
                }

                HttpClient client = _factory.WithWebHostBuilder(builder =>
                {
                    IdentityModelEventSource.ShowPII = true;

                    builder.ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        config.AddConfiguration(new ConfigurationBuilder().AddJsonFile("appsettings.unittest.json").Build());
                    });

                    builder.ConfigureTestServices(services =>
                    {
                        services.AddSingleton(eventsService);
                        services.AddSingleton(_traceLogService.Object);
                        services.AddSingleton(authorizationService);

                        // Set up mock authentication so that not well known endpoint is used
                        services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                        services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                        services.AddSingleton<IPDP, PepWithPDPAuthorizationMockSI>();
                    });
                }).CreateClient();

                return client;
            }
        }
    }
}
