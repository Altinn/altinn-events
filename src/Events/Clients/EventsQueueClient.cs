using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Clients
{
    /// <summary>
    /// Implementation of the <see ref="IEventsQueueClient"/> using Azure Storage Queues.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class EventsQueueClient : IEventsQueueClient
    {
        private readonly QueueStorageSettings _settings;

        private QueueClient _registrationQueueClient;

        private QueueClient _inboundQueueClient;

        private QueueClient _outboundQueueClient;

        private QueueClient _validationQueueClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsQueueClient"/> class.
        /// </summary>
        /// <param name="settings">The queue storage settings</param>
        public EventsQueueClient(IOptions<QueueStorageSettings> settings)
        {
            _settings = settings.Value;
        }

        /// <inheritdoc/>
        public async Task<QueuePostReceipt> EnqueueRegistration(string content)
        {
            try
            {
                QueueClient client = await GetRegistrationQueueClient();
                await client.SendMessageAsync(content);
            }
            catch (Exception e)
            {
                return new QueuePostReceipt { Success = false, Exception = e };
            }

            return new QueuePostReceipt { Success = true };
        }

        /// <inheritdoc/>
        public async Task<QueuePostReceipt> EnqueueInbound(string content)
        {
            try
            {
                QueueClient client = await GetInboundQueueClient();
                await client.SendMessageAsync(content);
            }
            catch (Exception e)
            {
                return new QueuePostReceipt { Success = false, Exception = e };
            }

            return new QueuePostReceipt { Success = true };
        }

        /// <inheritdoc/>
        public async Task<QueuePostReceipt> EnqueueOutbound(string content)
        {
            try
            {
                QueueClient client = await GetOutboundQueueClient();
                await client.SendMessageAsync(content);
            }
            catch (Exception e)
            {
                return new QueuePostReceipt { Success = false, Exception = e };
            }

            return new QueuePostReceipt { Success = true };
        }

        /// <inheritdoc/>
        public async Task<QueuePostReceipt> EnqueueSubscriptionValidation(string content)
        {
            try
            {
                QueueClient client = await GetValidationQueueClient();
                await client.SendMessageAsync(content);
            }
            catch (Exception e)
            {
                return new QueuePostReceipt { Success = false, Exception = e };
            }

            return new QueuePostReceipt { Success = true };
        }

        private async Task<QueueClient> GetRegistrationQueueClient()
        {
            if (_registrationQueueClient == null)
            {
                _registrationQueueClient = new QueueClient(_settings.ConnectionString, _settings.RegistrationQueueName);
                await _registrationQueueClient.CreateIfNotExistsAsync();
            }

            return _registrationQueueClient;
        }

        private async Task<QueueClient> GetInboundQueueClient()
        {
            if (_inboundQueueClient == null)
            {
                _inboundQueueClient = new QueueClient(_settings.ConnectionString, _settings.InboundQueueName);
                await _inboundQueueClient.CreateIfNotExistsAsync();
            }

            return _inboundQueueClient;
        }

        private async Task<QueueClient> GetOutboundQueueClient()
        {
            if (_outboundQueueClient == null)
            {
                _outboundQueueClient = new QueueClient(_settings.ConnectionString, _settings.OutboundQueueName);
                await _outboundQueueClient.CreateIfNotExistsAsync();
            }

            return _outboundQueueClient;
        }

        private async Task<QueueClient> GetValidationQueueClient()
        {
            if (_validationQueueClient == null)
            {
                _validationQueueClient = new QueueClient(_settings.ConnectionString, _settings.ValidationQueueName);
                await _validationQueueClient.CreateIfNotExistsAsync();
            }

            return _validationQueueClient;
        }
    }
}
