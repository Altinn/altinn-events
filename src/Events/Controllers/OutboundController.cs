#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers;

/// <summary>
/// Controller responsible for multicasting incoming events to all authorized subscribers.
/// </summary>
[ApiController]
[Route("events/api/v1/outbound")]
[SwaggerTag("Private API")]
public class OutboundController : ControllerBase
{
    private readonly IOutboundService _outboundService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundController"/> class.
    /// </summary>
    public OutboundController(IOutboundService outboundService, ILogger<OutboundController> logger)
    {
        _outboundService = outboundService;
        _logger = logger;
    }

    /// <summary>
    /// Find matching subscriptions and queue event for delivery.
    /// </summary>
    /// <remarks>
    /// Identifies matching subscriptions based on subscription filters and performs authorization 
    /// check before adding event to the outbound queue. Once for each identified subscription.
    /// </remarks>
    /// <returns>A task representing the async operation with the action result.</returns>
    [HttpPost]
    [Authorize(Policy = "PlatformAccess")]
    [Consumes("application/cloudevents+json")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> Post([FromBody] CloudEvent cloudEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _outboundService.PostOutbound(cloudEvent, cancellationToken);
            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "// OutboundController.Post failed for {CloudEventId}.", cloudEvent?.Id);
            return StatusCode(503, e.Message);
        }
    }
}
