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
    /// Retrieves the organization identifier from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The organization identifier if the claim exists; otherwise, <c>null</c>.</returns>
    public static string? GetOrganizationId(this ClaimsPrincipal user)
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
    /// Retrieves the organization number of the system user's owner from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The organization number of the system user's owner if the claim exists; otherwise, <c>null</c>.</returns>
    public static string? GetSystemUserOwner(this ClaimsPrincipal user)
    {
        SystemUserClaim? systemUser = user.GetSystemUser();
        if (systemUser is not null)
        {
            string consumerAuthority = systemUser.Systemuser_org.Authority;
            if (!"iso6523-actorid-upis".Equals(consumerAuthority))
            {
                return null;
            }

            string consumerId = systemUser.Systemuser_org.ID;

            string organisationNumber = consumerId.Split(":")[1];

            return organisationNumber;
        }

        return null;
    }

    /// <summary>
    /// Retrieves the identifier of the system user from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The identifier of the system user if the claim exists; otherwise, <c>null</c>.</returns>
    public static Guid? GetSystemUserId(this ClaimsPrincipal user)
    {
        SystemUserClaim? systemUser = user.GetSystemUser();

        if (systemUser is not null)
        {
            string systemUserId = systemUser.Systemuser_id[0];
            if (Guid.TryParse(systemUserId, out Guid systemUserIdGuid))
            {
                return systemUserIdGuid;
            }
        }

        return null;
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
