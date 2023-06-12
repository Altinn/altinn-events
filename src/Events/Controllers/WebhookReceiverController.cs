using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller solely for testing purposes. 
    /// Used as webhook endpoint for testing push of events
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("events/api/v1/webhookreceiver")]
    public class WebhookReceiverController : ControllerBase
    {
        /// <summary>
        /// Accepts an http post request and responds OK if request body can be deserialized into a cloud event.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<string>> Post([FromBody] CloudEvent cloudEvent)
        {
            return Ok();
        }
    }
}
