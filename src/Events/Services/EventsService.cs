using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Functionality for registering and forwarding cloud events. 
    /// </summary>
    public class EventsService : IEventsService
    {
        private readonly ICloudEventRepository _repository;
        private readonly ITraceLogService _traceLogService;
        private readonly IEventsQueueClient _queueClient;

        private readonly IRegisterService _registerService;
        private readonly IAuthorization _authorizationService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsService"/> class.
        /// </summary>
        public EventsService(
            ICloudEventRepository repository,
            ITraceLogService traceLogService,
            IEventsQueueClient queueClient,
            IRegisterService registerService,
            IAuthorization authorizationService,
            ILogger<EventsService> logger)
        {
            _repository = repository;
            _traceLogService = traceLogService;
            _queueClient = queueClient;
            _registerService = registerService;
            _authorizationService = authorizationService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> Save(CloudEvent cloudEvent)
        {
            try
            {
                await _repository.CreateEvent(cloudEvent.Serialize());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// EventsService // Save // Failed to save eventId {EventId} to storage.", cloudEvent.Id);
                throw;
            }

            return cloudEvent.Id;
        }

        /// <inheritdoc/>
        public async Task<string> RegisterNew(CloudEvent cloudEvent)
        {
            QueuePostReceipt receipt = await _queueClient.EnqueueRegistration(cloudEvent.Serialize());

            if (!receipt.Success)
            {
                _logger.LogError(receipt.Exception, "// EventsService // RegisterNew // Failed to send cloudEventId {EventId} to queue.", cloudEvent.Id);
                throw receipt.Exception;
            }

            await _traceLogService.CreateRegisteredEntry(cloudEvent); // log entry for registering a new event
            return cloudEvent.Id;
        }

        /// <inheritdoc/>
        public async Task<string> PostInbound(CloudEvent cloudEvent)
        {
            QueuePostReceipt receipt = await _queueClient.EnqueueInbound(cloudEvent.Serialize());

            if (!receipt.Success)
            {
                _logger.LogError(receipt.Exception, "// EventsService // PostInbound // Failed to send cloudEventId {EventId} to queue.", cloudEvent.Id);
                throw receipt.Exception;
            }

            return cloudEvent.Id;
        }

        /// <inheritdoc/>
        public async Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, int partyId, List<string> source, string resource, List<string> type, string unit, string person, int size = 50)
        {
            if ((!string.IsNullOrEmpty(person) || !string.IsNullOrEmpty(unit)) && partyId <= 0)
            {
                partyId = await _registerService.PartyLookup(unit, person);
            }

            string subject = partyId == 0 ? null : $"/party/{partyId}";
            source = source?.Count > 0 ? source : null;
            type = type.Count > 0 ? type : null;
            after ??= string.Empty;

            List<CloudEvent> events = await _repository.GetAppEvents(after, from, to, subject, source, resource, type, size);

            if (events.Count == 0)
            {
                return events;
            }

            return await _authorizationService.AuthorizeAltinnAppEvents(events);
        }

        /// <inheritdoc/>
        public async Task<List<CloudEvent>> GetEvents(
            string resource, 
            string after, 
            string subject, 
            string alternativeSubject, 
            List<string> types, 
            int size,
            CancellationToken cancellationToken)
        {
            types = types.Count > 0 ? types : null;
            after ??= string.Empty;

            List<CloudEvent> events = await _repository.GetEvents(resource, after, subject, alternativeSubject, types, size);

            if (events.Count == 0)
            {
                return events;
            }

            return await _authorizationService.AuthorizeEvents(events, cancellationToken);
        }
    }
}
