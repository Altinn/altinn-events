using Microsoft.AspNetCore.Authorization;

namespace Altinn.Platform.Events.Authorization
{
    /// <summary>
    /// The requirement used to enable access token or publish scope verification
    /// </summary>
    public class PublishScopeOrAccessTokenRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PublishScopeOrAccessTokenRequirement"/> class.
        /// </summary>
        public PublishScopeOrAccessTokenRequirement()
        {
        }
    }
}
