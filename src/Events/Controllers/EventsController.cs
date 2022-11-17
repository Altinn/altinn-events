using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller for all events related operations
    /// </summary>
    [Authorize]
    [Route("events/api/v1/events")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly IEventsService _events;
        private readonly GeneralSettings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsController"/> class.
        /// </summary>
        public EventsController(IEventsService events, IOptions<GeneralSettings> settings)
        {
            _events = events;
            _settings = settings.Value;
        }

        /// <summary>
        /// Endpoint for posting a new cloud event
        /// </summary>
        /// <param name="cloudEvent">The incoming cloud event</param>
        /// <returns>The cloud event subject and id</returns>
        [Authorize(Policy = AuthorizationConstants.POLICY_SCOPE_EVENTS_PUBLISH)]
        [Consumes("application/cloudevents+json")]
        [HttpPost]
        public async Task<ActionResult<string>> Post([FromBody] CloudEvent cloudEvent)
        {
            if (!_settings.EnableExternalEvents)
            {
                return NotFound();
            }

            if (!AuthorizeEvent(cloudEvent))
            {
                return Forbid();
            }

            try
            {
                var id = await _events.RegisterNew(cloudEvent);
                return Created(cloudEvent.Subject, id);
            }
            catch
            {
                return StatusCode(500, $"Unable to register cloud event.");
            }
        }

        private static bool AuthorizeEvent(CloudEvent cloudEvent)
        {
            // Further authorization to be implemented in Altinn/altinn-events#183            
            return true;
        }
    }
}
