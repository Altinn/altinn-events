using System;
using System.Collections.Generic;
using System.Security.Claims;

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
    /// Test creation of single event XACML event.
    /// </summary>
    [Fact]
    public void CreateSingleEventRequest()
    {
        // Arrange
        ClaimsPrincipal principal = GetPrincipal(1, 1);

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
        Assert.Single(xacmlJsonProfile.Request.Resource);
        Assert.Single(xacmlJsonProfile.Request.Action);
        Assert.Single(xacmlJsonProfile.Request.AccessSubject);
    }

    /// <summary>
    /// Test creation of one request.
    /// </summary>
    [Fact]
    public void CreateSingleEventRequestForConsumer()
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
        Assert.Single(xacmlJsonProfile.Request.Resource);
        Assert.Single(xacmlJsonProfile.Request.Action);
        Assert.Single(xacmlJsonProfile.Request.AccessSubject);
    }

    /// <summary>
    /// Creates a ClaimsPrincipal for testing purposes.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="partyId">The party identifier.</param>
    /// <returns>A <see cref="ClaimsPrincipal"/> object.</returns>
    private static ClaimsPrincipal GetPrincipal(int userId, int partyId)
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
}
