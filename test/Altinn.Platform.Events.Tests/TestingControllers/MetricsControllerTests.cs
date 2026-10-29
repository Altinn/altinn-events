using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Interfaces;
using Altinn.Platform.Events.Controllers;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.Tests.Mocks.Authentication;
using Altinn.Platform.Events.Tests.Utils;
using Altinn.Platform.Events.UnitTest.Mocks;
using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Tests.TestingControllers;

/// <summary>
/// Reperesents a collection of integration tests.
/// </summary>
public partial class IntegrationTests
{
    /// <summary>
    /// Test class for MetricsController
    /// </summary>
    public class MetricsControllerTests : IClassFixture<WebApplicationFactory<MetricsController>>
    {
        private const string _requestUri = "/events/api/v1/metrics/getmetrics";

        private readonly WebApplicationFactory<MetricsController> _factory;

        public MetricsControllerTests(WebApplicationFactory<MetricsController> factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Scenario:
        ///   Post to the trigger endpoint with a valid platform access token.
        /// Expected result:
        ///   Returns OK and the metrics service is triggered.
        /// </summary>
        [Fact]
        public async Task TriggerGetMetrics_ValidPlatformAccessToken_ReturnsOkAndTriggersService()
        {
            // Arrange
            Mock<IMetricsService> metricsService = new();
            HttpClient client = GetTestClient(metricsService.Object);
            HttpRequestMessage request = new(HttpMethod.Post, _requestUri);
            request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "endring-av-navn-v2"));

            // Act
            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            metricsService.Verify(s => s.TriggerGetMetrics(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Scenario:
        ///   Post to the trigger endpoint without a platform access token.
        /// Expected result:
        ///   Returns Unauthorized and the metrics service is not triggered.
        /// </summary>
        [Fact]
        public async Task TriggerGetMetrics_NoPlatformAccessToken_ReturnsUnauthorized()
        {
            // Arrange
            Mock<IMetricsService> metricsService = new();
            HttpClient client = GetTestClient(metricsService.Object);
            HttpRequestMessage request = new(HttpMethod.Post, _requestUri);

            // Act
            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            metricsService.Verify(s => s.TriggerGetMetrics(It.IsAny<CancellationToken>()), Times.Never);
        }

        private HttpClient GetTestClient(IMetricsService metricsService)
        {
            return _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddConfiguration(new ConfigurationBuilder().AddJsonFile("appsettings.unittest.json").Build());
                });

                builder.UseSetting("WolverineSettings:EnableRegistrationPublisher", "false");
                builder.UseSetting("WolverineSettings:EnableValidationPublisher", "false");
                builder.UseSetting("WolverineSettings:EnableRegistrationListener", "false");
                builder.UseSetting("WolverineSettings:EnableInboundListener", "false");
                builder.UseSetting("WolverineSettings:EnableOutboundListener", "false");
                builder.UseSetting("WolverineSettings:EnableValidationListener", "false");

                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(metricsService);

                    // Set up mock authentication so that not well known endpoint is used
                    services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                    services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                    services.AddSingleton<IPDP, PepWithPDPAuthorizationMockSI>();
                });
            }).CreateClient();
        }
    }
}
