using System;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller for logging event operations to persistence
    /// </summary>
    /// <param name="traceLogService">Service for logging event operations to persistence</param>
    [Route("events/api/v1/storage/events/logs")]
    [ApiController]
    [SwaggerTag("Private API")]
    public class LogsController(ITraceLogService traceLogService) : ControllerBase
    {
        private readonly ITraceLogService _traceLogService = traceLogService;

        /// <summary>
        /// Create a new trace log for cloud event with a status code.
        /// </summary>
        /// <param name="logEntry">The event wrapper associated with the event for logging <see cref="CloudEventEnvelope"/></param>
        /// <returns></returns>
        [Authorize(Policy = AuthorizationConstants.POLICY_PLATFORM_ACCESS)]
        [HttpPost]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Logs([FromBody] LogEntryDto logEntry)
        {
            var result = await _traceLogService.CreateWebhookResponseEntry(logEntry);

            if (string.IsNullOrEmpty(result))
            {
                return BadRequest();
            }

            return Created();
        }
    }
}
