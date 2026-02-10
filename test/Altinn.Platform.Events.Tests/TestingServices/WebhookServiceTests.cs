using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Altinn.Platform.Events.Tests.Services;

public class WebhookServiceTests
{
    private readonly Mock<ILogger<WebhookService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly WebhookService _webhookService;

    public WebhookServiceTests()
    {
        _loggerMock = new Mock<ILogger<WebhookService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _webhookService = new WebhookService(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task SendAsync_SuccessfulRequest_LogsSuccessAndCompletes()
    {
        // Arrange
        var envelope = CreateTestEnvelope("https://example.com/webhook");
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri == envelope.Endpoint),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(expectedResponse);

        // Act
        await _webhookService.SendAsync(envelope);

        // Assert
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri == envelope.Endpoint),
            ItExpr.IsAny<CancellationToken>());

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sending webhook request")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Successfully sent webhook request")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_NullEnvelope_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _webhookService.SendAsync(null));

        Assert.Contains("Envelope or Endpoint cannot be null", exception.Message);
    }

    [Fact]
    public async Task SendAsync_NullEndpoint_ThrowsArgumentNullException()
    {
        // Arrange
        var envelope = new CloudEventEnvelope
        {
            CloudEvent = CreateTestCloudEvent(),
            Consumer = "/org/test",
            Endpoint = null,
            SubscriptionId = 1
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _webhookService.SendAsync(envelope));

        Assert.Contains("Envelope or Endpoint cannot be null", exception.Message);
    }

    [Fact]
    public async Task SendAsync_FailedRequest_ThrowsHttpRequestException()
    {
        // Arrange
        var envelope = CreateTestEnvelope("https://example.com/webhook");
        var errorMessage = "Service unavailable";
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(errorMessage)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(expectedResponse);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => _webhookService.SendAsync(envelope));

        Assert.Contains("ServiceUnavailable", exception.Message);
        Assert.Contains(errorMessage, exception.Message);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Webhook request failed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]    
    public async Task SendAsync_HttpRequestExceptionThrown_RethrowsAndLogs()
    {
        // Arrange
        var envelope = CreateTestEnvelope("https://example.com/webhook");
        var httpException = new HttpRequestException("Connection refused");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(httpException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => _webhookService.SendAsync(envelope));

        Assert.Equal("Connection refused", exception.Message);

        // HttpRequestException is caught by the specific catch block and rethrown immediately
        // without calling the generic error logger, so we should NOT expect error logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to send webhook request")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never);

        // Only the initial "Sending webhook request" log should be called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sending webhook request")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_UnexpectedException_ThrowsAndLogs()
    {
        // Arrange
        var envelope = CreateTestEnvelope("https://example.com/webhook");
        var exception = new InvalidOperationException("Unexpected error");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _webhookService.SendAsync(envelope));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to send webhook request")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_SlackEndpoint_SendsSlackFormattedPayload()
    {
        // Arrange
        var envelope = CreateTestEnvelope("https://hooks.slack.com/services/ABC123");
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage capturedRequest = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, token) => capturedRequest = req)
            .ReturnsAsync(expectedResponse);

        // Act
        await _webhookService.SendAsync(envelope);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest.Content.ReadAsStringAsync();
        
        // Slack payload should contain "text" property
        Assert.Contains("\"text\":", content);
        Assert.Contains("application/json", capturedRequest.Content.Headers.ContentType.MediaType);
    }

    [Fact]
    public async Task SendAsync_NonSlackEndpoint_SendsCloudEventDirectly()
    {
        // Arrange
        var envelope = CreateTestEnvelope("https://example.com/webhook");
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage capturedRequest = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, token) => capturedRequest = req)
            .ReturnsAsync(expectedResponse);

        // Act
        await _webhookService.SendAsync(envelope);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest.Content.ReadAsStringAsync();
        
        // Should NOT contain Slack wrapper
        Assert.DoesNotContain("\"text\":", content);

        // Should contain CloudEvent properties
        Assert.Contains("\"id\":", content);
        Assert.Contains("\"type\":", content);
        Assert.Contains("application/json", capturedRequest.Content.Headers.ContentType.MediaType);
    }

    [Fact]
    public async Task SendAsync_SlackEndpointUpperCase_SendsSlackFormattedPayload()
    {
        // Arrange
        var envelope = CreateTestEnvelope("https://HOOKS.SLACK.COM/services/ABC123");
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage capturedRequest = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, token) => capturedRequest = req)
            .ReturnsAsync(expectedResponse);

        // Act
        await _webhookService.SendAsync(envelope);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest.Content.ReadAsStringAsync();
        Assert.Contains("\"text\":", content);
    }

    [Fact]
    public async Task SendAsync_NullCloudEvent_SendsEmptyPayload()
    {
        // Arrange
        var envelope = new CloudEventEnvelope
        {
            CloudEvent = null,
            Consumer = "/org/test",
            Endpoint = new Uri("https://example.com/webhook"),
            SubscriptionId = 1
        };
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage capturedRequest = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, token) => capturedRequest = req)
            .ReturnsAsync(expectedResponse);

        // Act
        await _webhookService.SendAsync(envelope);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest.Content.ReadAsStringAsync();
        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public async Task SendAsync_SlackEndpointWithNullCloudEvent_SendsSlackFormatWithEmptyText()
    {
        // Arrange
        var envelope = new CloudEventEnvelope
        {
            CloudEvent = null,
            Consumer = "/org/test",
            Endpoint = new Uri("https://hooks.slack.com/services/ABC123"),
            SubscriptionId = 1
        };
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage capturedRequest = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, token) => capturedRequest = req)
            .ReturnsAsync(expectedResponse);

        // Act
        await _webhookService.SendAsync(envelope);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest.Content.ReadAsStringAsync();
        Assert.Contains("\"text\":\"\"", content);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task SendAsync_VariousFailureStatusCodes_ThrowsHttpRequestException(HttpStatusCode statusCode)
    {
        // Arrange
        var envelope = CreateTestEnvelope("https://example.com/webhook");
        var expectedResponse = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent($"Error {(int)statusCode}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(expectedResponse);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => _webhookService.SendAsync(envelope));

        Assert.Contains(statusCode.ToString(), exception.Message);
    }

    [Fact]
    public void Constructor_SetsHttpClientTimeout()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new Mock<ILogger<WebhookService>>();        

        // Act
        var service = new WebhookService(httpClient, logger.Object);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(300), httpClient.Timeout);
    }

    [Fact]
    public async Task SendAsync_LogsCorrectSubscriptionId()
    {
        // Arrange
        var subscriptionId = 42;
        var envelope = CreateTestEnvelope("https://example.com/webhook");
        envelope.SubscriptionId = subscriptionId;
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(expectedResponse);

        // Act
        await _webhookService.SendAsync(envelope);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"subscription {subscriptionId}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Exactly(2));
    }

    private static CloudEventEnvelope CreateTestEnvelope(string endpointUri)
    {
        return new CloudEventEnvelope
        {
            CloudEvent = CreateTestCloudEvent(),
            Consumer = "/org/test",
            Endpoint = new Uri(endpointUri),
            SubscriptionId = 1
        };
    }

    private static CloudEvent CreateTestCloudEvent()
    {
        return new CloudEvent(CloudEventsSpecVersion.V1_0)
        {
            Id = Guid.NewGuid().ToString(),
            Source = new Uri("urn:test:source"),
            Type = "test.event.type"
        };
    }
}
