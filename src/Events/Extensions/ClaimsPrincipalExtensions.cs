using System;
using System.Linq;
using System.Security.Claims;

using AltinnCore.Authentication.Constants;

namespace Altinn.Platorm.Events.Extensions;

/// <summary>
/// Extension methods for <see cref="ClaimsPrincipal"/> instances."/>
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Retrieves the organization identifier from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The organization identifier as a string if the claim exists; otherwise, null.</returns>
    public static string GetOrg(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(AltinnCoreClaimTypes.Org);
    }

    /// <summary>
    /// Retrieves the organization number from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The organization number as a string if the claim exists; otherwise, null.</returns>
    public static string GetOrgNumber(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(AltinnCoreClaimTypes.OrgNumber);
    }

    /// <summary>
    /// Retrieves the user identifier as an integer if the UserId claim is set; otherwise, null.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The user identifier as an integer if the claim exists and is a valid integer; otherwise, null.</returns>
    public static int? GetUserIdAsInt(this ClaimsPrincipal user)
    {
        string userIdValue = user.FindFirstValue(AltinnCoreClaimTypes.UserId);
        return int.TryParse(userIdValue, out int userId) ? userId : null;
    }


    /// <summary>
    /// Determines whether the specified required scope is present in the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <param name="requiredScope">The required scope to check for.</param>
    /// <returns><c>true</c> if the required scope is present; otherwise, <c>false</c>.</returns>
    public static bool HasRequiredScope(this ClaimsPrincipal user, string requiredScope)
    {
        var contextScopes = user.Identities?
            .FirstOrDefault(e => e.AuthenticationType != null && e.AuthenticationType.Equals("AuthenticationTypes.Federation"))?
            .Claims
            .Where(e => e.Type.Equals("urn:altinn:scope"))
            .Select(e => e.Value)
            .FirstOrDefault()?
            .Split(' ');

        contextScopes ??= user.Claims
            .Where(e => e.Type.Equals("scope"))
            .Select(e => e.Value)
            .FirstOrDefault()?
            .Split(' ');

        return contextScopes != null && contextScopes.Any(x => x.Equals(requiredScope, StringComparison.InvariantCultureIgnoreCase));
    }
}
