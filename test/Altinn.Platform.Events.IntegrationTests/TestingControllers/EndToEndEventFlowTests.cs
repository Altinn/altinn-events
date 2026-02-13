#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.IntegrationTests.Data;
using Altinn.Platform.Events.IntegrationTests.Infrastructure;
using Altinn.Platform.Events.IntegrationTests.Utils;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using CloudNative.CloudEvents;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.IntegrationTests.TestingControllers;

/// <summary>
/// End-to-end integration tests that exercise the full HTTP -> queue -> webhook pipeline.
/// Uses authenticated HTTP clients to call controller endpoints and verifies the entire flow.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class EndToEndEventFlowTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the complete event delivery flow:
    /// 1. Create a subscription via HTTP
    /// 2. Wait for subscription validation webhook
    /// 3. Post an event via HTTP
    /// 4. Verify the event is delivered to the subscriber's webhook
    /// </summary>
    [Fact]
    public async Task PostEvent_WithMatchingSubscription_EventDeliveredToWebhook()
    {
        // Arrange - Mock webhook to capture all calls
        var webhookCalls = new ConcurrentBag<CloudEventEnvelope>();
        var webhookMock = new Mock<IWebhookService>();
        webhookMock.Setup(w => w.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<CloudEventEnvelope, CancellationToken>((e, _) => webhookCalls.Add(e))
            .Returns(Task.CompletedTask);

        // Mock authorization to allow everything
        var authMock = new Mock<IAuthorization>();
        authMock.Setup(a => a.AuthorizePublishEvent(It.IsAny<CloudEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        authMock.Setup(a => a.AuthorizeConsumerForEventsSubscription(It.IsAny<Subscription>()))
            .ReturnsAsync(true);
        authMock.Setup(a => a.AuthorizeMultipleConsumersForAltinnAppEvent(
                It.IsAny<CloudEvent>(), It.IsAny<List<string>>()))
            .ReturnsAsync((CloudEvent _, List<string> consumers) =>
                consumers.ToDictionary(c => c, _ => true));
        authMock.Setup(a => a.AuthorizeMultipleConsumersForGenericEvent(
                It.IsAny<CloudEvent>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CloudEvent _, List<string> consumers, CancellationToken _) =>
                consumers.ToDictionary(c => c, _ => true));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => webhookMock.Object)
            .ReplaceService(_ => authMock.Object)
            .Initialize();

        await using (factory)
        {
            // Step 1: Create subscription via HTTP
            var subscribeClient = factory.CreateAuthenticatedClient("digdir", "altinn:events.subscribe");

            var subscriptionRequest = new SubscriptionRequestModel
            {
                EndPoint = new Uri("https://test-subscriber.example.com/webhook"),
                ResourceFilter = "urn:altinn:resource:app_ttd_test-app",
                TypeFilter = "app.instance.created"
            };

            var createResponse = await subscribeClient.PostAsync(
                "/events/api/v1/subscriptions",
                new StringContent(subscriptionRequest.Serialize(), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            // Step 2: Wait for subscription validation webhook
            var validationReceived = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(webhookCalls.Any(c =>
                    c.CloudEvent.Type == "platform.events.validatesubscription")),
                maxAttempts: 30,
                delayMs: 500);
            Assert.True(validationReceived, "Subscription validation webhook should be received");

            // Step 3: Post event via HTTP
            var publishClient = factory.CreateAuthenticatedClient("digdir", "altinn:events.publish");
            var cloudEvent = CloudEventTestData.CreateTestCloudEvent();

            var postResponse = await publishClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, "/events/api/v1/events")
                {
                    Content = new StringContent(
                        cloudEvent.Serialize(), Encoding.UTF8, "application/cloudevents+json")
                });
            Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

            // Step 4: Wait for event delivery to webhook
            var eventDelivered = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(webhookCalls.Any(c =>
                    c.CloudEvent.Type == "app.instance.created")),
                maxAttempts: 30,
                delayMs: 500);
            Assert.True(eventDelivered, "Event should be delivered to webhook after processing through all queues");

            // Step 5: Verify delivered event content
            var deliveredEnvelope = webhookCalls.First(c =>
                c.CloudEvent.Type == "app.instance.created");
            Assert.Equal(cloudEvent.Id, deliveredEnvelope.CloudEvent.Id);
            Assert.Equal(cloudEvent.Source, deliveredEnvelope.CloudEvent.Source);
            Assert.Equal("/org/digdir", deliveredEnvelope.Consumer);
        }
    }
}
