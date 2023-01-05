using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platorm.Events.Extensions;

using AutoMapper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Events.Controllers
{
    /// <summary>
    /// Controller to handle administration of event subscriptions
    /// </summary>
    [Route("events/api/v1/subscriptions")]
    [ApiController]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _eventsSubscriptionService;
        private readonly IMapper _mapper;
        private readonly PlatformSettings _settings;
        private readonly IClaimsPrincipalProvider _claimsPrincipalProvider;

        private const string UserPrefix = "/user/";
        private const string OrgPrefix = "/org/";

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionController"/> class.
        /// </summary>
        public SubscriptionController(
            ISubscriptionService eventsSubscriptionService,
            IMapper mapper,
            IClaimsPrincipalProvider claimsPrincipalProvider,
            IOptions<PlatformSettings> settings)
        {
            _eventsSubscriptionService = eventsSubscriptionService;
            _mapper = mapper;
            _claimsPrincipalProvider = claimsPrincipalProvider;
            _settings = settings.Value;
        }

        /// <summary>
        /// Register an subscription for events.
        /// </summary>
        /// <remarks>
        /// Requires information about endpoint to post events for subscribers.
        /// </remarks>
        /// <param name="eventsSubscriptionRequest">The subscription details</param>
        [Authorize]
        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Produces("application/json")]
        public async Task<ActionResult<Subscription>> Post([FromBody] SubscriptionRequestModel eventsSubscriptionRequest)
        {
            bool isAppSubscription = true;

            if (!Uri.IsWellFormedUriString(eventsSubscriptionRequest.SourceFilter.ToString(), UriKind.Absolute))
            {
                return StatusCode(400, "SourceFilter must be an absolute URI");
            }

            if (!eventsSubscriptionRequest.SourceFilter.DnsSafeHost.EndsWith(_settings.AppsDomain))
            {
                // Only non Altinn App subscriptions require the additional scope
                if (!_claimsPrincipalProvider.GetUser().HasRequiredScope(AuthorizationConstants.SCOPE_EVENTS_SUBSCRIBE))
                {
                    return Forbid();
                }

                isAppSubscription = false;
            }

            Subscription eventsSubscription = _mapper.Map<Subscription>(eventsSubscriptionRequest);

            (Subscription createdSubscription, ServiceError error) = isAppSubscription ?
                await _eventsSubscriptionService.CreateAppSubscription(eventsSubscription) :
                await _eventsSubscriptionService.CreateGenericSubscription(eventsSubscription);

            if (error != null)
            {
                return StatusCode(error.ErrorCode, error.ErrorMessage);
            }

            return Created("/events/api/v1/subscription/" + createdSubscription.Id, createdSubscription);
        }

        /// <summary>
        /// Get a specific subscription
        /// </summary>
        [Authorize]
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        public async Task<ActionResult<Subscription>> Get(int id)
        {
            (Subscription subscription, ServiceError error) = await _eventsSubscriptionService.GetSubscription(id);

            if (error != null)
            {
                return StatusCode(error.ErrorCode, error.ErrorMessage);
            }

            return Ok(subscription);
        }

        /// <summary>
        /// Get all subscription for the authorized consumer
        /// </summary>
        [Authorize]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Produces("application/json")]
        public async Task<ActionResult<SubscriptionList>> Get()
        {
            string consumer = GetConsumer();
            (List<Subscription> subscriptions, ServiceError error) = await _eventsSubscriptionService.GetAllSubscriptions(consumer);

            if (error != null)
            {
                return StatusCode(error.ErrorCode, error.ErrorMessage);
            }

            SubscriptionList list = new()
            {
                Count = subscriptions != null ? subscriptions.Count : 0,
                Subscriptions = subscriptions
            };

            return Ok(list);
        }

        /// <summary>
        /// Method to validate an specific subscription. Only avaiable from validation function.
        /// </summary>
        [Authorize(Policy = "PlatformAccess")]
        [HttpPut("validate/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        public async Task<ActionResult<Subscription>> Validate(int id)
        {
            (Subscription subscription, ServiceError error) = await _eventsSubscriptionService.SetValidSubscription(id);

            if (error != null)
            {
                return StatusCode(error.ErrorCode, error.ErrorMessage);
            }

            return Ok(subscription);
        }

        /// <summary>
        /// Delete a given subscription
        /// </summary>
        [Authorize]
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        public async Task<ActionResult> Delete(int id)
        {
            var error = await _eventsSubscriptionService.DeleteSubscription(id);

            if (error != null)
            {
                return StatusCode(error.ErrorCode, error.ErrorMessage);
            }

            return Ok();
        }

        private string GetConsumer()
        {
            var user = HttpContext.User;

            string authenticatedConsumer = string.Empty;

            if (!string.IsNullOrEmpty(user.GetOrg()))
            {
                authenticatedConsumer = OrgPrefix + user.GetOrg();
            }
            else if (user.GetUserIdAsInt().HasValue)
            {
                authenticatedConsumer = UserPrefix + user.GetUserIdAsInt().Value;
            }

            return authenticatedConsumer;
        }
    }
}
