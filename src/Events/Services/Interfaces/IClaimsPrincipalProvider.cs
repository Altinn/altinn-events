using System.Security.Claims;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Defines the methods required for an implementation of a user JSON Web Token provider.
    /// The provider is used by client implementations that needs the user token in requests 
    /// against other systems.
    /// </summary>
    public interface IClaimsPrincipalProvider
    {
        /// <summary>
        /// Defines a method that can return a claims principal for the current user.
        /// </summary>
        /// <returns>The Json Web Token for the current user.</returns>
        public ClaimsPrincipal GetUser();
    }
}
