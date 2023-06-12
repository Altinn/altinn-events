using System.Diagnostics.CodeAnalysis;

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
    [Consumes("application/cloudevents+json")]

    [SwaggerTag("Private API")]
    public class WebhookReceiverController : ControllerBase
    {
        /// <summary>
        /// Accepts an http post request and responds OK if request body can be deserialized into a cloud event.
        /// </summary>
        [HttpPost]
        public ActionResult Post([FromBody] CloudEvent cloudEvent)
        {
            return Ok();
        }
    }
}
