using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Events.Clients.Interfaces;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Handles events service. 
    /// Notice when saving cloudEvent:
    /// - the id for the cloudEvent is created by the app
    /// - time is set by the database when calling SaveAndPostInbound
    ///   or by the service when calling PushToRegistrationQueue
    /// </summary>
    public class EventsService : IEventsService
    {
        private readonly ICloudEventRepository _repository;
        private readonly IEventsQueueClient _queueClient;

        private readonly IRegisterService _registerService;
        private readonly IAuthorization _authorizationService;
        private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;

        private readonly PlatformSettings _settings;

        private readonly ILogger<IEventsService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsService"/> class.
        /// </summary>
        public EventsService(
            ICloudEventRepository repository,
            IEventsQueueClient queueClient,
            IRegisterService registerService,
            IAuthorization authorizationService,
            IClaimsPrincipalProvider claimsPrincipalProvider,
            IOptions<PlatformSettings> settings,
            ILogger<IEventsService> logger)
        {
            _repository = repository;
            _queueClient = queueClient;
            _registerService = registerService;
            _authorizationService = authorizationService;
            _claimsPrincipalProvider = claimsPrincipalProvider;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> Save(CloudEvent cloudEvent)
        {
            var serializedEvent = JsonSerializer.Serialize(cloudEvent);

            try
            {
                if (IsAppEvent(cloudEvent))
                {
                    await _repository.CreateAppEvent(cloudEvent, serializedEvent);
                }
                else
                {
                    await _repository.CreateEvent(serializedEvent);
                }
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
            cloudEvent.Id = Guid.NewGuid().ToString();
            cloudEvent.Time ??= DateTime.UtcNow;

            QueuePostReceipt receipt = await _queueClient.EnqueueRegistration(JsonSerializer.Serialize(cloudEvent));

            if (!receipt.Success)
            {
                _logger.LogError(receipt.Exception, "// EventsService // RegisterNew // Failed to send cloudEventId {EventId} to queue.", cloudEvent.Id);
                throw receipt.Exception;
            }

            return cloudEvent.Id;
        }

        /// <inheritdoc/>
        public async Task<string> PostInbound(CloudEvent cloudEvent)
        {
            QueuePostReceipt receipt = await _queueClient.EnqueueInbound(JsonSerializer.Serialize(cloudEvent));

            if (!receipt.Success)
            {
                _logger.LogError(receipt.Exception, "// EventsService // PostInbound // Failed to send cloudEventId {EventId} to queue.", cloudEvent.Id);
                throw receipt.Exception;
            }

            return cloudEvent.Id;
        }

        /// <inheritdoc/>
        public async Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, int partyId, List<string> source, List<string> type, string unit, string person, int size = 50)
        {
            if ((!string.IsNullOrEmpty(person) || !string.IsNullOrEmpty(unit)) && partyId <= 0)
            {
                partyId = await _registerService.PartyLookup(unit, person);
            }

            string subject = partyId == 0 ? string.Empty : $"/party/{partyId}";
            source = source.Count > 0 ? source : null;
            type = type.Count > 0 ? type : null;
            after ??= string.Empty;

            List<CloudEvent> events = await _repository.GetAppEvent(after, from, to, subject, source, type, size);

            if (events.Count == 0)
            {
                return events;
            }

            return await _authorizationService.AuthorizeEvents(_claimsPrincipalProvider.GetUser(), events);
        }

        private bool IsAppEvent(CloudEvent cloudEvent)
        {
            return !string.IsNullOrEmpty(cloudEvent.Source.Host) && cloudEvent.Source.Host.EndsWith(_settings.AppsDomain);
        }
    }
}
