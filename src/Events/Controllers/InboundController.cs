using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller responsible for posting events to events-inbound queue.
    /// </summary>
    [Route("events/api/v1/inbound")]
    [ApiController]
    [SwaggerTag("Private API")]
    public class InboundController : ControllerBase
    {
        private readonly IEventsService _eventsService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="InboundController"/> class.
        /// </summary>
        public InboundController(IEventsService eventsService, ILogger<InboundController> logger)
        {
            _eventsService = eventsService;
            _logger = logger;
        }

        /// <summary>
        /// Post a previously registered cloudEvent to the inbound events queue
        /// and further processing by the EventsInbound Azure function
        /// </summary>
        /// <returns>The cloudEvent subject and id</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/cloudevents+json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post([FromBody] CloudEvent cloudEvent)
        {
            try
            {
                await _eventsService.PostInbound(cloudEvent);
                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "// InboundController.Post failed for {cloudEventId}.", cloudEvent?.Id);
                return StatusCode(503, e.Message);
            }
        }
    }
}
