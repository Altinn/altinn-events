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
    public class StorageController : ControllerBase
    {
        private readonly IInboundService _inboundService;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageController"/> class
        /// </summary>
        public StorageController(
            IInboundService inboundService,
            ILogger<StorageController> logger,
            IMapper mapper)
        {
            _logger = logger;
            _inboundService = inboundService;
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
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post([FromBody] CloudEvent cloudEvent)
        {
            try
            {
                string cloudEventId = await _inboundService.Save(_mapper.Map<CloudEvent>(cloudEvent));
                return Created(cloudEvent.Subject, cloudEventId);
            }
            catch (Exception e)
            {
                var msg = $"Temporarily unable to save cloudEventId {cloudEvent?.Id} to storage, please try again.";
                _logger.LogError(e, msg);
                return StatusCode(503, msg);
            }
        }
    }
}
