﻿using System;
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

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
            private readonly string _invalidEvent;
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
                    Source = new Uri("urn:isbn:1234567890"),
                };

            }

            [Fact]
            public async Task Post_MissingBearerToken_UnauthorizedResponse()
            {
                // Arrange
                string requestUri = $"{BasePath}/events";

                HttpClient client = GetTestClient(null);
                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(_validEvent.SerializeCloudEvent(), Encoding.UTF8, "application/cloudevents+json")
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
                    Content = new StringContent(_validEvent.SerializeCloudEvent(), Encoding.UTF8, "application/cloudevents+json")
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
                    Content = new StringContent(_validEvent.SerializeCloudEvent(), Encoding.UTF8, "application/cloudevents+json")
                };
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("digdir", scope: "altinn:events.publish"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
                Assert.Contains("CloudEvent is missing required attributes", responseMessage);
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
                    Content = new StringContent(_validEvent.SerializeCloudEvent(), Encoding.UTF8, "application/json")
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
                    Content = new StringContent(_validEvent.SerializeCloudEvent(), Encoding.UTF8, "application/cloudevents+json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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
