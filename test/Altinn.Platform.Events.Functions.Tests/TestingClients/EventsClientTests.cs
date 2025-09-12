using System.Net;
using System.Security.Cryptography.X509Certificates;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Functions.Clients;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingClients
{
    public class EventsClientTests
    {
        private readonly Mock<ILogger<EventsClient>> _loggerMock = new Mock<ILogger<EventsClient>>();
        private readonly Mock<IAccessTokenGenerator> _atgMock = new Mock<IAccessTokenGenerator>();
        private readonly Mock<ICertificateResolverService> _srMock = new Mock<ICertificateResolverService>();

        private CloudEvent _cloudEvent = new(CloudEventsSpecVersion.V1_0)
        {
            Id = "1337",
            Source = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
            Type = "automated.test"
        };

        private IOptions<PlatformSettings> _platformSettings = Options.Create(new PlatformSettings
        {
            ApiEventsEndpoint = "https://platform.test.altinn.cloud/events/api/v1/"
        });

        private IOptions<KeyVaultSettings> _kvSettings = Options.Create(new KeyVaultSettings
        {
            KeyVaultURI = "https://vg.no",
            PlatformCertSecretId = "secretId"
        });

        public EventsClientTests()
        {
            _atgMock.Setup(atg => atg.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<X509Certificate2>())).Returns(string.Empty);
        }

        /// <summary>
        /// Verify that the endpoint the client sends a request to is set correctly
        /// </summary>
        [Fact]
        public async Task SaveCloudEvent_SuccessResponse()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/events/api/v1/storage/events",
                HttpStatusCode.OK);

            var sut = new EventsClient(
                new HttpClient(handlerMock.Object),
                _atgMock.Object,
                _srMock.Object,
                _platformSettings,
                _loggerMock.Object);

            // Act
            await sut.SaveCloudEvent(_cloudEvent);

            // Assert
            handlerMock.VerifyAll();
        }

        [Theory]
        [InlineData("resource")]
        [InlineData(null)]
        public async Task PostLogEntry_SuccessResponse(string resource)
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/events/api/v1/storage/events/logs",
                HttpStatusCode.OK);

            var sut = new EventsClient(
                new HttpClient(handlerMock.Object),
                _atgMock.Object,
                _srMock.Object,
                _platformSettings,
                _loggerMock.Object);

            _cloudEvent["resource"] = resource;

            var cloudEnvelope = new CloudEventEnvelope
            {
                CloudEvent = _cloudEvent,
                SubscriptionId = 1337,
                Endpoint = new Uri("https://localhost:5000")
            };

            // Act
            await sut.LogWebhookHttpStatusCode(cloudEnvelope, HttpStatusCode.Created, isSuccessStatusCode: true);

            // Assert
            handlerMock.VerifyAll();
        }

        [Fact]
        public async Task SaveCloudEvent_NonSuccessResponse_ErrorLoggedAndExceptionThrown()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/events/api/v1/storage/events",
                HttpStatusCode.ServiceUnavailable);

            var sut = CreateTestInstance(handlerMock.Object);

            // Act
            await Assert.ThrowsAsync<HttpRequestException>(async () => await sut.SaveCloudEvent(_cloudEvent));

            // Assert
            handlerMock.VerifyAll();
            _loggerMock.Verify(
                x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SaveCloudEvent with id 1337 failed with status code ServiceUnavailable")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }

        /// <summary>
        /// Verify that the endpoint the client sends a request to is set correctly
        /// </summary>
        [Fact]
        public async Task PostInbound_SuccessResponse()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/events/api/v1/inbound",
                HttpStatusCode.OK);

            var sut = CreateTestInstance(handlerMock.Object);

            // Act
            await sut.PostInbound(_cloudEvent);

            // Assert
            handlerMock.VerifyAll();
        }

        [Fact]
        public async Task PostInbound_NonSuccessResponse_ErrorLoggedAndExceptionThrown()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/events/api/v1/inbound",
                HttpStatusCode.ServiceUnavailable);

            var sut = CreateTestInstance(handlerMock.Object);

            // Act
            await Assert.ThrowsAsync<HttpRequestException>(async () => await sut.PostInbound(_cloudEvent));

            // Assert
            handlerMock.VerifyAll();
            _loggerMock.Verify(
                x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("PostInbound event with id 1337 failed with status code ServiceUnavailable")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }

        /// <summary>
        /// Verify that the endpoint the client sends a request to is set correctly
        /// </summary>
        [Fact]
        public async Task PostOutbound_SuccessResponse()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/events/api/v1/outbound",
                HttpStatusCode.OK);

            var sut = CreateTestInstance(handlerMock.Object);

            // Act
            await sut.PostOutbound(_cloudEvent);

            // Assert
            handlerMock.VerifyAll();
        }

        [Fact]
        public async Task PostOutbound_NonSuccessResponse_ErrorLoggedAndExceptionThrown()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/events/api/v1/outbound",
                HttpStatusCode.ServiceUnavailable);

            var sut = CreateTestInstance(handlerMock.Object);

            // Act
            await Assert.ThrowsAsync<HttpRequestException>(async () => await sut.PostOutbound(_cloudEvent));

            // Assert
            handlerMock.VerifyAll();
            _loggerMock.Verify(
                x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("PostOutbound event with id 1337 failed with status code ServiceUnavailable")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }

        /// <summary>
        /// Verify that the endpoint the client sends a request to is set correctly
        /// </summary>
        [Fact]
        public async Task ValidateSubscription_SuccessResponse()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/events/api/v1/subscriptions/validate/1337",
                HttpStatusCode.OK);

            var sut = CreateTestInstance(handlerMock.Object);

            // Act
            await sut.ValidateSubscription(1337);

            // Assert
            handlerMock.VerifyAll();
        }

        [Fact]
        public async Task ValidateSubscription_NonSuccessResponse_ErrorLoggedAndExceptionThrown()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/events/api/v1/subscriptions/validate/1337",
                HttpStatusCode.ServiceUnavailable);

            var sut = CreateTestInstance(handlerMock.Object);

            // Act
            await Assert.ThrowsAsync<HttpRequestException>(async () => await sut.ValidateSubscription(1337));

            // Assert
            handlerMock.VerifyAll();
            _loggerMock.Verify(
                x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Validate subscription with id 1337 failed with status code ServiceUnavailable")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }

        [Fact]
        public async Task ValidateSubscription_NotFoundResponse_ErrorLoggedNoExceptionThrown()
        {
            // Arrange
            var handlerMock = CreateMessageHandlerMock(
                "https://platform.test.altinn.cloud/events/api/v1/subscriptions/validate/1337",
                HttpStatusCode.NotFound);

            var sut = CreateTestInstance(handlerMock.Object);

            // Act
            await sut.ValidateSubscription(1337);

            // Assert
            handlerMock.VerifyAll();
            _loggerMock.Verify(
                x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Attempting to validate non existing subscription 1337")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }

        private static Mock<HttpMessageHandler> CreateMessageHandlerMock(string clientEndpoint, HttpStatusCode statusCode)
        {
            var messageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            messageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(rm => rm.RequestUri.Equals(clientEndpoint)), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    var response = new HttpResponseMessage(statusCode);
                    return response;
                })
                .Verifiable();

            return messageHandlerMock;
        }

        private EventsClient CreateTestInstance(HttpMessageHandler messageHandlerMock)
        {
            return new EventsClient(
                  new HttpClient(messageHandlerMock),
                  _atgMock.Object,
                  _srMock.Object,
                  _platformSettings,
                  _loggerMock.Object);
        }
    }
}
