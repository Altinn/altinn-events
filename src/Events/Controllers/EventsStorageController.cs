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
    /// Provides operations for saving and retrieving cloud events from persistent storage.
    /// </summary>
    [Authorize]
    [Route("events/api/v1/storage/events")]
    public class EventsStorageController : ControllerBase
    {
        private readonly IAppEventsService _eventsService;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsStorageController"/> class
        /// </summary>
        public EventsStorageController(
            IAppEventsService eventsService,
            ILogger<EventsStorageController> logger,
            IMapper mapper)
        {
            _logger = logger;
            _eventsService = eventsService;
            _mapper = mapper;
        }

        /// <summary>
        /// Saves a cloud event to persistent storage.
        /// </summary>
        /// <param name="cloudEvent">The cloudEvent to be saved</param>
        /// <returns>The application metadata object.</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post([FromBody] CloudEventRequestModel cloudEvent)
        {
            if (string.IsNullOrEmpty(cloudEvent.Source.OriginalString) || string.IsNullOrEmpty(cloudEvent.SpecVersion) ||
            string.IsNullOrEmpty(cloudEvent.Type) || string.IsNullOrEmpty(cloudEvent.Subject))
            {
                return Problem("Missing parameter values: source, subject, type, id or time cannot be null", null, 400);
            }

            try
            {
                string cloudEventId = await _eventsService.SaveToDatabase(_mapper.Map<CloudEvent>(cloudEvent));
                return Created(cloudEvent.Subject, cloudEventId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to store cloud event in database.");
                return StatusCode(500, $"Unable to store cloud event in database.");
            }
        }
    }
}
