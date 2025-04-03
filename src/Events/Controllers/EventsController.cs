using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Services.Interfaces;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        private readonly string _eventsBaseUri;
        private readonly IAuthorization _authorizationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsController"/> class.
        /// </summary>
        public EventsController(
            IEventsService events,
            IAuthorization authorizationService,
            IOptions<GeneralSettings> settings)
        {
            _eventsService = events;
            _authorizationService = authorizationService;
            _eventsBaseUri = settings.Value.BaseUri;
        }

        /// <summary>
        /// Endpoint for posting a new cloud event
        /// </summary>
        /// <param name="cloudEvent">The incoming cloud event</param>
        /// <returns>The cloud event subject and id</returns>
        [HttpPost]
        [Authorize(Policy = AuthorizationConstants.POLICY_PUBLISH_SCOPE_OR_PLATFORM_ACCESS)]
        [Consumes("application/cloudevents+json")]
        public async Task<ActionResult<string>> Post([FromBody] CloudEvent cloudEvent)
        {
            (bool isValid, string errorMessage) = ValidateCloudEvent(cloudEvent);
            if (!isValid)
            {
                return Problem(errorMessage, null, 400);
            }

            bool isAuthorizedToPublish = await _authorizationService.AuthorizePublishEvent(cloudEvent);
            if (!isAuthorizedToPublish)
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
        /// <param name="resource" example="urn:altinn:resource:app_ttd_apps-test">
        /// Required resource attribute</param>
        /// <param name="after" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Retrieve events that were registered after this event Id</param>
        /// <param name="subject">Optional filter by subject. Only exact matches will be returned.</param>
        /// <param name="alternativeSubject" example="/person/16035001577">Optional filter by extension attribute alternative subject. Only exact matches will be returned.</param>
        /// <param name="type" example="[&quot;instance.created&quot;, &quot;instance.process.completed&quot;]">
        /// Optional filter by event type. </param>
        /// <param name="size">The maximum number of events to include in the response.</param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used by other objects or threads to receive notice of cancellation.
        /// </param>
        [HttpGet]
        [Authorize(Policy = AuthorizationConstants.POLICY_SCOPE_EVENTS_SUBSCRIBE)]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Produces("application/cloudevents+json")]
        public async Task<ActionResult<List<CloudEvent>>> Get(
            [FromQuery, Required] string resource,
            [FromQuery] string after,
            [FromQuery] string subject,
            [FromHeader(Name = "Altinn-AlternativeSubject")] string alternativeSubject,
            [FromQuery] List<string> type,
            [FromQuery] int size,
            CancellationToken cancellationToken)
        {
            // Set size to 50 if it is less than 1 and clamp it to a maximum of 1000
            size = size < 1 ? 50 : Math.Min(size, 1000);

            (bool isValid, string errorMessage) =
                ValidateQueryParams(resource, after, subject, alternativeSubject);

            if (!isValid)
            {
                return Problem(errorMessage, null, 400);
            }

            List<CloudEvent> events;
            try
            {
                events = await _eventsService.GetEvents(
                    resource, after, subject, alternativeSubject, type, size, cancellationToken);
            }
            catch (TaskCanceledException tce)
            {
                if (tce.CancellationToken.IsCancellationRequested)
                {
                    return Problem(tce.StackTrace, null, 499, tce.Message);
                }

                throw;
            }

            bool includeSubject = !string.IsNullOrEmpty(alternativeSubject) && string.IsNullOrEmpty(subject);
            SetNextLink(events, includeSubject);

            return events;
        }

        private static (bool IsValid, string ErrorMessage) ValidateQueryParams(string resource, string after, string subject, string alternativeSubject)
        {
            if (!resource.StartsWith("urn:altinn:resource:"))
            {
                return (false, "The 'resource' parameter must begin with `urn:altinn:resource:`");
            }

            if (string.IsNullOrEmpty(after))
            {
                return (false, "The 'after' parameter must be defined.");
            }

            if (!string.IsNullOrEmpty(subject) && !string.IsNullOrEmpty(alternativeSubject))
            {
                return (false, "Only one of 'subject' or 'alternativeSubject' can be defined.");
            }

            return (true, null);
        }

        private static (bool IsValid, string ErrorMessage) ValidateCloudEvent(CloudEvent cloudEvent)
        {
            string eventResource = cloudEvent["resource"]?.ToString();
            if (string.IsNullOrEmpty(eventResource))
            {
                return (false, "A 'resource' property must be defined.");
            }

            if (!UriExtensions.IsValidUrn(eventResource))
            {
                return (false, "'Resource' must be a valid urn.");
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

                Response.Headers["next"] = nextUriBuilder.ToString();
            }
        }
    }
}
