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
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Controllers;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
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
        /// Represents a collection of integration tests of the <see cref="SubscriptionController"/>.
        /// </summary>
        public class SubscriptionControllerTests : IClassFixture<WebApplicationFactory<SubscriptionController>>
        {
            private const string BasePath = "/events/api/v1";

            private readonly WebApplicationFactory<SubscriptionController> _factory;

            private readonly JsonSerializerOptions _jsonOptions;

            /// <summary>
            /// Initializes a new instance of the <see cref="SubscriptionControllerTests"/> class with the given <see cref="WebApplicationFactory{TSubscriptionController}"/>.
            /// </summary>
            /// <param name="factory">The <see cref="WebApplicationFactory{TSubscriptionController}"/> to use when setting up the test server.</param>
            public SubscriptionControllerTests(WebApplicationFactory<SubscriptionController> factory)
            {
                _factory = factory;
                _jsonOptions = new() { PropertyNameCaseInsensitive = true };
            }

            /// <summary>
            /// Scenario:
            ///   Post a valid EventsSubscription for SKD
            /// Expected result:
            ///   Returns HttpStatus Created and the url with object for the resource created.
            /// Success criteria:
            ///   The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_GivenValidSubscription_ReturnsStatusCreatedAndCorrectData()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://skd.apps.altinn.no/skd/flyttemelding", "https://www.skatteetaten.no/hook");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("skd"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);

                string content = response.Content.ReadAsStringAsync().Result;
                Assert.Contains(cloudEventSubscription.SourceFilter.AbsoluteUri, content);
            }

            /// <summary>
            /// Scenario: Invalid path that includes / at end
            /// Expected: Returns bad request
            /// </summary>
            /// <returns></returns>
            [Fact]
            public async Task Post_GivenInvalidCloudEventSubscription_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://skd.apps.altinn.no/skd/flyttemelding/", "https://www.skatteetaten.no/hook");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("skd"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            /// <summary>
            /// Scenario: Invalid source provided relative URI, absolute requied
            /// Expected: Returns bad request
            /// </summary>        
            [Fact]
            public async Task Post_GivenInvalidSubscription_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("skd/flyttemelding", "https://www.skatteetaten.no/hook");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("skd"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post a valid EventsSubscription for SKD
            /// Expected result:
            ///   Returns HttpStatus Created and the url with object for the resource created.
            /// Success criteria:
            ///   The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_OrgSubscriptionWithMissing_ReturnsStatusCreatedAndCorrectData()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://skd.apps.altinn.no/", "https://www.skatteetaten.no/hook");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("skd"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post an invalid eventssubscription for user 1337. 
            /// Expected result:
            ///   Returns HttpStatus BadRequest because it is missing subject. This is not allowed as end user
            /// Success criteria:
            ///   The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_GivenInvalidValidSubscriptionUser_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://skd.apps.altinn.no/", "https://www.skatteetaten.no/hook");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post an invalid eventssubscription for user 1337. 
            /// Expected result:
            ///   Returns HttpStatus BadRequest because it is missing subject. This is not allowed as end user
            /// Success criteria:
            ///   The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_GivenValidSubscriptionUser_ReturnsCreated()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://skd.apps.altinn.no/skd/flyttemelding", "https://www.skatteetaten.no/hook", alternativeSubjectFilter: "/person/01039012345");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }

            /// <summary>
            /// Scenario:
            ///   Post an invalid eventssubscription for organization 950474084. 
            /// Expected result:
            ///   Returns HttpStatus BadRequest because it is mismatch between subject and identity
            /// Success criteria:
            ///   The response has correct status
            /// </summary>
            [Fact]
            public async Task Post_GivenSubscriptionOrganizationWithInvalidSubject_ReturnsNotAuthorized()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://skd.apps.altinn.no/skd/flyttemelding", "https://www.skatteetaten.no/hook", alternativeSubjectFilter: "/org/950474084");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(null, "897069651"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            /// <summary>
            /// Post valid subscription for organization
            /// Expected result:
            /// Returns HttpStatus created
            /// Success criteria:
            /// The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_GivenSubscriptionOrganizationWithValidSubject_ReturnsCreated()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://skd.apps.altinn.no/skd/flyttemelding", "https://www.skatteetaten.no/hook", alternativeSubjectFilter: "/org/950474084");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(null, "950474084"));
                HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }

            /// <summary>
            /// Post valid subscription for user with organization as subject
            /// Expected result:
            /// Returns HttpStatus created
            /// Success criteria:
            /// The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_GivenSubscriptionUserWithValidOrganizationSubject_ReturnsCreated()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2", "https://www.ttd.no/hook", alternativeSubjectFilter: "/org/897069650");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }

            /// <summary>
            /// Post valid subscription for user with persn as subject
            /// Expected result:
            /// Returns HttpStatus created
            /// Success criteria:
            /// The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_GivenSubscriptionUserWithValidPersonSubject_ReturnsCreated()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2", "https://www.ttd.no/hook", alternativeSubjectFilter: "/person/01039012345");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }

            /// <summary>
            /// Post invalid subscription for user with person as subject. User missing required role.
            /// Expected result:
            /// Returns HttpStatus Forbidden
            /// Success criteria:
            /// The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_GivenSubscriptionUserWithInvalidPersonSubject_ReturnsCreated()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2", "https://www.ttd.no/hook", alternativeSubjectFilter: "/person/16069412345");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            /// <summary>
            /// Post invalid subscription for org with invalid subject
            /// Expected result:
            /// Returns HttpStatus badrequest
            /// Success criteria:
            /// The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_GivenSubscriptionOrgWithInvalidSubject_ReturnsBadRequest()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://skd.apps.altinn.no/skd/flyttemelding", "https://www.skatteetaten.no/hook", alternativeSubjectFilter: "/organization/960474084");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("skd", "950474084"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            /// <summary>
            /// Post missing bearer token
            /// Expected result:
            /// Returns HttpStatus Unauthorized
            /// Success criteria:
            /// The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_GivenMissingBearerToken_ReturnsUnauthorized()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://skd.apps.altinn.no/skd/flyttemelding", "https://www.skatteetaten.no/hook", alternativeSubjectFilter: "/organization/960474084");

                HttpClient client = GetTestClient();
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            /// <summary>
            /// Post valid subscription for an external event. Missing required scope in token.
            /// Expected result:
            /// Returns HttpStatus forbidden
            /// Success criteria:
            /// The response has correct status and correct responseId.
            /// </summary>
            [Fact]
            public async Task Post_GivenSubscriptionForExternalEventWithoutScope_ReturnsForbidden()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://hunderpasseren.no/by/bronnoysund", "https://www.skatteetaten.no/hook", subjectFilter: "/hund/ascii");

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("skd", "950474084"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            /// <summary>
            /// Post valid subscription for an external event. 
            /// Expected result:
            /// Service for creating generic subscription is called.
            /// Success criteria:
            /// The expected method in the service is called.
            /// </summary>
            [Fact]
            public async Task Post_GivenSubscriptionForExternalEvent_RightServiceMethodIsCalled()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://hunderpasseren.no/by/bronnoysund", "https://www.skatteetaten.no/hook", subjectFilter: "/hund/ascii");

                Mock<ISubscriptionService> serivceMock = new();
                serivceMock.Setup(s => s.CreateGenericSubscription(It.IsAny<Subscription>())).ReturnsAsync((new Subscription { Id = 1 }, null));
                serivceMock.Setup(s => s.CreateAppSubscription(It.IsAny<Subscription>())).ReturnsAsync((new Subscription { Id = 2 }, null));

                HttpClient client = GetTestClient(serivceMock.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("skd", "950474084", "altinn:events.subscribe"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                serivceMock.Verify(s => s.CreateGenericSubscription(It.IsAny<Subscription>()), Times.Once);
                serivceMock.Verify(s => s.CreateAppSubscription(It.IsAny<Subscription>()), Times.Never);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }

            /// <summary>
            /// Post valid subscription for an external event. 
            /// Expected result:
            /// Service for creating app subscription is called.
            /// Success criteria:
            /// The expected method in the service is called.
            /// </summary>
            [Fact]
            public async Task Post_GivenSubscriptionForAppEvent_RightServiceMethodIsCalled()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                SubscriptionRequestModel cloudEventSubscription = GetEventsSubscriptionRequest("https://skd.apps.altinn.no/skd/flyttemelding", "https://www.skatteetaten.no/hook", alternativeSubjectFilter: "/organization/960474084");

                Mock<ISubscriptionService> serivceMock = new();
                serivceMock.Setup(s => s.CreateGenericSubscription(It.IsAny<Subscription>())).ReturnsAsync((new Subscription { Id = 1 }, null));
                serivceMock.Setup(s => s.CreateAppSubscription(It.IsAny<Subscription>())).ReturnsAsync((new Subscription { Id = 2 }, null));

                HttpClient client = GetTestClient(serivceMock.Object);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("skd", "950474084", "altinn:events.subscribe"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(cloudEventSubscription.Serialize(), Encoding.UTF8, "application/json")
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                serivceMock.Verify(s => s.CreateAppSubscription(It.IsAny<Subscription>()), Times.Once);
                serivceMock.Verify(s => s.CreateGenericSubscription(It.IsAny<Subscription>()), Times.Never);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }

            /// <summary>
            /// Gets a specific subscription
            /// Expected result:
            /// Returns HttpStatus ok
            /// Scenario: 
            /// </summary>
            [Fact]
            public async Task Get_GivenSubscriptionOrganizationWithValidSubject_ReturnsOk()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions/12";

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(null, "950474084"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri)
                {
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            /// <summary>
            /// Deletes a subscription that user is authorized for
            /// Expected result:
            /// Return httpStatus ok
            /// </summary>
            [Fact]
            public async Task Delete_GivenSubscriptionOrganizationWithValidSubject_ReturnsCreated()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions/16";
                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(null, "950474084"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri)
                {
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            /// <summary>
            /// Gets a specific subscription
            /// Expected result:
            /// Returns HttpStatus ok
            /// Scenario: 
            /// </summary>
            [Fact]
            public async Task Get_GivenSubscriptionOrganizationWithInvalidCreatedBy_ReturnsUnauthorizd()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions/12";

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(null, "897069652"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri)
                {
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            /// <summary>
            /// Deletes a subscription that user is authorized for
            /// Expected result:
            /// Return httpStatus ok
            /// </summary>
            [Fact]
            public async Task Delete_GivenSubscriptionOrganizationWithInvalidCreatedBy_ReturnsUnAuthorized()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions/16";
                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken(null, "897069652"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri)
                {
                };

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            [Fact]
            public async Task ValidateSubscription_ReturnsOk()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions/validate/16";
                HttpClient client = GetTestClient();

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, requestUri)
                {
                };

                httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("platform", "events"));

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            [Fact]
            public async Task Get_MissingAuthToken_ReturnsUnauthorized()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";
                HttpClient client = GetTestClient();

                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

                // Assert
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            [Fact]
            public async Task Get_AuthenticatedOrg_CallsServiceAndReturnsList()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string content = await response.Content.ReadAsStringAsync();
                SubscriptionList actual = JsonSerializer.Deserialize<SubscriptionList>(content, _jsonOptions);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(actual.Count > 0);
                Assert.DoesNotContain(actual.Subscriptions, s => s.Consumer != "/org/ttd");
            }

            [Fact]
            public async Task Get_AuthenticatedUser_CallsServiceAndReturnsList()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1337));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string content = await response.Content.ReadAsStringAsync();
                SubscriptionList actual = JsonSerializer.Deserialize<SubscriptionList>(content, _jsonOptions);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(actual.Count > 0);
                Assert.DoesNotContain(actual.Subscriptions, s => s.Consumer != "/user/1337");
            }

            [Fact]
            public async Task Get_AuthenticatedUser_NoSubscriptionsReturnsEmtpyList()
            {
                // Arrange
                string requestUri = $"{BasePath}/subscriptions";

                HttpClient client = GetTestClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetToken(1402));
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Act
                HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
                string content = await response.Content.ReadAsStringAsync();
                SubscriptionList actual = JsonSerializer.Deserialize<SubscriptionList>(content, _jsonOptions);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Empty(actual.Subscriptions);
                Assert.Equal(0, actual.Count);
            }

            private HttpClient GetTestClient(ISubscriptionService subscriptionServiceMock = null)
            {
                HttpClient client = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        if (subscriptionServiceMock != null)
                        {
                            services.AddSingleton(subscriptionServiceMock);
                        }

                        services.AddSingleton<IRegisterService, RegisterServiceMock>();
                        services.AddSingleton<IProfile, ProfileMockSI>();

                        services.AddSingleton<ICloudEventRepository, CloudEventRepositoryMock>();
                        services.AddSingleton<ISubscriptionRepository, SubscriptionRepositoryMock>();

                        services.AddSingleton<IEventsQueueClient, EventsQueueClientMock>();

                        // Set up mock authentication so that not well known endpoint is used
                        services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                        services.AddSingleton<ISigningKeysResolver, SigningKeyResolverMock>();
                        services.AddSingleton<IPDP, PepWithPDPAuthorizationMockSI>();
                    });
                }).CreateClient();

                return client;
            }

            private static SubscriptionRequestModel GetEventsSubscriptionRequest(string sourceFilter, string endpoint, string subjectFilter = null, string alternativeSubjectFilter = null)
            {
                Uri sourceFilterUri;

                try
                {
                    sourceFilterUri = new Uri(sourceFilter);
                }
                catch
                {
                    sourceFilterUri = new Uri(sourceFilter, UriKind.Relative);
                }

                SubscriptionRequestModel subscription = new SubscriptionRequestModel()
                {
                    EndPoint = new Uri(endpoint),
                    AlternativeSubjectFilter = alternativeSubjectFilter,
                    SubjectFilter = subjectFilter,
                    SourceFilter = sourceFilterUri
                };

                return subscription;
            }
        }
    }
}
