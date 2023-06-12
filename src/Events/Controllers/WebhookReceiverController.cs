using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller for supporting automated tests.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Route("events/api/v1/tests/webhookreceiver")]
    [Consumes("application/json")]
    [SwaggerTag("Private API")]
    public class WebhookReceiverController : ControllerBase
    {
        /// <summary>
        /// Accepts an http post request and responds OK.
        /// </summary>
        [HttpPost]
        public ActionResult Post()
        {
            return Ok();
        }
    }
}
