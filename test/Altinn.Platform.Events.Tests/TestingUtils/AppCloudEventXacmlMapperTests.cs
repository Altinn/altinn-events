using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;

using Altinn.AccessManagement.Core.Models;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Platform.Events.Authorization;

using AltinnCore.Authentication.Constants;

using CloudNative.CloudEvents;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingUtils;

/// <summary>
/// Tests for the AppCloudEventXacmlMapper class.
/// </summary>
public class AppCloudEventXacmlMapperTests
{
    /// <summary>
    /// Verifies that a multi-decision XACML request can be created for a regular user.
    /// </summary>
    [Fact]
    public void CreateMultiDecisionReadRequest_ShouldReturnValidXacmlRequest_ForRegularUser()
    {
        // Arrange
        ClaimsPrincipal principal = GetUserClaimPrincipal(1, 1);

        List<CloudEvent> cloudEvents = [];
        CloudEvent cloudEvent = new()
        {
            Source = new Uri("https://skd.apps.altinn.no/skd/skattemelding/instances/1234324/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
            Subject = "/party/1234324"
        };

        cloudEvents.Add(cloudEvent);

        // Act
        XacmlJsonRequestRoot xacmlJsonProfile = AppCloudEventXacmlMapper.CreateMultiDecisionReadRequest(principal, cloudEvents);

        // Assert.
        Assert.NotNull(xacmlJsonProfile);
        Assert.Single(xacmlJsonProfile.Request.Action);
        Assert.Single(xacmlJsonProfile.Request.Resource);
        Assert.Single(xacmlJsonProfile.Request.AccessSubject);
    }

    /// <summary>
    /// Verifies that a multi-decision XACML request can be created for a system user.
    /// </summary>
    [Fact]
    public void CreateMultiDecisionReadRequest_ShouldReturnValidXacmlRequest_ForSystemUser()
    {
        // Arrange
        ClaimsPrincipal principal = GetSystemUserPrincipal("system_id_mock", Convert.ToString(Guid.NewGuid()), "org_cliam_id_mock");

        List<CloudEvent> cloudEvents = [];
        CloudEvent cloudEvent = new()
        {
            Source = new Uri("https://skd.apps.altinn.no/skd/skattemelding/instances/1234324/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
            Subject = "/party/1234324"
        };

        cloudEvents.Add(cloudEvent);

        // Act
        XacmlJsonRequestRoot xacmlJsonProfile = AppCloudEventXacmlMapper.CreateMultiDecisionReadRequest(principal, cloudEvents);

        // Assert.
        Assert.NotNull(xacmlJsonProfile);
        Assert.Single(xacmlJsonProfile.Request.Action);
        Assert.Single(xacmlJsonProfile.Request.Resource);
        Assert.Single(xacmlJsonProfile.Request.AccessSubject);
    }

    /// <summary>
    /// Verifies that a single XACML decision request can be created for a consumer.
    /// </summary>
    [Fact]
    public void CreateDecisionRequest_ShouldReturnValidXacmlRequest_ForConsumer()
    {
        CloudEvent cloudEvent = new()
        {
            Source = new Uri("https://skd.apps.altinn.no/skd/skattemelding/instances/1234324/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
            Subject = "/party/1234324"
        };

        // Act
        XacmlJsonRequestRoot xacmlJsonProfile = AppCloudEventXacmlMapper.CreateDecisionRequest(cloudEvent, "/party/2");

        // Assert.
        Assert.NotNull(xacmlJsonProfile);
        Assert.Single(xacmlJsonProfile.Request.Action);
        Assert.Single(xacmlJsonProfile.Request.Resource);
        Assert.Single(xacmlJsonProfile.Request.AccessSubject);
    }

    /// <summary>
    /// Creates a ClaimsPrincipal object represents a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="partyId">The party identifier.</param>
    /// <returns>A <see cref="ClaimsPrincipal"/> object.</returns>
    private static ClaimsPrincipal GetUserClaimPrincipal(int userId, int partyId)
    {
        string issuer = "www.altinn.no";

        List<Claim> claims =
        [
            new Claim(AltinnCoreClaimTypes.UserId, userId.ToString(), ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.UserName, "UserOne", ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.PartyID, partyId.ToString(), ClaimValueTypes.Integer32, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "2", ClaimValueTypes.Integer32, issuer),
        ];

        ClaimsIdentity identity = new("mock");

        identity.AddClaims(claims);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a ClaimsPrincipal object represents a system user.
    /// </summary>
    /// <param name="systemId">The system identifier.</param>
    /// <param name="systemUserId">The system user identifier.</param>
    /// <param name="orgClaimId">The organization claim identifier.</param>
    /// <returns>A <see cref="ClaimsPrincipal"/> object representing the system user.</returns>
    private static ClaimsPrincipal GetSystemUserPrincipal(string systemId, string systemUserId, string orgClaimId)
    {
        string issuer = "www.altinn.no";

        var systemUserClaim = new SystemUserClaim
        {
            System_id = systemId,
            Systemuser_id = [systemUserId],
            Systemuser_org = new OrgClaim
            {
                ID = orgClaimId
            }
        };

        List<Claim> claims =
        [
            new Claim(AltinnCoreClaimTypes.UserId, systemUserId, ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.UserName, "systemUser", ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer),
            new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "3", ClaimValueTypes.Integer32, issuer),
            new Claim("authorization_details", JsonSerializer.Serialize(systemUserClaim), ClaimValueTypes.String, issuer),
        ];

        var identity = new ClaimsIdentity(claims, "mock");

        identity.AddClaims(claims);

        return new ClaimsPrincipal(identity);
    }
}
