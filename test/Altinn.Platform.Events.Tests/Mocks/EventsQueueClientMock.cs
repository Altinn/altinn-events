using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Tests.Mocks
{
    public class EventsQueueClientMock : IEventsQueueClient
    {
        public EventsQueueClientMock()
        {
            OutboundQueue = new Dictionary<string, List<CloudEventEnvelope>>();
        }

        /// <summary>
        /// Queue mock for unit test
        /// </summary>
        public Dictionary<string, List<CloudEventEnvelope>> OutboundQueue { get; set; }

        public Task<QueuePostReceipt> EnqueueOutbound(string content)
        {
            CloudEventEnvelope cloudEventEnvelope = JsonSerializer.Deserialize<CloudEventEnvelope>(content);

            if (!OutboundQueue.ContainsKey(cloudEventEnvelope.CloudEvent.Id))
            {
                OutboundQueue.Add(cloudEventEnvelope.CloudEvent.Id, new List<CloudEventEnvelope>());
            }

            OutboundQueue[cloudEventEnvelope.CloudEvent.Id].Add(cloudEventEnvelope);

            return Task.FromResult(new QueuePostReceipt { Success = true });
        }

        public Task<QueuePostReceipt> EnqueueInbound(string content)
        {
            return Task.FromResult(new QueuePostReceipt { Success = true });
        }

        public Task<QueuePostReceipt> EnqueueSubscriptionValidation(string content)
        {
            return Task.FromResult(new QueuePostReceipt { Success = true });
        }
    }
}
