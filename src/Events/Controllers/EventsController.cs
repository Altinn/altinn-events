using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Exceptions;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller for all events related operations
    /// </summary>
    [Authorize]
    [Route("events/api/v1/events")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly IEventsService _eventsService;
        private readonly ILogger _logger;
        private readonly GeneralSettings _settings;
        private readonly string _eventsBaseUri;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsController"/> class.
        /// </summary>
        public EventsController(
            IEventsService events,
            ILogger<EventsController> logger,
            IOptions<GeneralSettings> settings)
        {
            _eventsService = events;
            _logger = logger;
            _settings = settings.Value;
            _eventsBaseUri = settings.Value.BaseUri;
        }

        /// <summary>
        /// Endpoint for posting a new cloud event
        /// </summary>
        /// <param name="cloudEvent">The incoming cloud event</param>
        /// <returns>The cloud event subject and id</returns>
        [HttpPost]
        [Authorize(Policy = AuthorizationConstants.POLICY_SCOPE_EVENTS_PUBLISH)]
        [Consumes("application/cloudevents+json")]
        public async Task<ActionResult<string>> Post([FromBody] CloudEvent cloudEvent)
        {
            if (!_settings.EnableExternalEvents)
            {
                return NotFound();
            }

            if (!AuthorizeEvent(cloudEvent))
            {
                return Forbid();
            }

            try
            {
                await _eventsService.RegisterNew(cloudEvent);
                return Ok();
            }
            catch
            {
                return StatusCode(500, $"Unable to register cloud event.");
            }
        }

        /// <summary>
        /// Retrieves a set of events related based on query parameters.
        /// </summary>
        /// <param name="after" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Retrieve events that were registered after this event Id</param>
        /// <param name="source" example="https://ttd.apps.at22.altinn.cloud/ttd/apps-test/">
        /// Optional source </param>
        /// <param name="subject">Optional filter by subject. Only exact matches will be returned.</param>
        /// <param name="alternativeSubject" example="/person/16035001577">Optional filter by extension attribute alternative subject. Only exact matches will be returned.</param>
        /// <param name="type" example="[&quot;instance.created&quot;, &quot;instance.process.completed&quot;]">
        /// Optional filter by event type. </param>
        /// <param name="size">The maximum number of events to include in the response.</param>
        [HttpGet]
        [Authorize(Policy = AuthorizationConstants.SCOPE_EVENTS_SUBSCRIBE)]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Produces("application/cloudevents+json")]
        public async Task<ActionResult<List<CloudEvent>>> Get(
            [FromQuery] string after,
            [FromQuery] string source,
            [FromQuery] string subject,
            [FromHeader(Name = "Altinn-AlternativeSubject")] string alternativeSubject,
            [FromQuery] List<string> type,
            [FromQuery] int size = 50)
        {
            if (!_settings.EnableExternalEvents)
            {
                return NotFound();
            }

            // Maximum allowed result set size is adjusted silently.
            size = size > 1000 ? 1000 : size;

            (bool isValid, string errorMessage) = ValidateQueryParams(after, size, source);

            if (!isValid)
            {
                return Problem(errorMessage, null, 400);
            }

            try
            {
                List<CloudEvent> events = await _eventsService.GetEvents(after, source, subject, alternativeSubject, type, size);
                bool includeSubject = !string.IsNullOrEmpty(alternativeSubject) && string.IsNullOrEmpty(subject);
                SetNextLink(events, includeSubject);
                return events;
            }
            catch (PlatformHttpException e)
            {
                return HandlePlatformHttpException(e);
            }
        }

        private static (bool IsValid, string ErrorMessage) ValidateQueryParams(string after, int size, string source)
        {
            if (string.IsNullOrEmpty(after))
            {
                return (false, "The 'after' parameter must be defined.");
            }

            if (size < 1)
            {
                return (false, "The 'size' parameter must be a number larger that 0.");
            }

            if (string.IsNullOrEmpty(source))
            {
                return (false, "The 'source' parameter must be defined.");
            }

            return (true, null);
        }

        private void SetNextLink(List<CloudEvent> events, bool includeSubject = false)
        {
            if (events.Count > 0)
            {
                List<KeyValuePair<string, string>> queryCollection = HttpContext.Request.Query
                    .SelectMany(q => q.Value, (col, value) => new KeyValuePair<string, string>(col.Key, value))
                    .Where(q => q.Key != "after")
                    .ToList();

                StringBuilder nextUriBuilder = new($"{_eventsBaseUri}{HttpContext.Request.Path}?after={events.Last().Id}");

                foreach (KeyValuePair<string, string> queryParam in queryCollection)
                {
                    nextUriBuilder.Append($"&{queryParam.Key}={queryParam.Value}");
                }

                if (includeSubject)
                {
                    var subject = events.First().Subject;
                    nextUriBuilder.Append($"&subject={subject}");
                }

                Response.Headers.Add("next", nextUriBuilder.ToString());
            }
        }

        private ActionResult HandlePlatformHttpException(PlatformHttpException e)
        {
            if (e.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }
            else
            {
                _logger.LogError(e, "// AppController // HandlePlatformHttpException // Unexpected response from Altinn Platform.");
                return Problem(e.Message, statusCode: 500);
            }
        }

        private static bool AuthorizeEvent(CloudEvent cloudEvent)
        {
            // Further authorization to be implemented in Altinn/altinn-events#183
            return true;
        }
    }
}
