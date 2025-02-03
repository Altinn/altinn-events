#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;

using Altinn.AccessManagement.Core.Models;

using AltinnCore.Authentication.Constants;

namespace Altinn.Platform.Events.Extensions;

/// <summary>
/// Extension methods for <see cref="ClaimsPrincipal"/> instances.
/// </summary>
public static class ClaimsPrincipalExtensions
{
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

    /// <summary>
    /// Retrieves the authentication level from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The authentication level if the claim exists; otherwise, <c>null</c>.</returns>
    public static string? GetAuthenticationLevel(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(AltinnCoreClaimTypes.AuthenticationLevel);
    }

    /// <summary>
    /// Retrieves the organization from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The organization if the claim exists; otherwise, <c>null</c>.</returns>
    public static string? GetOrg(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(AltinnCoreClaimTypes.Org);
    }

    /// <summary>
    /// Retrieves the organization number from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The organization number if the claim exists; otherwise, <c>null</c>.</returns>
    public static string? GetOrganizationNumber(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(AltinnCoreClaimTypes.OrgNumber);
    }

    /// <summary>
    /// Retrieves the party identifier from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The party identifier if the claim exists; otherwise, <c>null</c>.</returns>
    public static string? GetPartyId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(AltinnCoreClaimTypes.PartyID);
    }

    /// <summary>
    /// Retrieves the identifier of the system user from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The identifier of the system user if the claim exists; otherwise, <c>null</c>.</returns>
    public static Guid? GetSystemUserId(this ClaimsPrincipal user)
    {
        SystemUserClaim? systemUser = user.GetSystemUser();

        if (systemUser is null)
        {
            return null;
        }

        if (systemUser.Systemuser_id == null)
        {
            return null;
        }

        if (systemUser.Systemuser_id.Count == 0)
        {
            return null;
        }

        return Guid.TryParse(systemUser.Systemuser_id[0], out Guid systemUserIdGuid) ? systemUserIdGuid : null;
    }

    /// <summary>
    /// Retrieves the system user from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The system user if the claim exists; otherwise, <c>null</c>.</returns>
    public static SystemUserClaim? GetSystemUser(this ClaimsPrincipal user)
    {
        string? authorizationDetailsValue = user.FindFirstValue("authorization_details");

        return authorizationDetailsValue is not null ? JsonSerializer.Deserialize<SystemUserClaim>(authorizationDetailsValue) : null;
    }

    /// <summary>
    /// Retrieves the user identifier from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The user identifier if the claim exists; otherwise, <c>null</c>.</returns>
    public static string? GetUserId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(AltinnCoreClaimTypes.UserId);
    }
}
