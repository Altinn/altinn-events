using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller responsible for pushing events to subscribers
    /// </summary>
    [Route("events/api/v1/push")]
    [ApiController]
    public class PushToOutboundController : ControllerBase
    {
        private readonly IPushOutboundService _pushOutboundService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PushToOutboundController"/> class.
        /// </summary>
        public PushToOutboundController(IPushOutboundService pushOutboundService)
        {
            _pushOutboundService = pushOutboundService;
        }

        /// <summary>
        /// Alert push controller about a new event.
        /// </summary>
        /// <remarks>
        /// This method will then identify any matching subscriptions and authorize if the consumer is authorized
        /// to receive event. If autorized it will put it on a outbound queue
        /// </remarks>
        /// <returns>Returns the result of the request in the form og a HTTP status code.</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        public async Task<ActionResult> Post([FromBody] CloudEvent cloudEvent)
        {
            await _pushOutboundService.PushOutbound(cloudEvent);

            return Ok();
        }
    }
}
