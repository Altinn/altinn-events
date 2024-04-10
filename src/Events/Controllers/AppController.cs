using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Altinn.Common.AccessToken.Configuration;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Exceptions;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platorm.Events.Extensions;

using CloudNative.CloudEvents;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Provides operations for handling events
    /// </summary>
    [Authorize]
    [Route("events/api/v1/app")]
    public class AppController : ControllerBase
    {
        private readonly IEventsService _eventsService;
        private readonly ILogger _logger;
        private readonly AccessTokenSettings _accessTokenSettings;
        private readonly string _eventsBaseUri;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppController"/> class
        /// </summary>
        public AppController(
            IEventsService eventsService,
            IOptions<GeneralSettings> settings,
            ILogger<AppController> logger,
            IOptions<AccessTokenSettings> accessTokenSettings)
        {
            _logger = logger;
            _eventsService = eventsService;
            _eventsBaseUri = settings.Value.BaseUri;
            _accessTokenSettings = accessTokenSettings.Value;
        }

        /// <summary>
        /// Inserts a new event.
        /// </summary>
        /// <returns>The cloudEvent subject and id</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post([FromBody] AppCloudEventRequestModel cloudEventRequest)
        {
            if (!cloudEventRequest.ValidateRequiredProperties())
            {
                return Problem("Missing parameter values: source, subject and type cannot be null", null, 400);
            }

            var item = HttpContext.Items[_accessTokenSettings.AccessTokenHttpContextId];

            if (!cloudEventRequest.Source.AbsolutePath.StartsWith("/" + item))
            {
                return StatusCode(401, item + " is not authorized to create events for " + cloudEventRequest.Source);
            }

            try
            {
                var cloudEvent = AppCloudEventExtensions.CreateEvent(cloudEventRequest);

                await _eventsService.RegisterNew(cloudEvent);
                return Created(cloudEvent.Subject, cloudEvent.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to register cloud event in queue.");
                return StatusCode(500, $"Unable to register cloud event.");
            }
        }

        /// <summary>
        /// Retrieves a set of events related to an application owner based on query parameters.
        /// </summary>
        /// <param name="org">The application owner acronym</param>
        /// <param name="app">The name of the app</param>
        /// <param name="after" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the latter even that should be included</param>
        /// <param name="from" example="2022-02-14 07:22:19Z">The lower limit for when the cloud event was created in UTC</param>
        /// <param name="to" example="2022-06-16 13:37:00Z">The upper limit for when the cloud event was created in UTC</param>
        /// <param name="party" example="50002108">The party id representing the subjects</param>
        /// <param name="unit" example="989271156">The organisation number representing the subject</param>
        /// <param name="person" example="01014922047">The person number representing the subject</param>
        /// <param name="type" example="[&quot;app.instance.created&quot;, &quot;app.instance.process.completed&quot;]">
        /// A list of the event types to include
        /// </param>
        /// <param name="size">The maximum number of events to include in the response</param>
        [HttpGet("{org}/{app}")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<List<CloudEvent>>> GetForOrg(
                [FromRoute] string org,
                [FromRoute] string app,
                [FromQuery] string after,
                [FromQuery] DateTime? from,
                [FromQuery] DateTime? to,
                [FromQuery] int party,
                [FromQuery] string unit,
                [FromHeader] string person,
                [FromQuery] List<string> type,
                [FromQuery] int size = 50)
        {
            if (string.IsNullOrEmpty(HttpContext.User.GetOrg()))
            {
                // Only orgs can do a search based on a specific app. Alternative can be a service owner read in scope. Need to be added later
                return StatusCode(401, "Only orgs can call this api");
            }

            (bool isValid, string errorMessage) = ValidateQueryParams(after, from, to, size);

            if (!isValid)
            {
                return Problem(errorMessage, null, 400);
            }

            string resource = $"{AuthorizationConstants.AppResourcePrefix}{org}.{app}";

            try
            {
                List<CloudEvent> events = await _eventsService.GetAppEvents(after, from, to, party, null, resource, type, unit, person, size);
                SetNextLink(events);

                return events;
            }
            catch (PlatformHttpException e)
            {
                return HandlePlatformHttpException(e);
            }
        }

        /// <summary>
        /// Retrieves a set of events related to a party based on query parameters.
        /// </summary>
        /// <param name="after" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the latter event that should be included</param>
        /// <param name="from" example="2022-02-14 07:22:19Z">The lower limit for when the cloud event was created in UTC</param>
        /// <param name="to" example="2022-06-16 13:37:00Z">The upper limit for when the cloud event was created in UTC</param>
        /// <param name="party" example="50002108">The party id representing the subjects</param>
        /// <param name="unit" example="989271156">The organisation number representing the subject</param>
        /// <param name="person" example="01014922047">The person number representing the subject</param>
        /// <param name="source" example="[&quot;https://ttd.apps.at22.altinn.cloud/ttd/apps-test/&quot;
        /// , &quot;https://ttd.apps.at22.altinn.cloud/digdir/bli-tjenesteeier/&quot;, &quot;https://ttd.apps.at22.altinn.cloud/ttd/apps-%/&quot;]">
        /// A list of the sources to include</param>
        /// <param name="type" example="[&quot;app.instance.created&quot;, &quot;app.instance.process.completed&quot;]">
        /// A list of the event types to include</param>
        /// <param name="size">The maximum number of events to include in the response</param>
        [HttpGet("party")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<List<CloudEvent>>> GetForParty(
            [FromQuery] string after,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int party,
            [FromQuery] string unit,
            [FromHeader] string person,
            [FromQuery] List<string> source,
            [FromQuery] List<string> type,
            [FromQuery] int size = 50)
        {
            (bool isValid, string errorMessage) = ValidateQueryParams(after, from, to, size);

            if (!isValid)
            {
                return Problem(errorMessage, null, 400);
            }

            if (string.IsNullOrEmpty(person) && string.IsNullOrEmpty(unit) && party <= 0)
            {
                return Problem("Subject must be specified using either query params party or unit or header value person.", null, 400);
            }

            try
            {
                List<CloudEvent> events = await _eventsService.GetAppEvents(after, from, to, party, source, null, type, unit, person, size);
                SetNextLink(events);
                return events;
            }
            catch (PlatformHttpException e)
            {
                return HandlePlatformHttpException(e);
            }
        }

        private static (bool IsValid, string ErrorMessage) ValidateQueryParams(string after, DateTime? from, DateTime? to, int size)
        {
            if (string.IsNullOrEmpty(after) && from == null)
            {
                return (false, "The 'From' or 'After' parameter must be defined.");
            }

            if (from != null && from.Value.Kind == DateTimeKind.Unspecified)
            {
                return (false, "The 'From' parameter must specify timezone. E.g. 2022-07-07T11:00:53.3917Z for UTC");
            }

            if (to != null && to.Value.Kind == DateTimeKind.Unspecified)
            {
                return (false, "The 'To' parameter must specify timezone. E.g. 2022-07-07T11:00:53.3917Z for UTC");
            }

            if (size < 1)
            {
                return (false, "The 'Size' parameter must be a number larger that 0.");
            }

            return (true, null);
        }

        private void SetNextLink(List<CloudEvent> events)
        {
            if (events.Count > 0)
            {
                List<KeyValuePair<string, string>> queryCollection = HttpContext.Request.Query
                    .SelectMany(q => q.Value, (col, value) => new KeyValuePair<string, string>(col.Key, value))
                    .Where(q => q.Key != "after")
                    .ToList();

                StringBuilder nextUriBuilder = new StringBuilder($"{_eventsBaseUri}{HttpContext.Request.Path}?after={events.Last().Id}");

                foreach (KeyValuePair<string, string> queryParam in queryCollection)
                {
                    nextUriBuilder.Append($"&{queryParam.Key}={queryParam.Value}");
                }

                Response.Headers["next"] = nextUriBuilder.ToString();
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
    }
}
