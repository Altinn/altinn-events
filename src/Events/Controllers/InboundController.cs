using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller responsible for posting events to events-inbound queue.
    /// </summary>
    [Route("events/api/v1/inbound")]
    [ApiController]
    public class InboundController : ControllerBase
    {
        private static readonly CloudEventFormatter _formatter = new JsonEventFormatter();
        private readonly IEventsService _eventsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="InboundController"/> class.
        /// </summary>
        public InboundController(IEventsService eventsService)
        {
            _eventsService = eventsService;
        }

        /// <summary>
        /// Post a previously registered cloudEvent to the inbound events queue
        /// and further processing by the EventsInbound Azure function
        /// </summary>
        /// <returns>The cloudEvent subject and id</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post()
        {
            var rawBody = await Request.GetRawBodyAsync(Encoding.UTF8);
            CloudEvent cloudEvent = null;

            try
            {
                cloudEvent = _formatter.DecodeStructuredModeMessage(new MemoryStream(Encoding.UTF8.GetBytes(rawBody)), null, null);

                string cloudEventId = await _eventsService.PostInbound(cloudEvent);

                return Created(cloudEvent.Subject, cloudEventId);
            }
            catch (Exception e)
            {
                return StatusCode(503, $"Temporarily unable to send cloudEventId ${cloudEvent?.Id} to events-inbound queue. Error: {e.Message}");
            }
        }
    }
}
