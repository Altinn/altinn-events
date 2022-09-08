using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Altinn.Common.AccessToken.Configuration;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Exceptions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platorm.Events.Extensions;

using AutoMapper;

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
    public class EventsController : ControllerBase
    {
        private readonly IEventsService _eventsService;
        private readonly IRegisterService _registerService;
        private readonly IAuthorization _authorizationService;
        private readonly ILogger _logger;
        private readonly string _eventsBaseUri;
        private readonly AccessTokenSettings _accessTokenSettings;
        private readonly IMapper _mapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsController"/> class
        /// </summary>
        public EventsController(
            IEventsService eventsService,
            IRegisterService registerService,
            IAuthorization authorizationService,
            IOptions<GeneralSettings> settings,
            ILogger<EventsController> logger,
            IOptions<AccessTokenSettings> accessTokenSettings,
            IMapper mapper)
        {
            _registerService = registerService;
            _logger = logger;
            _eventsService = eventsService;
            _eventsBaseUri = $"https://platform.{settings.Value.Hostname}";
            _authorizationService = authorizationService;
            _accessTokenSettings = accessTokenSettings.Value;
            _mapper = mapper;
        }

        /// <summary>
        /// Inserts a new event.
        /// </summary>
        /// <returns>The application metadata object.</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post([FromBody] CloudEventRequestModel cloudEvent)
        {
            if (string.IsNullOrEmpty(cloudEvent.Source.OriginalString) || string.IsNullOrEmpty(cloudEvent.SpecVersion) ||
            string.IsNullOrEmpty(cloudEvent.Type) || string.IsNullOrEmpty(cloudEvent.Subject))
            {
                return Problem("Missing parameter values: source, subject, type, id or time cannot be null", null, 400);
            }

            var item = HttpContext.Items[_accessTokenSettings.AccessTokenHttpContextId];

            if (!cloudEvent.Source.AbsolutePath.StartsWith("/" + item))
            {
                return StatusCode(401, item + " is not authorized to create events for " + cloudEvent.Source);
            }

            try
            {
                string cloudEventId = await _eventsService.StoreCloudEvent(_mapper.Map<CloudEvent>(cloudEvent));
                _logger.LogInformation("Cloud Event successfully stored with id: {cloudEventId}", cloudEventId);
                return Created(cloudEvent.Subject, cloudEventId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to store cloud event in database.");
                return StatusCode(500, $"Unable to store cloud event in database.");
            }
        }

        /// <summary>
        /// Retrieves a set of events related to an application owner based on query parameters.
        /// </summary>
        /// <param name="org">The organisation short code</param>
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
                // Only orgs can do a search based on. Alternative can be a service owner read in scope. Need to be added later
                return StatusCode(401, "Only orgs can call this api");
            }

            if (string.IsNullOrEmpty(after) && from == null)
            {
                return Problem("From or after must be defined.", null, 400);
            }

            if (size < 1)
            {
                return Problem("Size must be a number larger that 0.", null, 400);
            }

            List<string> source = new List<string> { $"%{org}/{app}%" };

            if ((!string.IsNullOrEmpty(person) || !string.IsNullOrEmpty(unit)) && party <= 0)
            {
                try
                {
                    party = await _registerService.PartyLookup(unit, person);
                }
                catch (PlatformHttpException e)
                {
                    return HandlePlatformHttpException(e);
                }
            }

            return await RetrieveAndAuthorizeEvents(after, from, to, party, source, type, size);
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
            if (string.IsNullOrEmpty(after) && from == null)
            {
                return Problem("From or after must be defined.", null, 400);
            }

            if (size < 1)
            {
                return Problem("Size must be a number larger that 0.", null, 400);
            }

            if (string.IsNullOrEmpty(person) && string.IsNullOrEmpty(unit) && party <= 0)
            {
                return Problem("Subject must be specified using either query params party or unit or header value person.", null, 400);
            }

            if (party <= 0)
            {
                try
                {
                    party = await _registerService.PartyLookup(unit, person);
                }
                catch (PlatformHttpException e)
                {
                    return HandlePlatformHttpException(e);
                }
            }

            return await RetrieveAndAuthorizeEvents(after, from, to, party, source, type, size);
        }

        private async Task<ActionResult<List<CloudEvent>>> RetrieveAndAuthorizeEvents(
            string after,
            DateTime? from,
            DateTime? to,
            int party,
            List<string> source,
            List<string> type,
            int size)
        {
            List<CloudEvent> events = await _eventsService.Get(after, from, to, party, source, type, size);

            if (events.Count > 0)
            {
                events = await _authorizationService.AuthorizeEvents(HttpContext.User, events);
            }

            if (events.Count > 0)
            {
                List<KeyValuePair<string, string>> queryCollection = Request.Query
                    .SelectMany(q => q.Value, (col, value) => new KeyValuePair<string, string>(col.Key, value))
                    .Where(q => q.Key != "after")
                    .ToList();

                StringBuilder nextUriBuilder = new StringBuilder($"{_eventsBaseUri}{Request.Path}?after={events.Last().Id}");

                foreach (KeyValuePair<string, string> queryParam in queryCollection)
                {
                    nextUriBuilder.Append($"&{queryParam.Key}={queryParam.Value}");
                }

                Response.Headers.Add("next", nextUriBuilder.ToString());
            }

            return events;
        }

        private ActionResult HandlePlatformHttpException(PlatformHttpException e)
        {
            if (e.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }
            else
            {
                _logger.LogError(e, "// EventsController // HandlePlatformHttpException // Unexpected response from Altinn Platform.");
                return StatusCode(500, e);
            }
        }
    }
}
