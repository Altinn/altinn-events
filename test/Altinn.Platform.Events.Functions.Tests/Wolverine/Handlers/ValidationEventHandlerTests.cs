using Altinn.Platform.Events.Functions.Clients.Interfaces;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Wolverine.Commands;
using Altinn.Platform.Events.Functions.Wolverine.Handlers;
using Altinn.Platform.Events.Functions.Wolverine.Models;
using Altinn.Platform.Events.Functions.Wolverine.Services.Interfaces;
using Altinn.Platform.Events.Models;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.Wolverine.Handlers;

/// <summary>
/// A collection of tests related to <see cref="ValidationEventHandler"/>.
/// </summary>
public class ValidationEventHandlerTests
{
    private readonly IOptions<PlatformSettings> _platformSettings =
        Options.Create(new PlatformSettings { ApiEventsEndpoint = "https://at22.altinn.cloud/events/api/v1/" });

    [Fact]
    public async Task Handle_SendsWebhookThenValidatesSubscription()
    {
        // Arrange
        Subscription subscription = new()
        {
            Id = 1337,
            Consumer = "/org/ttd",
            EndPoint = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
        };

        var callOrder = new List<string>();

        Mock<IWebhookService> webhookServiceMock = new();
        webhookServiceMock
            .Setup(w => w.Send(It.IsAny<CloudEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("Send"))
            .Returns(Task.CompletedTask);

        Mock<IEventsClient> eventsClientMock = new();
        eventsClientMock
            .Setup(c => c.ValidateSubscription(subscription.Id))
            .Callback(() => callOrder.Add("ValidateSubscription"))
            .Returns(Task.CompletedTask);

        // Act
        await ValidationEventHandler.Handle(
            new ValidateSubscriptionCommand(subscription),
            _platformSettings,
            webhookServiceMock.Object,
            eventsClientMock.Object,
            CancellationToken.None);

        // Assert
        webhookServiceMock.Verify(w => w.Send(It.Is<CloudEventEnvelope>(e => e.SubscriptionId == subscription.Id && e.Consumer == subscription.Consumer), It.IsAny<CancellationToken>()), Times.Once);
        eventsClientMock.Verify(c => c.ValidateSubscription(subscription.Id), Times.Once);
        Assert.Equal(["Send", "ValidateSubscription"], callOrder);
    }

    [Fact]
    public void CreateValidateEvent_BuildsEnvelopeFromSubscription()
    {
        // Arrange
        Subscription subscription = new()
        {
            Id = 42,
            Consumer = "/org/ttd",
            EndPoint = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test")
        };

        // Act
        CloudEventEnvelope envelope = ValidationEventHandler.CreateValidateEvent(subscription, _platformSettings.Value);

        // Assert
        Assert.Equal(subscription.Id, envelope.SubscriptionId);
        Assert.Equal(subscription.Consumer, envelope.Consumer);
        Assert.Equal(subscription.EndPoint, envelope.Endpoint);
        Assert.Equal("platform.events.validatesubscription", envelope.CloudEvent?.Type);
        Assert.Equal(new Uri("https://at22.altinn.cloud/events/api/v1/subscriptions/42"), envelope.CloudEvent?.Source);
    }
}
