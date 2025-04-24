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
        private readonly Mock<IKeyVaultService> _kvMock = new Mock<IKeyVaultService>();
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
            string base64 = "MIID/zCCAuegAwIBAgIQF2ov3ZZUmJVKtoz0a1fabDANBgkqhkiG9w0BAQsFADB/\r\nMRMwEQYKCZImiZPyLGQBGRYDY29tMRcwFQYKCZImiZPyLGQBGRYHY29udG9zbzEU\r\nMBIGCgmSJomT8ixkARkWBGNvcnAxFTATBgNVBAsMDFVzZXJBY2NvdW50czEiMCAG\r\nA1UEAwwZQWx0aW5uIFBsYXRmb3JtIFVuaXQgdGVzdDAgFw0yMDA0MTQwOTMwMTda\r\nGA8yMTIwMDQxNDA5NDAxOFowfzETMBEGCgmSJomT8ixkARkWA2NvbTEXMBUGCgmS\r\nJomT8ixkARkWB2NvbnRvc28xFDASBgoJkiaJk/IsZAEZFgRjb3JwMRUwEwYDVQQL\r\nDAxVc2VyQWNjb3VudHMxIjAgBgNVBAMMGUFsdGlubiBQbGF0Zm9ybSBVbml0IHRl\r\nc3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDCAKc+q5jbYFyQFxM1\r\nxU3v0N477ppnMu03K8qlEkX0+yffRHcR1I0Kku8yg1S+LQjeqh1K42b270myKiIt\r\nvxeuNnanRwdehTZthThembr8RXoGcmzaXfMet7NVDgUa7gNzPXbqjhTFdyWoZzeU\r\nX6TWTgFtciTs5M1F50H+3nieGKX2dvLUIEXWFO7yevj9bqtI8k0b66eLgBjchnjW\r\n8B7oYOFZW44VDDnqQrvFJ9aMQ44FfLAWWLcy6nBzcDdK+Z+yq9FNVgduyl0J7vRo\r\n3UtcVazLUvmDdwASLIB3IwB7YmT6fuOyM+6eyw5F1CdjXbc/bhop0pCDY1aAEsZA\r\nCjT9AgMBAAGjdTBzMA4GA1UdDwEB/wQEAwIHgDATBgNVHSUEDDAKBggrBgEFBQcD\r\nAjAtBgNVHREEJjAkoCIGCisGAQQBgjcUAgOgFAwSdGVzdEBhbHRpbm4uc3R1ZGlv\r\nMB0GA1UdDgQWBBTv8Cpf5J7nfmGds20LU/J3bg05XTANBgkqhkiG9w0BAQsFAAOC\r\nAQEAahWeu6ymaiJe9+LiMlQwNsUIV4KaLX+jCsRyF1jUJ0C13aFALGM4k9svqqXR\r\nDzBdCXXr0c1E+Ks3sCwBLfK5yj5fTI+pL26ceEmHahcVyLvzEBljtNb4FnGFs92P\r\nCH0NuCz45hQ2O9/Tv4cZAdgledTznJTKzzQNaF8M6iINmP6sf4kOg0BQx0K71K4f\r\n7j2oQvYKiT7Zv1e83cdk9pS4ihDe+ZWYiGUM/IuaXNPl6OzVk4rY88PZJAoz7q33\r\nrYjlT+zkcl3dzTc3E0CWzbIWjhaXCRWvlI44cLRtdpmPqJUHI6a/tcGwNb5vWiT4\r\nYfZJ0EZ2iSRQlpU3+jMs8Ci2AA==";
            _kvMock.Setup(kv => kv.GetCertificateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(base64);
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
