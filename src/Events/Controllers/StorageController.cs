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

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Provides operations for saving and retrieving cloud events from persistent storage.
    /// </summary>
    [Route("events/api/v1/storage/events")]
    [ApiController]
    public class StorageController : ControllerBase
    {
        private static readonly CloudEventFormatter _formatter = new JsonEventFormatter();

        private readonly IEventsService _eventsService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageController"/> class
        /// </summary>
        public StorageController(
            IEventsService eventsService,
            ILogger<StorageController> logger)
        {
            _logger = logger;
            _eventsService = eventsService;
        }

        /// <summary>
        /// Saves a cloud event to persistent storage.
        /// </summary>
        /// <returns>The cloudEvent subject and id</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post()
        {
            var rawBody = await Request.GetRawBodyAsync(Encoding.UTF8);
            CloudEvent cloudEvent = null;

            try
            {
                cloudEvent = _formatter.DecodeStructuredModeMessage(new MemoryStream(Encoding.UTF8.GetBytes(rawBody)), null, null);

                await _eventsService.Save(cloudEvent);
                return Ok();
            }
            catch (Exception e)
            {
                var msg = $"Temporarily unable to save cloudEventId {cloudEvent?.Id} to storage, please try again. Message: {e.Message}";
                _logger.LogError(e, msg);
                return StatusCode(503, msg);
            }
        }
    }
}
