using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller responsible for pushing events to subscribers
    /// </summary>
    [Route("events/api/v1/push")]
    [ApiController]
    public class PushController : ControllerBase
    {
        private readonly IPushEvent _pushEventsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PushController"/> class.
        /// </summary>
        public PushController(
        IPushEvent pushEventsService)
        {
            _pushEventsService = pushEventsService;
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
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Produces("application/json")]
        public async Task<ActionResult> Post([FromBody] CloudEvent cloudEvent)
        {
            await _pushEventsService.Push(cloudEvent);

            return Ok();
        }
    }
}
