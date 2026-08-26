using Altinn.Platform.Events.Functions.Wolverine.Commands;
using Altinn.Platform.Events.Functions.Wolverine.Extensions;
using Altinn.Platform.Events.Functions.Wolverine.Handlers;
using Altinn.Platform.Events.Functions.Wolverine.Models;
using Altinn.Platform.Events.Functions.Wolverine.Services.Interfaces;

using CloudNative.CloudEvents;

using Moq;

using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.Wolverine.Handlers;

/// <summary>
/// A collection of tests related to <see cref="OutboundEventHandler"/>.
/// </summary>
public class OutboundEventHandlerTests
{
    [Fact]
    public async Task Handle_DeserializesPayloadAndSendsWebhook()
    {
        // Arrange
        CloudEventEnvelope envelope = new()
        {
            SubscriptionId = 1337,
            Consumer = "/org/ttd",
            Endpoint = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
            CloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = "cloud-event-id",
                Source = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test"),
                Type = "automated.test"
            }
        };

        string serializedPayload = envelope.Serialize();
        using CancellationTokenSource cts = new();

        CloudEventEnvelope actualEnvelope = null!;
        Mock<IWebhookService> webhookServiceMock = new();
        webhookServiceMock
            .Setup(w => w.Send(It.IsAny<CloudEventEnvelope>(), cts.Token))
            .Callback<CloudEventEnvelope, CancellationToken>((e, _) => actualEnvelope = e)
            .Returns(Task.CompletedTask);

        // Act
        await OutboundEventHandler.Handle(new OutboundEventCommand(serializedPayload), webhookServiceMock.Object, cts.Token);

        // Assert
        webhookServiceMock.Verify(w => w.Send(It.IsAny<CloudEventEnvelope>(), cts.Token), Times.Once);
        Assert.Equal(envelope.SubscriptionId, actualEnvelope.SubscriptionId);
        Assert.Equal(envelope.Consumer, actualEnvelope.Consumer);
        Assert.Equal(envelope.Endpoint, actualEnvelope.Endpoint);
        Assert.Equal(envelope.CloudEvent.Id, actualEnvelope.CloudEvent?.Id);
    }
}
