using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Exceptions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Handles events sevice. 
    /// Notice when saving cloudevent:
    /// - the id for the cloudevent is created by the app
    /// - time is set to null, it will be created in the database
    /// </summary>
    public class AppEventsService : IAppEventsService
    {
        private readonly ICloudEventRepository _repository;
        private readonly IQueueService _queue;

        private readonly IRegisterService _registerService;
        private readonly IAuthorization _authorizationService;
        private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;
        private readonly ILogger<IAppEventsService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppEventsService"/> class.
        /// </summary>
        public AppEventsService(
            ICloudEventRepository repository,
            IQueueService queue,
            IRegisterService registerService,
            IAuthorization authorizationService,
            IClaimsPrincipalProvider claimsPrincipalProvider,
            ILogger<IAppEventsService> logger)
        {
            _repository = repository;
            _queue = queue;
            _registerService = registerService;
            _authorizationService = authorizationService;
            _claimsPrincipalProvider = claimsPrincipalProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> StoreCloudEvent(CloudEvent cloudEvent)
        {
            cloudEvent.Id = Guid.NewGuid().ToString();
            cloudEvent.Time = null;
            cloudEvent = await _repository.Create(cloudEvent);

            PushQueueReceipt receipt = await _queue.PushToQueue(JsonSerializer.Serialize(cloudEvent));

            if (!receipt.Success)
            {
                _logger.LogError(receipt.Exception, "// EventsService // StoreCloudEvent // Failed to push event {EventId} to queue.", cloudEvent.Id);
            }

            return cloudEvent.Id;
        }

        /// <inheritdoc/>
        public async Task PushToConsumer(CloudEventEnvelope cloudEventEnvelope)
        {
            PushQueueReceipt receipt = await _queue.PushToOutboundQueue(JsonSerializer.Serialize(cloudEventEnvelope));
            string cloudEventId = cloudEventEnvelope.CloudEvent.Id;
            int subscriptionId = cloudEventEnvelope.SubscriptionId;

            if (!receipt.Success)
            {
                _logger.LogError(receipt.Exception, "// EventsService // StoreCloudEvent // Failed to push event envelope {EventId} to comsumer with subscriptionId {subscriptionId}.", cloudEventId, subscriptionId);
            }
        }

        /// <inheritdoc/>
        public async Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, int partyId, List<string> source, List<string> type, string unit, string person, int size = 50)
        {
            if (partyId <= 0)
            {
                partyId = await _registerService.PartyLookup(unit, person);
            }

            string subject = partyId == 0 ? string.Empty : $"/party/{partyId}";
            source = source.Count > 0 ? source : null;
            type = type.Count > 0 ? type : null;
            after ??= string.Empty;

            List<CloudEvent> events = await _repository.Get(after, from, to, subject, source, type, size);

            if (events.Count == 0)
            {
                return events;
            }

            return await _authorizationService.AuthorizeEvents(_claimsPrincipalProvider.GetUser(), events);
        }
    }
}
