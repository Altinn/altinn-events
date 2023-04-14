using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Platform.Events.Authorization
{
    /// <summary>
    /// Authorization handler for the <see cref="PublishScopeOrAccessTokenRequirement"/> class.
    /// </summary>
    public class PublishScopeHandler : AuthorizationHandler<PublishScopeOrAccessTokenRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishScopeHandler"/> class.
        /// </summary>
        public PublishScopeHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Handles verification of requirements. Publish scope requirement must be satisfied.
        /// </summary>
        protected override async Task<object> HandleRequirementAsync(AuthorizationHandlerContext context, PublishScopeOrAccessTokenRequirement requirement)
        {
            var authorizationService = _httpContextAccessor.HttpContext.RequestServices.GetService<IAuthorizationService>();
            
            var scopeCheck = await authorizationService.AuthorizeAsync(context.User, AuthorizationConstants.POLICY_SCOPE_EVENTS_PUBLISH);

            if (scopeCheck.Succeeded)
            {
                context.Succeed(requirement);
            }
            
            return Task.CompletedTask;
        }
    }
}
