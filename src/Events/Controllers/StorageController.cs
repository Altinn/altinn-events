using System;
using System.Threading.Tasks;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;
using Microsoft.ApplicationInsights.DataContracts;
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
    [SwaggerTag("Private API")]
    public class StorageController : ControllerBase
    {
        private readonly IEventsService _eventsService;
        private readonly ITraceLogService _traceLogService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageController"/> class
        /// </summary>
        public StorageController(
            IEventsService eventsService,
            ITraceLogService traceLogService,
            ILogger<StorageController> logger)
        {
            _logger = logger;
            _eventsService = eventsService;
            _traceLogService = traceLogService;
        }

        /// <summary>
        /// Saves a cloud event to persistent storage.
        /// </summary>
        /// <returns>The cloudEvent subject and id</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/cloudevents+json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post([FromBody] CloudEvent cloudEvent)
        {
            try
            {
                AddIdTelemetry(cloudEvent.Id);
                await _eventsService.Save(cloudEvent);
                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Temporarily unable to save cloudEventId {cloudEventId} to storage, please try again.", cloudEvent?.Id);
                return StatusCode(503, e.Message);
            }
        }

        /// <summary>
        /// Create a new trace log for cloud event with a status code.
        /// </summary>
        /// <param name="logEntry">The event wrapper associated with the event for logging <see cref="CloudEventEnvelope"/></param>
        /// <returns></returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost("logs")]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Logs([FromBody] LogEntryDto logEntry)
        {
            try
            {
                var result = await _traceLogService.CreateWebhookResponseEntry(logEntry);
                
                if (string.IsNullOrEmpty(result))
                {
                    return BadRequest();
                }

                return Created();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Temporarily unable to save log entry to storage, please try again.");
                return StatusCode(503, e.Message);
            }
        }

        private void AddIdTelemetry(string id)
        {
            RequestTelemetry requestTelemetry = HttpContext.Features.Get<RequestTelemetry>();

            if (requestTelemetry == null)
            {
                return;
            }

            requestTelemetry.Properties.Add("appevent.id", id);
        }
    }
}
