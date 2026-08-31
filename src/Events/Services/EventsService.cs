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
using Altinn.Platform.Events.Wolverine.Commands;
using Altinn.Platform.Events.Wolverine.Publishers;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Logging;
using Wolverine;

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
        private readonly IMessageBus _bus;
        private readonly ILogger _logger;
        private readonly IRegistrationEventPublisher _registrationPublisher;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsService"/> class.
        /// </summary>
        public EventsService(
            ICloudEventRepository repository,
            ITraceLogService traceLogService,
            IEventsQueueClient queueClient,
            IRegisterService registerService,
            IAuthorization authorizationService,
            IMessageBus bus,
            ILogger<EventsService> logger,
            IRegistrationEventPublisher registrationPublisher)
        {
            _repository = repository;
            _traceLogService = traceLogService;
            _queueClient = queueClient;
            _registerService = registerService;
            _authorizationService = authorizationService;
            _bus = bus;
            _logger = logger;
            _registrationPublisher = registrationPublisher;
        }

        /// <inheritdoc/>
        public async Task<bool> Save(CloudEvent cloudEvent, string idempotencyId = null)
        {
            try
            {
                var result = await _repository.CreateEvent(cloudEvent.Serialize(), idempotencyId);
                return result;  
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// EventsService // Save // Failed to save eventId {EventId} to storage.", cloudEvent.Id);
                throw new InvalidOperationException($"Failed to save event with ID {cloudEvent.Id} to storage.", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<string> RegisterNew(CloudEvent cloudEvent, string idempotencyId)
        {
            try    
            {
                await _registrationPublisher.PublishRegistrationEvent(cloudEvent, idempotencyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// EventsService // RegisterNew // Failed to register eventId {EventId}.", cloudEvent.Id);
                throw;
            }

            await _traceLogService.CreateRegisteredEntry(cloudEvent);
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

        /// <inheritdoc/>
        public async Task SaveAndPublish(CloudEvent cloudEvent, string idempotencyId, CancellationToken cancellationToken)
        {
            EnsureCorrectResourceFormat(cloudEvent);
            var cloudEventWasPersisted = await Save(cloudEvent, idempotencyId);

            if (cloudEventWasPersisted)
            {
                string payload = cloudEvent.Serialize();
                await _bus.SendAsync(new InboundEventCommand(payload));
            }
            else
            {
                await _traceLogService.CreateLogEntryDuplicateIdempotencyIdSkipped(cloudEvent, idempotencyId);
            }
        }

        /// <summary>
        /// Changes . notation in resource for Altinn App events to use _.
        /// </summary>
        private static void EnsureCorrectResourceFormat(CloudEvent cloudEvent)
        {
            var resource = cloudEvent["resource"];

            if (resource is not null)
            {
                string resourceValue = resource.ToString();
                if (resourceValue != null && resourceValue.StartsWith("urn:altinn:resource:altinnapp."))
                {
                    string org = null;
                    string app = null;

                    string[] pathParams = cloudEvent.Source?.AbsolutePath.Split("/") ?? Array.Empty<string>();

                    if (pathParams.Length > 5)
                    {
                        org = pathParams[1];
                        app = pathParams[2];
                    }

                    cloudEvent.SetAttributeFromString("resource", $"urn:altinn:resource:app_{org}_{app}");
                }
            }
        }
    }
}
