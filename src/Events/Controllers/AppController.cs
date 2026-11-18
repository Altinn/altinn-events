using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Common.AccessToken.Configuration;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Exceptions;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Register.Contracts;

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
    [ApiController]
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
        /// <param name="cloudEventRequest">The cloud event request model.</param>
        /// <param name="idempotencyKey">
        /// Optional client-supplied idempotency key (must be a valid GUID). If a cloud event with the
        /// same idempotency key has already been registered, the duplicate is detected and skipped
        /// during processing; the response to the caller is unaffected.
        /// </param>
        /// <returns>The cloudEvent subject and id</returns>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPost]
        [Consumes("application/json")]
        [SwaggerResponse(201, Type = typeof(Guid))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Post(
            [FromBody] AppCloudEventRequestModel cloudEventRequest,
            [FromHeader(Name = "Idempotency-Key")] [SwaggerParameter("Optional idempotency key (GUID) used to detect and skip duplicate submissions.")] Guid? idempotencyKey)
        {
            var item = HttpContext.Items[_accessTokenSettings.AccessTokenHttpContextId];

            if (!cloudEventRequest.ValidateRequiredProperties())
            {
                return Problem("Missing parameter values: source, subject and type cannot be null", null, 400);
            }

            if (!cloudEventRequest.Source.AbsolutePath.StartsWith("/" + item))
            {
                return StatusCode(401, item + " is not authorized to create events for " + cloudEventRequest.Source);
            }

            try
            {
                var cloudEvent = AppCloudEventExtensions.CreateEvent(cloudEventRequest);
                AddIdTelemetry(cloudEvent.Id);

                await _eventsService.RegisterNew(cloudEvent, idempotencyKey);
                return Created(cloudEvent.Subject, cloudEvent.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to register cloud event in queue.");
                return StatusCode(500, $"Unable to register cloud event.");
            }
        }

        /// <summary>
        /// Search for app events from the specified app with the given filter parameters.
        /// </summary>
        /// <remarks>This endpoint is exclusive for service owners. End users must use 
        /// the <c>party</c>endpoint instead.</remarks>
        /// <param name="org">The application owner acronym</param>
        /// <param name="app">The name of the app</param>
        /// <param name="query">The query parameters and headers used to filter the events</param>
        /// <param name="cancellationToken">A cancellation token that is cancelled if the request is aborted</param>
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
            AppEventsQueryModel query,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(HttpContext.User.GetOrg()))
            {
                return StatusCode(401, "Only orgs can call this api");
            }

            (bool isValid, string errorMessage) = ValidateQueryParams(query);

            if (!isValid)
            {
                return Problem(errorMessage, null, 400);
            }

            string resource = string.Format(AuthorizationConstants.AppResourceTemplate, org, app);

            try
            {
                List<CloudEvent> events = await _eventsService.GetAppEvents(
                    query.After, query.From, query.To, GetSubjectUrn(query), null, resource, query.Type, query.Size, cancellationToken);
                SetNextLink(events);

                return events;
            }
            catch (PlatformHttpException e)
            {
                return HandlePlatformHttpException(e);
            }
        }

        /// <summary>
        /// Search for app events as an end user.
        /// </summary>
        /// <remarks>This endpoints requires one event subject parameter. That means either
        /// <c>party</c>, <c>unit</c>, <c>person</c> or <c>externalUrn</c>.</remarks>
        /// <param name="query">The query parameters and headers used to filter the events</param>
        /// <param name="source" example="[&quot;https://ttd.apps.at22.altinn.cloud/ttd/apps-test/%&quot;, &quot;https://ttd.apps.at22.altinn.cloud/digdir/bli-tjenesteeier/%&quot;]">
        /// A list of the sources to include</param>
        /// <param name="cancellationToken">A cancellation token that is cancelled if the request is aborted</param>
        [HttpGet("party")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<List<CloudEvent>>> GetForParty(
            AppEventsQueryModel query,
            [FromQuery] List<string> source,
            CancellationToken cancellationToken)
        {
            (bool isValid, string errorMessage) = ValidateQueryParams(query);

            if (!isValid)
            {
                return Problem(errorMessage, null, 400);
            }

            PartyUrn subject = GetSubjectUrn(query);

            if (subject is null)
            {
                return Problem("Subject must be specified using query parameter party or unit, or header parameter person or externalUrn.", null, 400);
            }

            try
            {
                List<CloudEvent> events = await _eventsService.GetAppEvents(
                    query.After, query.From, query.To, subject, source, null, query.Type, query.Size, cancellationToken);
                SetNextLink(events);
                return events;
            }
            catch (PlatformHttpException e)
            {
                return HandlePlatformHttpException(e);
            }
        }

        /// <summary>
        /// Converts the subject parameter of the query into a party URN. The query must have
        /// passed <see cref="ValidateQueryParams"/>, which ensures that at most one subject
        /// parameter is set and that its value can be parsed.
        /// </summary>
        /// <returns>The party URN of the subject, or null if no subject parameter is set.</returns>
        private static PartyUrn GetSubjectUrn(AppEventsQueryModel query)
        {
            if (query.Party > 0)
            {
                return PartyUrn.PartyId.Create((uint)query.Party);
            }

            if (!string.IsNullOrEmpty(query.Unit))
            {
                return PartyUrn.OrganizationId.Create(OrganizationIdentifier.Parse(query.Unit));
            }

            if (!string.IsNullOrEmpty(query.Person))
            {
                return PartyUrn.PersonId.Create(PersonIdentifier.Parse(query.Person));
            }

            if (!string.IsNullOrEmpty(query.ExternalUrn))
            {
                return PartyUrn.Parse(query.ExternalUrn);
            }

            return null;
        }

        private static (bool IsValid, string ErrorMessage) ValidateQueryParams(AppEventsQueryModel query)
        {
            if (string.IsNullOrEmpty(query.After) && query.From == null)
            {
                return (false, "The 'From' or 'After' parameter must be defined.");
            }

            if (query.From != null && !query.From.Value.SpecifiesTimezone())
            {
                return (false, "The 'From' parameter must specify timezone. E.g. 2022-07-07T11:00:53.3917Z for UTC");
            }

            if (query.To != null && !query.To.Value.SpecifiesTimezone())
            {
                return (false, "The 'To' parameter must specify timezone. E.g. 2022-07-07T11:00:53.3917Z for UTC");
            }

            if (query.Size < 1)
            {
                return (false, "The 'Size' parameter must be a number larger that 0.");
            }

            bool hasParty = query.Party > 0;
            bool hasUnit = !string.IsNullOrEmpty(query.Unit);
            bool hasPerson = !string.IsNullOrEmpty(query.Person);
            bool hasExternalUrn = !string.IsNullOrEmpty(query.ExternalUrn);

            if (new[] { hasParty, hasUnit, hasPerson, hasExternalUrn }.Count(isSet => isSet) > 1)
            {
                return (false, "Only one of 'Party', 'Unit', 'Person' or 'ExternalUrn' can be defined.");
            }

            if (hasPerson && !PersonIdentifier.TryParse(query.Person, null, out _))
            {
                return (false, "Value of 'Person' needs to be a valid national identity number.");
            }

            if (hasUnit && !OrganizationIdentifier.TryParse(query.Unit, null, out _))
            {
                return (false, "Value of 'Unit' needs to be a valid organization number.");
            }

            if (hasExternalUrn && !PartyUrn.TryParse(query.ExternalUrn, out _))
            {
                return (false, "Value of 'ExternalUrn' needs to be a valid party URN.");
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

        private static void AddIdTelemetry(string id)
        {
            Activity.Current?.AddTag("appevent.id", id);
        }
    }
}
