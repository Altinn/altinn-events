using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Clients
{
    /// <summary>
    /// The queue service that handles actions related to the queue storage.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class EventsQueueClient : IEventsQueueClient
    {
        private readonly QueueStorageSettings _settings;

        private Azure.Storage.Queues.QueueClient _inboundQueueClient;

        private Azure.Storage.Queues.QueueClient _outboundQueueClient;

        private Azure.Storage.Queues.QueueClient _validationQueueClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsQueueClient"/> class.
        /// </summary>
        /// <param name="settings">The queue storage settings</param>
        public EventsQueueClient(IOptions<QueueStorageSettings> settings)
        {
            _settings = settings.Value;
        }

        /// <inheritdoc/>
        public async Task<PushQueueReceipt> PushToInboundQueue(string content)
        {
            try
            {
                Azure.Storage.Queues.QueueClient client = await GetInboundQueueClient();
                await client.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(content)));
            }
            catch (Exception e)
            {
                return new PushQueueReceipt { Success = false, Exception = e };
            }

            return new PushQueueReceipt { Success = true };
        }

        /// <inheritdoc/>
        public async Task<PushQueueReceipt> PushToOutboundQueue(string content)
        {
            try
            {
                Azure.Storage.Queues.QueueClient client = await GetOutboundQueueClient();
                await client.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(content)));
            }
            catch (Exception e)
            {
                return new PushQueueReceipt { Success = false, Exception = e };
            }

            return new PushQueueReceipt { Success = true };
        }

        /// <inheritdoc/>
        public async Task<PushQueueReceipt> PushToValidationQueue(string content)
        {
            try
            {
                Azure.Storage.Queues.QueueClient client = await GetValidationQueueClient();
                await client.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(content)));
            }
            catch (Exception e)
            {
                return new PushQueueReceipt { Success = false, Exception = e };
            }

            return new PushQueueReceipt { Success = true };
        }

        private async Task<Azure.Storage.Queues.QueueClient> GetInboundQueueClient()
        {
            if (_inboundQueueClient == null)
            {
                _inboundQueueClient = new Azure.Storage.Queues.QueueClient(_settings.ConnectionString, _settings.InboundQueueName);
                await _inboundQueueClient.CreateIfNotExistsAsync();
            }

            return _inboundQueueClient;
        }

        private async Task<Azure.Storage.Queues.QueueClient> GetOutboundQueueClient()
        {
            if (_outboundQueueClient == null)
            {
                _outboundQueueClient = new Azure.Storage.Queues.QueueClient(_settings.ConnectionString, _settings.OutboundQueueName);
                await _outboundQueueClient.CreateIfNotExistsAsync();
            }

            return _outboundQueueClient;
        }

        private async Task<Azure.Storage.Queues.QueueClient> GetValidationQueueClient()
        {
            if (_validationQueueClient == null)
            {
                _validationQueueClient = new Azure.Storage.Queues.QueueClient(_settings.ConnectionString, _settings.ValidationQueueName);
                await _validationQueueClient.CreateIfNotExistsAsync();
            }

            return _validationQueueClient;
        }
    }
}
