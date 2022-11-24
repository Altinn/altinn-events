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
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller responsible for pushing events to internal event queues.
    /// </summary>
    [Route("events/api/v1/outbound")]
    [ApiController]
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
        /// Alert push controller about a new event.
        /// </summary>
        /// <remarks>
        /// This method will then identify any matching subscriptions
        /// and verify whether the consumer is authorized to receive each event.
        /// If authorized, the event will be added to the outbound queue.
        /// </remarks>
        /// <returns>Returns the result of the request in the form og a HTTP status code.</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [Produces("application/json")]
        public async Task<ActionResult> Post()
        {
            var rawBody = await Request.GetRawBodyAsync(Encoding.UTF8);
            CloudEvent cloudEvent = null;

            try
            {
                cloudEvent = _formatter.DecodeStructuredModeMessage(new MemoryStream(Encoding.UTF8.GetBytes(rawBody)), null, null);

                await _outboundService.PostOutbound(cloudEvent);

                return Ok();
            }
            catch (Exception e)
            {
                var msg = $"// OutboundController.Post failed for {cloudEvent?.Id}. Message: {e.Message}";
                _logger.LogError(e, msg);
                return StatusCode(503, msg);
            }
        }
    }
}
