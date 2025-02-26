using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Events.Controllers;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.Tests.Mocks.Authentication;
using Altinn.Platform.Events.Tests.Utils;
using Altinn.Platform.Events.UnitTest.Mocks;

using AltinnCore.Authentication.JwtCookie;

using CloudNative.CloudEvents;

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
        /// Represents a collection of integration tests of the <see cref="StorageController"/>.
        /// </summary>
        public class StorageControllerTests : IClassFixture<WebApplicationFactory<StorageController>>
        {
            private const string BasePath = "/events/api/v1";

            private readonly WebApplicationFactory<StorageController> _factory;

            private readonly Mock<ITraceLogService> _traceLogServiceMock = new Mock<ITraceLogService>();

            /// <summary>
            /// Initializes a new instance of the <see cref="StorageControllerTests"/> class with the given <see cref="WebApplicationFactory{TStorageController}"/>.
            /// </summary>
            /// <param name="factory">The <see cref="WebApplicationFactory{TStorageController}"/> to use when setting up the test server.</param>
            public StorageControllerTests(WebApplicationFactory<StorageController> factory)
            {
                _factory = factory;
            }

            /// <summary>
            /// Scenario:
            ///   Post a valid CloudEventRequest instance.
            /// Expected result:
            ///   Returns HttpStatus Ok.
            /// Success criteria:
            ///   The response has correct status code.
            /// </summary>
            [Fact]
            public async Task Post_GivenValidCloudEvent_ReturnsStatusCreatedAndCorrectData()
            {
                // Arrange
                string requestUri = $"{BasePath}/storage/events";
                string responseId = Guid.NewGuid().ToString();
                var cloudEvent = GetCloudEventRequest();

                Mock<IEventsService> eventsService = new Mock<IEventsService>();
                eventsService.Setup(s => s.Save(It.IsAny<CloudEvent>())).ReturnsAsync(responseId);

                HttpClient client = GetTestClient(eventsService.Object);
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json")
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v2"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.True(response.IsSuccessStatusCode);
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
                string requestUri = $"{BasePath}/storage/events";
                var cloudEvent = GetCloudEventRequest();
                Mock<IEventsService> eventsService = new Mock<IEventsService>();
                eventsService.Setup(er => er.Save(It.IsAny<CloudEvent>())).Throws(new Exception());
                HttpClient client = GetTestClient(eventsService.Object);

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json")
                };
                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v2"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
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
                string requestUri = $"{BasePath}/storage/events";
                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);

                StringContent content = new(string.Empty);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/cloudevents+json");
                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri) { Content = content };

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
                string requestUri = $"{BasePath}/storage/events";

                HttpClient client = GetTestClient(new Mock<IEventsService>().Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));

                StringContent content = new(string.Empty);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/cloudevents+json");
                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri) { Content = content };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post a request that results in an exception being thrown by the trace log service instance.
            /// Expected result:
            ///   Returns internal server error 500.
            /// Success criteria:
            ///   The response has correct status code.
            /// </summary>
            [Fact]
            public async Task Logs_WhenExceptionIsThrownByTheService_IsCaughtAnd500IsReturned()
            {
                // Arrange
                string requestUri = $"{BasePath}/storage/events/logs";
                string responseId = Guid.NewGuid().ToString();

                Mock<IEventsService> eventsService = new();
                Mock<ITraceLogService> traceLogService = new();

                traceLogService.Setup(x => x.CreateWebhookResponseEntry(It.IsAny<LogEntryDto>())).Throws(new Exception());

                HttpClient client = GetTestClient(eventsService.Object, traceLogService.Object);
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new LogEntryDto()), Encoding.UTF8, "application/cloudevents+json")
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v2"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }

            /// <summary>
            /// Scenario: 
            ///   A possibly invalid log entry is posted to the logs endpoint.
            /// Expected result:
            ///   400 Bad request is returned
            /// Success criteria:
            ///   Empty string returned from the service results in a bad request response
            /// </summary>
            /// <returns></returns>
            [Fact]
            public async Task Logs_WhenEmptyStringIsReturnedFromTheService_ReturnBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/storage/events/logs";
                string responseId = string.Empty;
                Mock<IEventsService> eventsService = new();
                Mock<ITraceLogService> traceLogService = new();
                traceLogService.Setup(x => x.CreateWebhookResponseEntry(It.IsAny<LogEntryDto>())).ReturnsAsync(string.Empty);
                HttpClient client = GetTestClient(eventsService.Object, traceLogService.Object);
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new LogEntryDto()), Encoding.UTF8, "application/cloudevents+json")
                };
                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v2"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post a valid logs request with a logEntryDto.
            /// Expected result:
            ///   Returns HttpStatus Ok.
            /// Success criteria:
            ///   The response has correct status code.
            /// </summary>
            [Fact]
            public async Task Logs_ValidLogEntryDto_ReturnsStatusCreated()
            {
                // Arrange
                string requestUri = $"{BasePath}/storage/events/logs";
                string responseId = Guid.NewGuid().ToString();

                Mock<IEventsService> eventsService = new Mock<IEventsService>();
                Mock<ITraceLogService> traceLogService = new Mock<ITraceLogService>();

                traceLogService.Setup(x => x.CreateWebhookResponseEntry(It.IsAny<LogEntryDto>())).ReturnsAsync(responseId);

                var client = GetTestClient(eventsService.Object, traceLogService.Object);

                string logEntryDto = JsonSerializer.Serialize(new LogEntryDto());

                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(logEntryDto, Encoding.UTF8, "application/cloudevents+json")
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v2"));

                // Act
                var response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.True(response.IsSuccessStatusCode);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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
                            services.AddSingleton(_traceLogServiceMock.Object);
                        }

                        // Set up mock authentication so that not well known endpoint is used
                        services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                        services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                        services.AddSingleton<IPDP, PepWithPDPAuthorizationMockSI>();
                    });
                }).CreateClient();

                return client;
            }

            private static CloudEvent GetCloudEventRequest()
            {
                return new CloudEvent(CloudEventsSpecVersion.V1_0)
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "system.event.occurred",
                    Subject = "/person/16069412345",
                    Source = new Uri("urn:isbn:1234567890"),
                };
            }
        }
    }
}
