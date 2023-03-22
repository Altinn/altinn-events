using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Events.Configuration;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

using static Microsoft.Azure.KeyVault.WebKey.JsonWebKeyVerifier;

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

                _validEvent["resource"] = "urn:altinn:rr:nbib.bokoversikt.api";

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
            public async Task Post_ExternalEventsDisabled_NotFoundResponse()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                HttpClient client = GetTestClient(null);

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(_validEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json")
                };
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.publish"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
            public async void GetEvents_SizeIsLessThanZero_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?size=-5&after=e31dbb11-2208-4dda-a549-92a0db8c8808";
                string expected = "The 'size' parameter must be a number larger that 0.";

                HttpClient client = GetTestClient(new Mock<IEventsService>().Object, true);
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
            ///   Retrieve a list of events, without bearer token.
            /// Expected result:
            ///   Returns HttpStatus Unauthorized.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async void GetEvents_MissingBearerToken_ReturnsUnauthorized()
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

            /// <summary>
            /// Scenario:
            ///   Get events with  a valid set of query parameters
            /// Expected result:
            ///   Returns a list of events and a next header
            /// Success criteria:
            ///   The response has correct count. Next header is correct.
            /// </summary>
            [Fact]
            public async void GetEvents_ValidRequest_ReturnsListOfEventsAndNextUrl()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?source=urn:altinn:systemx&after=0&subject=%2Fparty%2F1337";
                string expectedNext = $"http://localhost:5080/events/api/v1/events?after=e31dbb11-2208-4dda-a549-92a0db8c8808&source=urn:altinn:systemx&subject=/party/1337";
                int expectedCount = 2;

                HttpClient client = GetTestClient(new EventsServiceMock(3), true);
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
            public async void GetEvents_ValidRequest_ReturnsNextHeaderWithReplacesAfterParameter()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?source=urn:altinn:systemx&after=e31dbb11-2208-4dda-a549-92a0db8c7708&subject=/party/1337";
                string expectedNext = $"http://localhost:5080/events/api/v1/events?after=e31dbb11-2208-4dda-a549-92a0db8c8808&source=urn:altinn:systemx&subject=/party/1337";
                int expectedCount = 1;

                HttpClient client = GetTestClient(new EventsServiceMock(3), true);
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
            public async void GetEvents_ServiceThrowsException_ReturnsInternalServerError()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?source=urn:altinn:systemx&after=e31dbb11-2208-4dda-a549-92a0db8c7708&subject=/party/567890";
                Mock<IEventsService> eventsService = new Mock<IEventsService>();
                eventsService.Setup(es => es.GetAppEvents(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<List<string>>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Throws(new Exception());
                HttpClient client = GetTestClient(eventsService.Object, true);

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
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
            public async void GetEvents_MissingRequiredQueryParam_ReturnsBadRequest()
            {
                // Arrange   
                string expected = "The 'after' parameter must be defined.";

                string requestUri = $"{BasePath}/events?source=urn:altinn:systemx&size=5&subject=/party/1337";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object, true);
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
            ///   Get events without defined after in query.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async void GetEvents_MissingSourceParam_ReturnsBadRequest()
            {
                // Arrange   
                string expected = "The 'source' parameter must be defined.";

                string requestUri = $"{BasePath}/events?after=e31dbb11-2208-4dda-a549-92a0db8c7708size=5&subject=/party/1337";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object, true);
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

            [Fact]
            public async Task Get_ExternalEventsDisabled_NotFoundResponse()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?after=e31dbb11-2208-4dda-a549-92a0db8c7708size=5&subject=/party/1337";

                HttpClient client = GetTestClient(null);

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, requestUri);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
            public async void GetEvents_AlternativeSubjectInHeader_ReturnsListOfEventsAndNextUrl()
            {
                // Arrange
                string requestUri = $"{BasePath}/events?source=urn:altinn:systemx&after=0";
                string expectedNext = $"http://localhost:5080/events/api/v1/events?after=e31dbb11-2208-4dda-a549-92a0db8c8808&source=urn:altinn:systemx&subject=/party/1337";
                int expectedCount = 2;

                HttpClient client = GetTestClient(new EventsServiceMock(3), true);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.subscribe"));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, requestUri);
                httpRequestMessage.Headers.Add("Altinn-AlternativeSubject", "/person/01038712345");

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();
                List<CloudEvent> actual = JsonSerializer.Deserialize<List<CloudEvent>>(responseString);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(expectedCount, actual.Count);
                Assert.Equal(expectedNext, response.Headers.GetValues("next").First());
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

                HttpClient client = GetTestClient(null, true);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.publish"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseMessage = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("CloudEvent is missing required attributes: id (Parameter 'data')", responseMessage);
            }

            [Fact]
            public async Task Post_IncorrectContentType_UnsupportedMediaType()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                HttpClient client = GetTestClient(null, true);
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
            public async Task Post_AuthorizedRequest_EventIsRegistered()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                Mock<IEventsService> eventMock = new();
                eventMock.Setup(em => em.RegisterNew(It.IsAny<CloudEvent>()))
                          .ReturnsAsync(Guid.NewGuid().ToString());

                HttpClient client = GetTestClient(eventMock.Object, true);
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

            private HttpClient GetTestClient(IEventsService eventsService = null, bool enableExternalEvents = false)
            {
                if (eventsService == null)
                {
                    eventsService = new EventsServiceMock();
                }

                HttpClient client = _factory.WithWebHostBuilder(builder =>
                {
                    IdentityModelEventSource.ShowPII = true;

                    builder.ConfigureTestServices(services =>
                    {
                        services.AddSingleton(eventsService);
                        services.Configure<GeneralSettings>(opts => opts.EnableExternalEvents = enableExternalEvents);

                        // Set up mock authentication so that not well known endpoint is used
                        services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                        services.AddSingleton<ISigningKeysResolver, SigningKeyResolverMock>();
                        services.AddSingleton<IPDP, PepWithPDPAuthorizationMockSI>();
                    });
                }).CreateClient();

                return client;
            }
        }
    }
}
