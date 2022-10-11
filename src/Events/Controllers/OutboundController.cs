using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller responsible for pushing events to internal event queues.
    /// </summary>
    [Route("events/api/v1/outbound")]
    [ApiController]
    public class OutboundController : ControllerBase
    {
        private readonly IInboundService _eventsService;
        private readonly IOutboundService _outboundService;
        private readonly IMapper _mapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundController"/> class.
        /// </summary>
        public OutboundController(
            IInboundService eventsService,
            IOutboundService outboundService,
            IMapper mapper)
        {
            _eventsService = eventsService;
            _outboundService = outboundService;
            _mapper = mapper;
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
        [Produces("application/json")]
        public async Task<ActionResult> Post([FromBody] CloudEvent cloudEvent)
        {
            await _outboundService.PostOutbound(cloudEvent);

            return Ok();
        }

        /// <summary>
        /// Push a previously registered cloudEvent to the inbound events queue
        /// and further processing by the EventsInbound Azure function
        /// </summary>
        /// <returns>The application metadata object.</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost("inbound")]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post([FromBody] CloudEventRequestModel cloudEvent)
        {
            string cloudEventId = await _eventsService.PostInbound(_mapper.Map<CloudEvent>(cloudEvent));

            return Created(cloudEvent.Subject, cloudEventId);
        }
    }
}
