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
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.Tests.Mocks.Authentication;
using Altinn.Platform.Events.Tests.Utils;
using Altinn.Platform.Events.UnitTest.Mocks;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

            /// <summary>
            /// Initializes a new instance of the <see cref="AppControllerTests"/> class with the given <see cref="WebApplicationFactory{TAppEventsController}"/>.
            /// </summary>
            /// <param name="factory">The <see cref="WebApplicationFactory{TEventsController}"/> to use when setting up the test server.</param>
            public EventsControllerTests(WebApplicationFactory<EventsController> factory)
            {
                _factory = factory;
            }

            [Fact]
            public async Task Post_MissingBearerToken_UnauhtorizedResponse()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                HttpClient client = GetTestClient(null);
                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri) { Content = null };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            public async Task Post_ValidScopee_EventIsRegistered()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                // TODO: add service mock
                HttpClient client = GetTestClient(null);
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
                };

                // TODO: PEP not executed.. must figure out why
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetTokenWithScope("altinn:events:publish"));

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
