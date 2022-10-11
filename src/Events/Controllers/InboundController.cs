using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller responsible for pushing events to internal event queues.
    /// </summary>
    [Route("events/api/v1/inbound")]
    [ApiController]
    public class InboundController : ControllerBase
    {
        private readonly IInboundService _inboundService;

        /// <summary>
        /// Initializes a new instance of the <see cref="InboundController"/> class.
        /// </summary>
        public InboundController(IInboundService inboundService)
        {
            _inboundService = inboundService;
        }

        /// <summary>
        /// Post a previously registered cloudEvent to the inbound events queue
        /// and further processing by the EventsInbound Azure function
        /// </summary>
        /// <returns>The application metadata object.</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post([FromBody] CloudEvent cloudEvent)
        {
            string cloudEventId = await _inboundService.PostInbound(cloudEvent);

            return Created(cloudEvent.Subject, cloudEventId);
        }
    }
}
