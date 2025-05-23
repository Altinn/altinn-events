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
using Altinn.Platform.Events.Controllers;
using Altinn.Platform.Events.Models;
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

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingControllers
{
    /// <summary>
    /// Represents a collection of integration tests.
    /// </summary>
    public partial class IntegrationTests
    {
        /// <summary>
        /// Represents a collection of integration tests of the <see cref="AppController"/>.
        /// </summary>
        public class AppControllerTests : IClassFixture<WebApplicationFactory<AppController>>
        {
            private const string BasePath = "/events/api/v1";

            private readonly WebApplicationFactory<AppController> _factory;

            private readonly JsonSerializerOptions _options;

            private readonly Mock<ITraceLogService> _traceLogServiceMock = new();

            /// <summary>
            /// Initializes a new instance of the <see cref="AppControllerTests"/> class with the given <see cref="WebApplicationFactory{TAppEventsController}"/>.
            /// </summary>
            /// <param name="factory">The <see cref="WebApplicationFactory{TAppEventsController}"/> to use when setting up the test server.</param>
            public AppControllerTests(WebApplicationFactory<AppController> factory)
            {
                _factory = factory;
                _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            }

            /// <summary>
            /// Scenario:
            ///   Post a valid CloudEventRequest instance.
            /// Expected result:
            ///   Returns HttpStatus Created and the Id for the instance.
            /// Success criteria:
            ///   The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_NormalUser_GivenValidCloudEvent_ReturnsStatusCreated()
            {
                // Arrange
                string requestUri = $"{BasePath}/app";
                AppCloudEventRequestModel cloudEvent = GetCloudEventRequest();

                Mock<IEventsService> eventsService = new();
                eventsService.Setup(s => s.RegisterNew(It.Is<CloudEvent>(c => !string.IsNullOrEmpty(c.Id) && c.Time != DateTimeOffset.MinValue))).ReturnsAsync((CloudEvent c) => c.Id);

                HttpClient client = GetTestClient(eventsService.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/json")
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v2"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post a valid CloudEventRequest instance as a system user.
            /// Expected result:
            ///   Returns HttpStatus Created and the Id for the instance.
            /// Success criteria:
            ///   The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_SystemUser_GivenValidCloudEvent_ReturnsStatusCreated()
            {
                // Arrange
                string requestUri = $"{BasePath}/app";
                AppCloudEventRequestModel cloudEvent = GetCloudEventRequest();

                Mock<IEventsService> eventsService = new();
                eventsService.Setup(s => s.RegisterNew(It.Is<CloudEvent>(c => !string.IsNullOrEmpty(c.Id) && c.Time != DateTimeOffset.MinValue))).ReturnsAsync((CloudEvent c) => c.Id);

                HttpClient client = GetTestClient(eventsService.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetTokenForSystemUser("random_system_identifier", Convert.ToString(Guid.NewGuid()), "random_org_cliam_identifier"));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/json")
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v2"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post a valid CloudEventRequest instance but with a non matching access token.
            /// Expected result:
            ///   Returns HttpStatus not authorized
            /// Success criteria:
            ///   The request is not authorized
            /// </summary>
            [Fact]
            public async Task Post_GivenValidCloudEvent_NotAuthorized()
            {
                // Arrange
                string requestUri = $"{BasePath}/app";
                string responseId = Guid.NewGuid().ToString();
                AppCloudEventRequestModel cloudEvent = GetCloudEventRequest();

                Mock<IEventsService> eventsService = new Mock<IEventsService>();
                eventsService.Setup(s => s.RegisterNew(It.IsAny<CloudEvent>())).ReturnsAsync(responseId);

                HttpClient client = GetTestClient(eventsService.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/json")
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v3"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post an invalid CloudEvent instance.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task Post_CloudEventMissingSubject_ReturnsStatusBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/app";
                AppCloudEventRequestModel cloudEvent = GetCloudEventRequest();
                cloudEvent.Subject = null;

                Mock<IEventsService> eventsService = new Mock<IEventsService>();

                HttpClient client = GetTestClient(eventsService.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/json")
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post a invalid CloudEvent instance.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task Post_CloudEventMissingSource_ReturnsStatusBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/app";
                AppCloudEventRequestModel cloudEvent = GetCloudEventRequest();
                cloudEvent.Source = null;

                Mock<IEventsService> eventsService = new Mock<IEventsService>();

                HttpClient client = GetTestClient(eventsService.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/json")
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post a invalid CloudEvent instance.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task Post_CloudEventOrgDoesNotMatch_ReturnsStatusBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/app";
                AppCloudEventRequestModel cloudEvent = GetCloudEventRequest("skd");

                Mock<IEventsService> eventsService = new Mock<IEventsService>();

                HttpClient client = GetTestClient(eventsService.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/json")
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post a valid cloud event, unexpected error when storing document
            /// Expected result:
            ///   Returns HttpStatus Internal Server Error.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task Post_RepositoryThrowsException_ReturnsInternalServerError()
            {
                // Arrange
                string requestUri = $"{BasePath}/app";
                AppCloudEventRequestModel cloudEvent = GetCloudEventRequest();
                Mock<IEventsService> eventsService = new Mock<IEventsService>();
                eventsService.Setup(er => er.RegisterNew(It.IsAny<CloudEvent>())).Throws(new Exception());
                HttpClient client = GetTestClient(eventsService.Object);

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/json")
                };
                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v2"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post a cloud event, without bearer token.
            /// Expected result:
            ///   Returns HttpStatus Unauthorized.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task Post_MissingBearerToken_ReturnsForbidden()
            {
                // Arrange
                string requestUri = $"{BasePath}/app";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);

                StringContent content = new StringContent(string.Empty);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = content };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post a cloud event, without access token.
            /// Expected result:
            ///   Returns HttpStatus Forbidden.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task Post_MissingAccessToken_ReturnsForbidden()
            {
                // Arrange
                string requestUri = $"{BasePath}/app";

                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));

                StringContent content = new StringContent(string.Empty);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = content };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Get events without defined after or from in query.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetForOrg_MissingRequiredFromOrAfterParam_ReturnsBadRequest()
            {
                // Arrange
                string expected = "The 'From' or 'After' parameter must be defined.";

                string requestUri = $"{BasePath}/app/ttd/endring-av-navn-v2?size=5";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

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
            ///   Get events with negative size.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetForOrg_SizeIsLessThanZero_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/ttd/endring-av-navn-v2?from=2020-01-01Z&size=-5";
                string expected = "The 'Size' parameter must be a number larger that 0.";

                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

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
            public async Task GetForOrg_MissingBearerToken_ReturnsUnauthorized()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/ttd/endring-av-navn-v2?from=2020-01-01Z&party=1337";
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
            ///   The response has correct count. Next header is corrcect.
            /// </summary>
            [Fact]
            public async Task GetForOrg_ValidRequest_ReturnsListOfEventsAndNextUrl()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/ttd/endring-av-navn-v2?from=2020-01-01Z&party=1337";
                string expectedNext = $"http://localhost:5080/events/api/v1/app/ttd/endring-av-navn-v2?after=e31dbb11-2208-4dda-a549-92a0db8c8808&from=2020-01-01Z&party=1337";
                int expectedCount = 2;

                HttpClient client = GetTestClient(new EventsServiceMock(1));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

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
            ///   Get events with a valid set of query parameters. No subject specification
            /// Expected result:
            ///   Returns a list of events and a next header
            /// Success criteria:
            ///   The response has correct count. Next header is corrcect.
            /// </summary>
            [Fact]
            public async Task GetForOrg_ValidRequest_NoSubject_ReturnsListOfEventsAndNextUrl()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/ttd/endring-av-navn-v2?from=2020-01-01Z";
                string expectedNext = $"http://localhost:5080/events/api/v1/app/ttd/endring-av-navn-v2?after=e31dbb11-2208-4dda-a549-92a0db8c8808&from=2020-01-01Z";
                int expectedCount = 2;

                HttpClient client = GetTestClient(new EventsServiceMock(1));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

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
            public async Task GetForOrg_ValidRequest_ForTTD_ReturnsNextHeaderWithReplacesAfterParameter()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/ttd/endring-av-navn-v2?after=e31dbb11-2208-4dda-a549-92a0db8c7708&from=2020-01-01Z&party=1337";
                string expectedNext = $"http://localhost:5080/events/api/v1/app/ttd/endring-av-navn-v2?after=e31dbb11-2208-4dda-a549-92a0db8c8808&from=2020-01-01Z&party=1337";
                int expectedCount = 1;

                HttpClient client = GetTestClient(new EventsServiceMock(1));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

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
            ///   Get events events service throws exception.
            /// Expected result:
            ///   Next header contains new guid in after parameter
            /// Success criteria:
            ///   Next header is corrcect.
            /// </summary>
            [Fact]
            public async Task GetForOrg_ServiceThrowsException_ReturnsInternalServerError()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/ttd/endring-av-navn-v2?after=e31dbb11-2208-4dda-a549-92a0db8c7708&party=567890";
                Mock<IEventsService> eventsService = new Mock<IEventsService>();
                eventsService.Setup(es => es.GetAppEvents(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Throws(new Exception());
                HttpClient client = GetTestClient(eventsService.Object);

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Gets the events for org with from as a datetime without timezone.
            /// Expected result:
            ///   The result be a problem detail object
            /// Success criteria:
            ///   Result status is 400 bad request and the problem details specifying which parameter is incorrect.
            /// </summary>
            [Fact]
            public async Task GetForOrg_FromMissingTimeZone_ReturnsBadRequest()
            {
                // Arrange
                Mock<IEventsService> serviceMock = new();

                string requestUri = $"{BasePath}/app/ttd/apps-test?from=2022-07-07T11:00:53.3917";
                HttpClient client = GetTestClient(serviceMock.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();
                ProblemDetails actual = JsonSerializer.Deserialize<ProblemDetails>(responseString, _options);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.StartsWith("The 'From' parameter must specify timezone.", actual.Detail);
            }

            /// <summary>
            /// Scenario:
            ///   Gets the events for org with To as a datetime without timezone.
            /// Expected result:
            ///   The result be a problem detail object
            /// Success criteria:
            ///   Result status is 400 bad request and the problem details specifying which parameter is incorrect.
            /// </summary>
            [Fact]
            public async Task GetForOrg_ToMissingTimeZone_ReturnsBadRequest()
            {
                // Arrange
                Mock<IEventsService> serviceMock = new();

                string requestUri = $"{BasePath}/app/ttd/apps-test?after=1&to=2022-07-07T11:00:53.3917";
                HttpClient client = GetTestClient(serviceMock.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();
                ProblemDetails actual = JsonSerializer.Deserialize<ProblemDetails>(responseString, _options);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.StartsWith("The 'To' parameter must specify timezone.", actual.Detail);
            }

            /// <summary>
            /// Scenario:
            ///   Get events without defined after or from in query.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetForParty_MissingRequiredQueryParam_ReturnsBadRequest()
            {
                // Arrange   
                string expected = "The 'From' or 'After' parameter must be defined.";

                string requestUri = $"{BasePath}/app/party?size=5";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));

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
            ///   Get events with negative size.
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetForParty_SizeIsLessThanZero_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?from=2020-01-01Z&size=-5";
                string expected = "The 'Size' parameter must be a number larger that 0.";

                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));

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
            ///   Get events without subject (pary, unit or person).
            /// Expected result:
            ///   Returns HttpStatus BadRequest.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetForParty_MissingSubject_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?from=2020-01-01Z&size=5";
                string expected = "Subject must be specified using either query params party or unit or header value person.";

                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));

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
            ///   Post a cloud event, without bearer token.
            /// Expected result:
            ///   Returns HttpStatus Unauthorized.
            /// Success criteria:
            ///   The response has correct status.
            /// </summary>
            [Fact]
            public async Task GetForParty_MissingBearerToken_ReturnsForbidden()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?from=2020-01-01&party=1337&app=apps-test&size=5";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Get events with  a valid set of query parameters, patyId as input
            /// Expected result:
            ///   Returns a list of events and a next header
            /// Success criteria:
            ///   The response has correct count. Next header is corrcect.
            /// </summary>
            [Fact]
            public async Task GetForParty_ValidRequestParyId_ReturnsListOfEventsAndNextUrl()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?from=2020-01-01Z&party=1337&size=5";
                string expectedNext = $"http://localhost:5080/events/api/v1/app/party?after=e31dbb11-2208-4dda-a549-92a0db8c8808&from=2020-01-01Z&party=1337&size=5";

                int expectedCount = 2;

                HttpClient client = GetTestClient(new EventsServiceMock(1));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));

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
            ///   Get events with  a valid set of query parameters, person as input
            /// Expected result:
            ///   Returns a list of events and a next header
            /// Success criteria:
            ///   The response has correct count. Next header is corrcect.
            /// </summary>
            [Fact]
            public async Task GetForParty_ValidRequestPerson_ReturnsListOfEventsAndNextUrl()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?from=2020-01-01Z&size=5";
                string expectedNext = $"http://localhost:5080/events/api/v1/app/party?after=e31dbb11-2208-4dda-a549-92a0db8c8808&from=2020-01-01Z&size=5";

                int expectedCount = 2;

                HttpClient client = GetTestClient(new EventsServiceMock(1));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
                httpRequestMessage.Headers.Add("Person", "01038712345");

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
            ///   Get events with  a valid set of query parameters
            /// Expected result:
            ///   Returns a list of events and a next header
            /// Success criteria:
            ///   The response has correct count. Next header is corrcect.
            /// </summary>
            [Fact]
            public async Task GetForParty_ValidRequestPartyIdAndAfter_ReturnsNextHeaderWithReplacesAfterParameter()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?after=e31dbb11-2208-4dda-a549-92a0db8c7708&from=2020-01-01Z&party=1337&size=5";
                string expectedNext = $"http://localhost:5080/events/api/v1/app/party?after=e31dbb11-2208-4dda-a549-92a0db8c8808&from=2020-01-01Z&party=1337&size=5";

                int expectedCount = 1;

                HttpClient client = GetTestClient(new EventsServiceMock(1));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));

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
            ///   Get events with  a valid set of query parameters, patyId as input
            /// Expected result:
            ///   Returns a list of events and a next header
            /// Success criteria:
            ///   The response has correct count. Next header is corrcect.
            /// </summary>
            [Fact]
            public async Task GetForParty_ValidRequestParyId_ReturnsListOfEventsAndNextUrlTest()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?from=2020-01-01Z&party=1337&org=ttd&app=endring-av-navn-v2&size=5";
                string expectedNext = $"http://localhost:5080/events/api/v1/app/party?after=e31dbb11-2208-4dda-a549-92a0db8c8808&from=2020-01-01Z&party=1337&org=ttd&app=endring-av-navn-v2&size=5";

                int expectedCount = 2;

                HttpClient client = GetTestClient(new EventsServiceMock(1));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));

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
            ///   Get events events service throws exception.
            /// Expected result:
            ///   Next header contains new guid in after parameter
            /// Success criteria:
            ///   Next header is corrcect.
            /// </summary>
            [Fact]
            public async Task GetForParty_ServiceThrowsException_ReturnsInternalServerError()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?after=e31dbb11-2208-4dda-a549-92a0db8c7708&party=1337";
                Mock<IEventsService> eventsService = new Mock<IEventsService>();
                eventsService.Setup(es => es.GetAppEvents(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Throws(new Exception());
                HttpClient client = GetTestClient(eventsService.Object);

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Gets the events for party specifying the source using wildcard '%' for app name.
            /// Expected result:
            ///   The result should contain all (2) events from ttd/endring-av-navn-v2
            /// Success criteria:
            ///   Result status is 200 OK and number of events is 2
            /// </summary>
            [Fact]
            public async Task GetForParty_WildcardApp_ReturnsOk()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?from=2020-01-01Z&party=1337&source=https://ttd.apps.altinn.no/ttd/%";
                HttpClient client = GetTestClient(new EventsServiceMock(1));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();
                List<CloudEvent> actual = JsonSerializer.Deserialize<List<CloudEvent>>(responseString);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Gets the events for party with from as a datetime without timezone.
            /// Expected result:
            ///   The result be a problem detail object
            /// Success criteria:
            ///   Result status is 400 bad request and the problem details specifying which parameter is incorrect.
            /// </summary>
            [Fact]
            public async Task GetForParty_FromMissingTimeZone_ReturnsBadRequest()
            {
                // Arrange
                Mock<IEventsService> serviceMock = new();

                string requestUri = $"{BasePath}/app/party?from=2022-07-07T11:00:53.3917";
                HttpClient client = GetTestClient(serviceMock.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();
                var actual = JsonSerializer.Deserialize<ProblemDetails>(responseString, _options);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

                Assert.StartsWith("The 'From' parameter must specify timezone.", actual.Detail);
            }

            /// <summary>
            /// Scenario:
            ///   Gets the events for party with To as a datetime without timezone.
            /// Expected result:
            ///   The result be a problem detail object
            /// Success criteria:
            ///   Result status is 400 bad request and the problem details specifying which parameter is incorrect.
            /// </summary>
            [Fact]
            public async Task GetForParty_ToMissingTimeZone_ReturnsBadRequest()
            {
                // Arrange
                Mock<IEventsService> serviceMock = new();

                string requestUri = $"{BasePath}/app/party?after=1&to=2022-07-07T11:00:53.3917";
                HttpClient client = GetTestClient(serviceMock.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();
                var actual = JsonSerializer.Deserialize<ProblemDetails>(responseString, _options);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.StartsWith("The 'To' parameter must specify timezone.", actual.Detail);
            }

            /// <summary>
            /// Scenario:
            ///   Get events with both partyId and unit defined as query parameters
            /// Expected result:
            ///   The result be a problem detail object
            /// Success criteria:
            ///   Result status is 400 bad request and the problem details specifying which parameter is incorrect.
            /// </summary>
            [Fact]
            public async Task GetForParty_ConflicingQueryParamsUnitAndParty_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?after=e31dbb11-2208-4dda-a549-92a0db8c7708&from=2020-01-01Z&party=1337&size=5&unit=12345";

                HttpClient client = GetTestClient(new EventsServiceMock(1));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();
                var actual = JsonSerializer.Deserialize<ProblemDetails>(responseString, _options);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.StartsWith("Only one of 'Party' or 'Unit' can be defined.", actual.Detail);
            }

            /// <summary>
            /// Scenario:
            ///   Get events with both partyId and person number defined as filter parameters
            /// Expected result:
            ///   The result be a problem detail object
            /// Success criteria:
            ///   Result status is 400 bad request and the problem details specifying which parameter is incorrect.
            /// </summary>
            [Fact]
            public async Task GetForParty_ConflicingQueryParamsPersonAndParty_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/app/party?after=e31dbb11-2208-4dda-a549-92a0db8c7708&from=2020-01-01Z&party=1337&size=5";

                HttpClient client = GetTestClient(new EventsServiceMock(1));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
                httpRequestMessage.Headers.Add("Person", "01038712345");

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string responseString = await response.Content.ReadAsStringAsync();
                var actual = JsonSerializer.Deserialize<ProblemDetails>(responseString, _options);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.StartsWith("Only one of 'Party' or 'Person' can be defined.", actual.Detail);
            }

            private HttpClient GetTestClient(IEventsService eventsService, ITraceLogService traceLogService = null)
            {
                HttpClient client = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        config.AddConfiguration(new ConfigurationBuilder().AddJsonFile("appsettings.unittest.json").Build());
                    });

                    builder.ConfigureTestServices(services =>
                    {
                        services.AddSingleton(eventsService);

                        if (traceLogService != null)
                        {
                            services.AddSingleton(traceLogService);
                        }
                        else
                        {
                            services = services.AddSingleton(_traceLogServiceMock.Object);
                        }

                        // Set up mock authentication so that not well known endpoint is used
                        services.AddSingleton<IPDP, PepWithPDPAuthorizationMockSI>();
                        services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                        services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                    });
                }).CreateClient();

                return client;
            }

            private static AppCloudEventRequestModel GetCloudEventRequest(string org = "ttd")
            {
                AppCloudEventRequestModel cloudEvent = new AppCloudEventRequestModel
                {
                    SpecVersion = "1.0",
                    Type = "instance.created",
                    Source = new Uri($"https://{org}.apps.altinn.no/{org}/endring-av-navn-v2/232243423"),
                    Subject = "/party/456456",
                    Data = "something/extra",
                };

                return cloudEvent;
            }
        }
    }
}
