using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Models;
using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Tests.Mocks
{
    public class EventsQueueClientMock : IEventsQueueClient
    {
        public EventsQueueClientMock()
        {
            OutboundQueue = new Dictionary<int, List<CloudEventEnvelope>>();
        }

        /// <summary>
        /// Queue mock for unit test
        /// </summary>
        public Dictionary<int, List<CloudEventEnvelope>> OutboundQueue { get; set; }

        public Task<QueuePostReceipt> EnqueueRegistration(string content)
        {
            return Task.FromResult(new QueuePostReceipt { Success = true });
        }

        public Task<QueuePostReceipt> EnqueueInbound(string content)
        {
            return Task.FromResult(new QueuePostReceipt { Success = true });
        }

        public Task<QueuePostReceipt> EnqueueOutbound(string content)
        {
            CloudEventEnvelope cloudEventEnvelope = JsonSerializer.Deserialize<CloudEventEnvelope>(content);

            var hash = cloudEventEnvelope.CloudEvent.GetHashCode();
            if (!OutboundQueue.ContainsKey(hash))
            {
                OutboundQueue.Add(hash, new List<CloudEventEnvelope>());
            }

            OutboundQueue[hash].Add(cloudEventEnvelope);

            return Task.FromResult(new QueuePostReceipt { Success = true });
        }

        public Task<QueuePostReceipt> EnqueueSubscriptionValidation(string content)
        {
            return Task.FromResult(new QueuePostReceipt { Success = true });
        }

        public Task<QueuePostReceipt> EnqueueRegistration(CloudEvent cloudEvent)
        {
            return Task.FromResult(new QueuePostReceipt { Success = true });
        }
    }
}
