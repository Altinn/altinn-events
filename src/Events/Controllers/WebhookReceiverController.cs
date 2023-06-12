using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller for supporting automated tests.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Route("events/api/v1/tests/webhookreceiver")]
    [SwaggerTag("Private API")]
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
