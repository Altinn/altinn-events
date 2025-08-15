using Altinn.Platform.Events.Functions.Extensions;
using Altinn.Platform.Events.Functions.Models;
using Altinn.Platform.Events.Functions.Queues;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Altinn.Platform.Events.IsolatedFunctions.Extensions;
using Altinn.Platform.Events.IsolatedFunctions.Models;
using Altinn.Platform.Events.IsolatedFunctions.Services;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace Altinn.Platform.Events.IsolatedFunctions.Tests.TestingFunctions;

public class EventsOutboundTests
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly RetryBackoffService _retryBackoffService;
    private const string _serializedCloudEvent = "{" +
            "\"id\":\"f276d3da-9b72-492b-9fee-9cf71e2826a2\"," +
            "\"source\":\"https://ttd.apps.at23.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319\"," +
            "\"specversion\":\"1.0\"," +
            "\"type\":\"app.instance.created\"," +
            "\"subject\":\"/party/50012356\"," +
            "\"time\":\"2023-01-13T09:47:41.1680188Z\"," +
            "\"alternativesubject\":\"/person/16035001577\"" +
            "}";

    public EventsOutboundTests()
    {
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        QueueSendDelegate mainDelegate = (msg, vis, ttl, ct) =>
        {
            return Task.CompletedTask;
        };

        PoisonQueueSendDelegate poisonDelegate = (msg, vis, ttl, ct) =>
        {
            return Task.CompletedTask;
        };

        _retryBackoffService = new RetryBackoffService(
            NullLogger<RetryBackoffService>.Instance,
            mainDelegate,
            poisonDelegate);
    }

    [Fact]
    public async Task Run_ConfirmDeserializationOCloudEventEnvelope_CloudEventPersisted()
    {
        // Arrange
        CloudEventEnvelope actualServiceInput = null;

        string serializedCloudEnvelope = "{" +
            "\"Pushed\": \"2023-01-17T16:09:10.9090958+00:00\"," +
            "  \"Endpoint\": \"https://hooks.slack.com/services/weebhook-endpoint\"," +
            "  \"Consumer\": \"/org/ttd\"," +
            "  \"SubscriptionId\": 427," +
            "  \"CloudEvent\": {" +
                " \"specversion\": \"1.0\"," +
                " \"id\": \"42849881-3659-4ff4-9ee1-c577646ea44b\"," +
                " \"source\": \"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/7806177e-5594-431b-8240-f173d92ed84d\"," +
                " \"type\": \"app.instance.created\"," +
                " \"subject\": \"/party/50002108\"," +
                " \"time\": \"2023-01-17T16:09:07.3146561Z\"," +
                " \"alternativesubject\": \"/person/01014922047\"}}";

        Mock<IWebhookService> webhookServiceMock = new();
        webhookServiceMock.Setup(wh => wh.Send(It.Is<CloudEventEnvelope>(cee => cee.CloudEvent != null)))
            .Callback<CloudEventEnvelope>(cee => actualServiceInput = cee)
            .Returns(Task.CompletedTask);

        var sut = new EventsOutbound(webhookServiceMock.Object, _retryBackoffService);

        // Act
        await sut.Run(serializedCloudEnvelope);

        // Assert
        webhookServiceMock.VerifyAll();
        Assert.Equal(CloudEventsSpecVersion.V1_0, actualServiceInput.CloudEvent.SpecVersion);
        Assert.Equal("https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/7806177e-5594-431b-8240-f173d92ed84d", actualServiceInput.CloudEvent.Source.ToString());
        Assert.Equal(427, actualServiceInput.SubscriptionId);
    }

    [Fact]
    public async Task Run_WithRetryableEventWrapper_ProcessesCloudEventSuccessfully()
    {
        // Arrange
        string serializedCloudEvent = "{" +
             "\"id\":\"f276d3da-9b72-492b-9fee-9cf71e2826a2\"," +
             "\"source\":\"https://ttd.apps.at23.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319\"," +
             "\"specversion\":\"1.0\"," +
             "\"type\":\"app.instance.created\"," +
             "\"subject\":\"/party/50012356\"," +
             "\"time\":\"2023-01-13T09:47:41.1680188Z\"," +
             "\"alternativesubject\":\"/person/16035001577\"" +
             "}";

        // Create the wrapper envelope
        var envelope = new CloudEventEnvelope
        {
            Pushed = DateTime.Parse("2023-01-17T16:09:10.9090958+00:00"),
            Endpoint = new Uri("https://hooks.slack.com/services/weebhook-endpoint"),
            Consumer = "/org/ttd",
            SubscriptionId = 427,
            CloudEvent = serializedCloudEvent.DeserializeToCloudEvent()
        };

        // Create RetryableEventWrapper
        var retryableEventWrapper = new RetryableEventWrapper
        {
            Payload = envelope.Serialize(),
            DequeueCount = 1,
            FirstProcessedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Serialize with custom options
        string serializedWrapper = JsonSerializer.Serialize(retryableEventWrapper, _jsonSerializerOptions);

        Mock<IWebhookService> webhookServiceMock = new();

        var sut = new EventsOutbound(webhookServiceMock.Object, _retryBackoffService);

        // Act
        await sut.Run(serializedWrapper);

        // Assert
        webhookServiceMock.Verify(x => x.Send(It.IsAny<CloudEventEnvelope>()), Times.Once);
    }

    [Fact]
    public async Task Run_WithWebhookFailure_RequeuesWithDequeueCount()
    {
        // Arrange
        var envelope = new CloudEventEnvelope
        {
            Pushed = DateTime.Parse("2023-01-17T16:09:10.9090958+00:00"),
            Endpoint = new Uri("https://hooks.slack.com/services/weebhook-endpoint"),
            Consumer = "/org/ttd",
            SubscriptionId = 427,
            CloudEvent = _serializedCloudEvent.DeserializeToCloudEvent()
        };

        // Create RetryableEventWrapper
        var retryableEventWrapper = new RetryableEventWrapper
        {
            Payload = envelope.Serialize(),
            DequeueCount = 0,
            FirstProcessedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Capture what's requeued
        RetryableEventWrapper? requeuedWrapper = null;
        Exception? caughtException = null;

        // Set up webhook service mock to throw exception
        Mock<IWebhookService> webhookServiceMock = new();
        webhookServiceMock
            .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>()))
            .ThrowsAsync(new InvalidOperationException("Webhook service failed"));

        // Set up retry service mock to capture requeued message
        var retryServiceMock = new Mock<IRetryBackoffService>();

        retryServiceMock
            .Setup(r => r.RequeueWithBackoff(It.IsAny<RetryableEventWrapper>(), It.IsAny<Exception>()))
            .Callback<RetryableEventWrapper, Exception>((wrapper, ex) =>
            {
                requeuedWrapper = wrapper;
                caughtException = ex;
            })
            .Returns(Task.CompletedTask);

        // Create the SUT
        var sut = new EventsOutbound(
            webhookServiceMock.Object,
            retryServiceMock.Object);

        // Serialize wrapper
        string serializedWrapper = JsonSerializer.Serialize(retryableEventWrapper, _jsonSerializerOptions);

        // Act
        await sut.Run(serializedWrapper);

        // Assert
        retryServiceMock.Verify(
            r => r.RequeueWithBackoff(
                It.IsAny<RetryableEventWrapper>(),
                It.IsAny<Exception>()),
            Times.Once);

        Assert.NotNull(requeuedWrapper);
        Assert.Equal(0, requeuedWrapper.DequeueCount); // Verify count is still 0
        Assert.IsType<InvalidOperationException>(caughtException);
        Assert.Equal("Webhook service failed", caughtException.Message);

        // Verify the payload is preserved
        CloudEventEnvelope deserializedEnvelope = requeuedWrapper.ExtractCloudEventEnvelope();
        Assert.Equal(427, deserializedEnvelope.SubscriptionId);
        Assert.Equal("/org/ttd", deserializedEnvelope.Consumer);
    }

    [Fact]
    public async Task Run_WithDirectCloudEventEnvelope_ProcessesCloudEventSuccessfully()
    {
        // Arrange
        string serializedCloudEvent = "{" +
             "\"id\":\"f276d3da-9b72-492b-9fee-9cf71e2826a2\"," +
             "\"source\":\"https://ttd.apps.at23.altinn.cloud/ttd/apps-test/instances/50012356/8f66119a-39eb-49ea-a34e-6b99ec6af319\"," +
             "\"specversion\":\"1.0\"," +
             "\"type\":\"app.instance.created\"," +
             "\"subject\":\"/party/50012356\"," +
             "\"time\":\"2023-01-13T09:47:41.1680188Z\"," +
             "\"alternativesubject\":\"/person/16035001577\"" +
             "}";

        // Create CloudEventEnvelope directly without using RetryableEventWrapper
        var envelope = new CloudEventEnvelope
        {
            Pushed = DateTime.Parse("2023-01-17T16:09:10.9090958+00:00"),
            Endpoint = new Uri("https://hooks.slack.com/services/weebhook-endpoint"),
            Consumer = "/org/ttd",
            SubscriptionId = 427,
            CloudEvent = serializedCloudEvent.DeserializeToCloudEvent()
        };

        // Serialize the envelope directly
        string serializedEnvelope = envelope.Serialize();

        Mock<IWebhookService> webhookServiceMock = new();
        webhookServiceMock
          .Setup(s => s.Send(It.IsAny<CloudEventEnvelope>()))
          .ThrowsAsync(new InvalidOperationException("Webhook service failed"));

        var sut = new EventsOutbound(webhookServiceMock.Object, _retryBackoffService);

        // Act
        await sut.Run(serializedEnvelope);

        // Assert
        webhookServiceMock.Verify(x => x.Send(It.IsAny<CloudEventEnvelope>()), Times.Once);
    }
}


