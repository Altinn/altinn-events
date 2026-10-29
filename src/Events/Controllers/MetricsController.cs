using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller for internal use that triggers retrieval and emission of custom metrics.
    /// </summary>
    /// <param name="metricsService">Service for retrieving and emitting custom metrics</param>
    [Route("events/api/v1/metrics")]
    [ApiController]
    [SwaggerTag("Private API")]
    public class MetricsController(IMetricsService metricsService) : ControllerBase
    {
        private readonly IMetricsService _metricsService = metricsService;

        /// <summary>
        /// Triggers retrieval and emission of custom metrics.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        [HttpPost("getmetrics")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> TriggerGetMetrics(CancellationToken cancellationToken)
        {
            await _metricsService.TriggerGetMetrics(cancellationToken);
            return Ok();
        }
    }
}
