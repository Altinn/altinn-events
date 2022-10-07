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

        public Task<PushQueueReceipt> PushToOutboundQueue(string content)
        {
            CloudEventEnvelope cloudEventEnvelope = JsonSerializer.Deserialize<CloudEventEnvelope>(content);

            if (!OutboundQueue.ContainsKey(cloudEventEnvelope.CloudEvent.Id))
            {
                OutboundQueue.Add(cloudEventEnvelope.CloudEvent.Id, new List<CloudEventEnvelope>());
            }

            OutboundQueue[cloudEventEnvelope.CloudEvent.Id].Add(cloudEventEnvelope);

            return Task.FromResult(new PushQueueReceipt { Success = true });
        }

        public Task<PushQueueReceipt> PushToInboundQueue(string content)
        {
            return Task.FromResult(new PushQueueReceipt { Success = true });
        }

        public Task<PushQueueReceipt> PushToValidationQueue(string content)
        {
            return Task.FromResult(new PushQueueReceipt { Success = true });
        }
    }
}
