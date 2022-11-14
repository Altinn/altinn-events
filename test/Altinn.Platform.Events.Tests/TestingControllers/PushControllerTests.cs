using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

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

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
        /// Represents a collection of integration tests of the <see cref="PushController"/>.
        /// </summary>
        public class PushControllerTests : IClassFixture<WebApplicationFactory<PushController>>
        {
            private const string BasePath = "/events/api/v1";

            private readonly WebApplicationFactory<PushController> _factory;

            /// <summary>
            /// Initializes a new instance of the <see cref="PushControllerTests"/> class with the given <see cref="WebApplicationFactory{TPushController}"/>.
            /// </summary>
            /// <param name="factory">The <see cref="WebApplicationFactory{TPushController}"/> to use when setting up the test server.</param>
            public PushControllerTests(WebApplicationFactory<PushController> factory)
            {
                _factory = factory;
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
            public async void Post_GivenValidCloudEvent_ReturnsStatusCreatedAndCorrectData()
            {
                // Arrange
                string requestUri = $"{BasePath}/push";
                string responseId = Guid.NewGuid().ToString();
                AppCloudEventRequestModel cloudEvent = GetCloudEventRequest();

                Mock<IOutboundService> service = new Mock<IOutboundService>();
                service.Setup(s => s.PostOutbound(It.IsAny<CloudEvent>()));

                HttpClient client = GetTestClient(service.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEvent.Serialize(), Encoding.UTF8, "application/json")
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v2"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
            public async void Post_RepositoryThrowsException_ReturnsInternalServerError()
            {
                // Arrange
                string requestUri = $"{BasePath}/push";
                AppCloudEventRequestModel cloudEvent = GetCloudEventRequest();
                Mock<IOutboundService> service = new Mock<IOutboundService>();
                service.Setup(er => er.PostOutbound(It.IsAny<CloudEvent>())).Throws(new Exception());
                HttpClient client = GetTestClient(service.Object);

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
            public async void Post_MissingBearerToken_ReturnsForbidden()
            {
                // Arrange
                string requestUri = $"{BasePath}/push";
                HttpClient client = GetTestClient(new Mock<IOutboundService>().Object);

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
            public async void Post_MissingAccessToken_ReturnsForbidden()
            {
                // Arrange
                string requestUri = $"{BasePath}/push";

                HttpClient client = GetTestClient(new Mock<IOutboundService>().Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1));

                StringContent content = new StringContent(string.Empty);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = content };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            private HttpClient GetTestClient(IOutboundService outboundService)
            {
                HttpClient client = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.AddSingleton(outboundService);

                        // Set up mock authentication so that not well known endpoint is used
                        services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                        services.AddSingleton<ISigningKeysResolver, SigningKeyResolverMock>();
                        services.AddSingleton<IPDP, PepWithPDPAuthorizationMockSI>();
                    });
                }).CreateClient();

                return client;
            }

            private static AppCloudEventRequestModel GetCloudEventRequest()
            {
                AppCloudEventRequestModel cloudEvent = new AppCloudEventRequestModel
                {
                    SpecVersion = "1.0",
                    Type = "instance.created",
                    Source = new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/232243423"),
                    Subject = "/party/456456",
                    Data = "something/extra",
                };

                return cloudEvent;
            }
        }
    }
}
