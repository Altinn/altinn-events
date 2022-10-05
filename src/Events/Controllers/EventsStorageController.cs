using System;
using System.Threading.Tasks;

using Altinn.Common.AccessToken.Configuration;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using AutoMapper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Provides operations for handling events
    /// </summary>
    [Authorize]
    [Route("events/api/v1/storage/events")]
    public class EventsStorageController : ControllerBase
    {
        private readonly IAppEventsService _eventsService;
        private readonly ILogger _logger;
        private readonly AccessTokenSettings _accessTokenSettings;
        private readonly IMapper _mapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsStorageController"/> class
        /// </summary>
        public EventsStorageController(
            IAppEventsService eventsService,
            ILogger<EventsStorageController> logger,
            IOptions<AccessTokenSettings> accessTokenSettings,
            IMapper mapper)
        {
            _logger = logger;
            _eventsService = eventsService;
            _accessTokenSettings = accessTokenSettings.Value;
            _mapper = mapper;
        }

        /// <summary>
        /// Registers a new cloudEvent.
        /// </summary>
        /// <returns>The application metadata object.</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
                _logger.LogInformation("Cloud Event successfully stored with id: {cloudEventId}", cloudEventId);
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
