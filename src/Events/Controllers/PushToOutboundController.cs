using System;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller responsible for pushing events to subscribers
    /// </summary>
    [Route("events/api/v1/push")]
    [ApiController]
    public class PushToOutboundController : ControllerBase
    {
        private readonly IAppEventsService _eventsService;
        private readonly IPushOutboundService _pushOutboundService;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="PushToOutboundController"/> class.
        /// </summary>
        public PushToOutboundController(
            IAppEventsService eventsService,
            IPushOutboundService pushOutboundService,
            ILogger<PushToOutboundController> logger,
            IMapper mapper)
        {
            _eventsService = eventsService;
            _pushOutboundService = pushOutboundService;
            _logger = logger;
            _mapper = mapper;
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
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post([FromBody] CloudEventRequestModel cloudEvent)
        {
            string cloudEventId = await _eventsService.PushToInboundQueue(_mapper.Map<CloudEvent>(cloudEvent));

            return Created(cloudEvent.Subject, cloudEventId);
        }
    }
}
