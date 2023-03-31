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
    public class PublishScopeOrAccessTokenHandler : AuthorizationHandler<PublishScopeOrAccessTokenRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishScopeOrAccessTokenHandler"/> class.
        /// </summary>
        public PublishScopeOrAccessTokenHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Handles verification of requirements. Publish scope requirement or AccesToken requirement must be satisfied.
        /// </summary>
        protected override async Task<object> HandleRequirementAsync(AuthorizationHandlerContext context, PublishScopeOrAccessTokenRequirement requirement)
        {
            var authorizationService = _httpContextAccessor.HttpContext.RequestServices.GetService<IAuthorizationService>();

            var policyCheck = await authorizationService.AuthorizeAsync(context.User, AuthorizationConstants.POLICY_PLATFORM_ACCESS);

            if (policyCheck.Succeeded)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
            
            var scopeCheck = await authorizationService.AuthorizeAsync(context.User, AuthorizationConstants.POLICY_SCOPE_EVENTS_PUBLISH);

            if (scopeCheck.Succeeded)
            {
                context.Succeed(requirement);
            }
            
            return Task.CompletedTask;
        }
    }
}
