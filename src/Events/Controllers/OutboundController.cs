using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller responsible for multicasting incoming events 
    /// to all authorized subscribers.
    /// </summary>
    [Route("events/api/v1/outbound")]
    [ApiController]
    [SwaggerTag("Private API")]
    public class OutboundController : ControllerBase
    {
        private static readonly CloudEventFormatter _formatter = new JsonEventFormatter();
        private readonly IOutboundService _outboundService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundController"/> class.
        /// </summary>
        public OutboundController(IOutboundService outboundService, ILogger<OutboundController> logger)
        {
            _outboundService = outboundService;
            _logger = logger;
        }

        /// <summary>
        /// Submit event for Outbound processing
        /// </summary>
        /// <remarks>
        /// Identifies matching subscriptions based on subscription filter hash.
        /// Runs authorization check before adding event to the outbound queue.
        /// </remarks>
        /// <returns>Returns HTTP 503 Service Unavailable if unable to post to outbound queue.</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/cloudevents+json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [Produces("application/json")]
        public async Task<ActionResult> Post([FromBody] CloudEvent cloudEvent)
        {
            try
            {
                await _outboundService.PostOutbound(cloudEvent);
                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "// OutboundController.Post failed for {cloudEventId}.", cloudEvent?.Id);
                return StatusCode(503, e.Message);
            }
        }
    }
}
