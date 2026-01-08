#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ABAC.Constants;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;

using Altinn.Common.PEP.Interfaces;

using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Utils;
using Altinn.Platform.Events.UnitTest.Mocks;

using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices;

/// <summary>
/// Tests to verify authorization helper
/// </summary>
public class AuthorizationServiceTest
{
    private readonly CloudEvent _cloudEvent;
    private readonly Mock<IClaimsPrincipalProvider> _principalMock = new();
    private readonly Mock<IRegisterService> _registerServiceMock = new();

    public AuthorizationServiceTest()
    {
        _cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
        {
            Id = Guid.NewGuid().ToString(),
            Type = "system.event.occurred",
            Subject = "/party/1337",
            Source = new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd")
        };
        _cloudEvent["resource"] = "urn:altinn:resource:ttd.endring-av-navn-v2";

        _principalMock
               .Setup(p => p.GetUser())
               .Returns(PrincipalUtil.GetClaimsPrincipal(12345, 3));
    }

    /// <summary>
    /// Test access to own event
    /// </summary>
    [Fact]
    public async Task AuthorizeConsumerForAltinnAppEvent_Self()
    {
        PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
        AuthorizationService authzHelper = 
            new(pdp, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        // Act
        var result = await authzHelper.AuthorizeMultipleConsumersForAltinnAppEvent(_cloudEvent, ["/user/1337"]);

        // Assert
        Assert.Single(result);
        Assert.True(result["/user/1337"]);
    }

    /// <summary>
    /// Test access to own event
    /// </summary>
    [Fact]
    public async Task AuthorizeConsumerForAltinnAppEvent_OrgAccessToEventForUser()
    {
        PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
        AuthorizationService authzHelper = 
            new AuthorizationService(pdp, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        // Act
        var result = await authzHelper.AuthorizeMultipleConsumersForAltinnAppEvent(_cloudEvent, ["/org/ttd"]);

        // Assert
        Assert.Single(result);
        Assert.True(result["/org/ttd"]);
    }

    /// <summary>
    /// Test access to event for user for org.Not authorized
    /// </summary>
    [Fact]
    public async Task AuthorizeConsumerForAltinnAppEvent_OrgAccessToEventForUserNotAuthorized()
    {
        PepWithPDPAuthorizationMockSI pdp = new PepWithPDPAuthorizationMockSI();
        AuthorizationService authzHelper = 
            new AuthorizationService(pdp, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        CloudEvent cloudEvent = new()
        {
            Source = new Uri("https://skd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
            Subject = "/party/1337"
        };

        // Act
        var result = await authzHelper.AuthorizeMultipleConsumersForAltinnAppEvent(cloudEvent, ["/org/nav"]);

        // Assert.
        Assert.Single(result);
        Assert.False(result["/org/nav"]);
    }

    [Fact]
    public async Task AuthorizeConsumerForGenericEvent_PdpIsCalledWithXacmlRequestRoot()
    {
        // Arrange
        Mock<IPDP> pdpMock = new();
        pdpMock
            .Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .ReturnsAsync(new XacmlJsonResponse
            {
                Response =
                [
                    new XacmlJsonResult
                    {
                        Decision = "Permit",
                        Category =
                        [
                            new XacmlJsonCategory
                            {
                                CategoryId = XacmlConstants.MatchAttributeCategory.Subject,
                                Attribute =
                                [
                                    new XacmlJsonAttribute
                                    {
                                        AttributeId = "urn:altinn:org",
                                        Value = "ttd"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

        // Act
        var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        var result = await sut.AuthorizeMultipleConsumersForGenericEvent(_cloudEvent, ["/org/ttd"], CancellationToken.None);

        // Assert
        pdpMock.VerifyAll();
        Assert.Single(result);
        Assert.True(result["/org/ttd"]);
    }

    [Fact]
    public async Task AuthorizeConsumerForGenericEvent_EventWithUnsupportedSubject_LogicPerformSubjectReplace()
    {
        // Arrange
        CloudEvent cloudEvent = 
            GetCloudEvent("e7c581bc-e931-46c8-bfc0-3c6716d8da15", "urn:altinn:person:identifier-no:18874198354");

        PartiesRegisterQueryResponse partiesRegisterQueryResponse = 
            await TestDataLoader.Load<PartiesRegisterQueryResponse>("oneperson");
        
        Mock<IRegisterService> registerMock = new();
        registerMock.Setup(r => r.PartyLookup(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<string> requestedUrnList, CancellationToken cancellationToken) =>
            {
                Assert.Single(requestedUrnList);
                Assert.Equal("urn:altinn:person:identifier-no:18874198354", requestedUrnList.ElementAt(0));
            })
            .ReturnsAsync([partiesRegisterQueryResponse.Data[0]]);

        // Create a mock XACML response that includes the subject category with org attribute
        XacmlJsonResponse decisionResponse = new()
        {
            Response =
            [
                new XacmlJsonResult
                {
                    Decision = "Permit",
                    Category = new List<XacmlJsonCategory>
                    {
                        new() 
                        {
                            CategoryId = XacmlConstants.MatchAttributeCategory.Subject,
                            Attribute = new List<XacmlJsonAttribute>
                            {
                                new XacmlJsonAttribute
                                {
                                    AttributeId = "urn:altinn:org",
                                    Value = "ttd"
                                }
                            }
                        },
                        new() 
                        {
                            CategoryId = XacmlConstants.MatchAttributeCategory.Resource,
                            Attribute = new List<XacmlJsonAttribute>
                            {
                                new XacmlJsonAttribute
                                {
                                    AttributeId = "urn:altinn:party:uuid",
                                    Value = "4a80af94-14be-4af5-9f95-a6a0824c5b55"
                                }
                            }
                        }
                    }
                }
            ]
        };

        Mock<IPDP> pdpMock = new();
        pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Callback((XacmlJsonRequestRoot authRequest) =>
            {
                List<XacmlJsonCategory> resources = authRequest.Request.Resource;

                Assert.Single(resources);

                Assert.Equal("urn:altinn:party:uuid", resources[0].Attribute[4].AttributeId);
                Assert.Equal("4a80af94-14be-4af5-9f95-a6a0824c5b55", resources[0].Attribute[4].Value);
            })
            .ReturnsAsync(decisionResponse);

        // Act
        AuthorizationService sut = new(pdpMock.Object, _principalMock.Object, registerMock.Object, NullLogger<AuthorizationService>.Instance);

        var result = await sut.AuthorizeMultipleConsumersForGenericEvent(cloudEvent, ["/org/ttd"], CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.True(result["/org/ttd"]);

        // Check that the cloud event is back to the original subject
        Assert.Equal("urn:altinn:person:identifier-no:18874198354", cloudEvent.Subject);
    }

    [Fact]
    public async Task FilterAuthorizedRequests_PermitAll()
    {
        // Arrange
        ClaimsPrincipal consumer = PrincipalUtil.GetClaimsPrincipal(12345, 3);
        List<CloudEvent> cloudEvents = TestdataUtil.GetCloudEventList();
        XacmlJsonResponse response = await TestDataLoader.Load<XacmlJsonResponse>("permit_read_all");

        // Act
        List<CloudEvent> actual = AuthorizationService.FilterAuthorizedRequests(cloudEvents, consumer, response);

        // Assert
        Assert.NotEmpty(actual);
        Assert.Equal(2, actual.Count);
    }

    [Fact]
    public async Task FilterAuthorizedRequests_PermitOne()
    {
        // Arrange
        ClaimsPrincipal consumer = PrincipalUtil.GetClaimsPrincipal(12345, 3);
        List<CloudEvent> cloudEvents = TestdataUtil.GetCloudEventList();
        XacmlJsonResponse response = await TestDataLoader.Load<XacmlJsonResponse>("permit_read_one");

        // Act
        List<CloudEvent> actual = AuthorizationService.FilterAuthorizedRequests(cloudEvents, consumer, response);

        // Assert
        Assert.Single(actual);
    }

    [Fact]
    public async Task FilterAuthorizedRequests_PermitAll_SystemUser()
    {
        // Arrange
        XacmlJsonResponse response = await TestDataLoader.Load<XacmlJsonResponse>("permit_read_all");
        List<CloudEvent> cloudEvents = TestdataUtil.GetCloudEventList();
        ClaimsPrincipal consumer = PrincipalUtil.GetSystemUserPrincipal("system_identifier", "9485C31D-6ECE-477F-B81C-1E2593FC1309", "random_org", 3);

        // Act
        List<CloudEvent> actual = AuthorizationService.FilterAuthorizedRequests(cloudEvents, consumer, response);

        // Assert
        Assert.NotEmpty(actual);
        Assert.Equal(2, actual.Count);
    }

    [Fact]
    public async Task AuthorizePublishEvent_PermitResponse_ReturnsTrue()
    {
        // Arrange
        Mock<IPDP> pdpMock = GetPDPMockWithRespose("Permit");

        // Act
        var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        bool actual = await sut.AuthorizePublishEvent(_cloudEvent, CancellationToken.None);

        // Assert
        pdpMock.VerifyAll();
        Assert.True(actual);
    }

    [Fact]
    public async Task AuthorizePublishEvent_DenyResponse_ReturnsFalse()
    {
        // Arrange
        Mock<IPDP> pdpMock = GetPDPMockWithRespose("Deny");

        // Act
        var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        bool actual = await sut.AuthorizePublishEvent(_cloudEvent, CancellationToken.None);

        // Assert
        pdpMock.VerifyAll();
        Assert.False(actual);
    }

    [Fact]
    public async Task AuthorizePublishEvent_IndeterminateResponse_ReturnsFalse()
    {
        // Arrange
        Mock<IPDP> pdpMock = GetPDPMockWithRespose("Indeterminate");

        // Act
        var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        bool actual = await sut.AuthorizePublishEvent(_cloudEvent, CancellationToken.None);

        // Assert
        Assert.False(actual);
    }

    [Fact]
    public async Task AuthorizePublishEvent_CloudEventWithUnsupportedSubject_SubjectIsReplacedAndRestored()
    {
        // Arrange
        CloudEvent cloudEvent =
            GetCloudEvent("e7c581bc-e931-46c8-bfc0-3c6716d8da15", "urn:altinn:person:identifier-no:18874198354");

        PartiesRegisterQueryResponse partiesRegisterQueryResponse =
            await TestDataLoader.Load<PartiesRegisterQueryResponse>("oneperson");

        Mock<IRegisterService> registerMock = new();
        registerMock.Setup(r => r.PartyLookup(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<string> requestedUrnList, CancellationToken cancellationToken) =>
            {
                Assert.Single(requestedUrnList);
                Assert.Equal("urn:altinn:person:identifier-no:18874198354", requestedUrnList.ElementAt(0));
            })
            .ReturnsAsync([partiesRegisterQueryResponse.Data[0]]);

        XacmlJsonResponse decisionResponse = await TestDataLoader.Load<XacmlJsonResponse>("permit_publish_one");

        Mock<IPDP> pdpMock = new();
        pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Callback((XacmlJsonRequestRoot authRequest) =>
            {
                List<XacmlJsonCategory> resources = authRequest.Request.Resource;

                Assert.Single(resources);

                Assert.Equal("urn:altinn:party:uuid", resources[0].Attribute[4].AttributeId);
                Assert.Equal("4a80af94-14be-4af5-9f95-a6a0824c5b55", resources[0].Attribute[4].Value);
            })
            .ReturnsAsync(decisionResponse);

        // Act
        AuthorizationService sut = new(pdpMock.Object, _principalMock.Object, registerMock.Object, NullLogger<AuthorizationService>.Instance);

        bool actual = await sut.AuthorizePublishEvent(cloudEvent, CancellationToken.None);

        // Assert
        Assert.True(actual);

        // Check that the cloud event is back to the original subject
        Assert.Equal("urn:altinn:person:identifier-no:18874198354", cloudEvent.Subject);
    }

    [Fact]
    public async Task AuthorizePublishEventWithAdminScope_IndeterminateResponse_ReturnsTrue()
    {
        // Arrange
        Mock<IPDP> pdpMock = GetPDPMockWithRespose("Indeterminate");
        Mock<IClaimsPrincipalProvider> principalMock = new();
        principalMock
            .Setup(p => p.GetUser())
            .Returns(PrincipalUtil.GetClaimsPrincipal("digdir", "912345678", "altinn:events.publish.admin", "AuthenticationTypes.Federation"));

        // Act
        var sut = new AuthorizationService(pdpMock.Object, principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        bool actual = await sut.AuthorizePublishEvent(_cloudEvent, CancellationToken.None);

        // Assert
        Assert.True(actual);
    }

    [Fact]
    public async Task AuthorizePublishEventWithFakeAdminScope_IndeterminateResponse_ReturnsFalse()
    {
        // Arrange
        Mock<IPDP> pdpMock = GetPDPMockWithRespose("Indeterminate");
        Mock<IClaimsPrincipalProvider> principalMock = new();
        principalMock
            .Setup(p => p.GetUser())
            .Returns(PrincipalUtil.GetClaimsPrincipal("digdir", "912345678", "somerandomprefix:altinn:events.publish.admin", "AuthenticationTypes.Federation"));

        // Act
        var sut = new AuthorizationService(pdpMock.Object, principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        bool actual = await sut.AuthorizePublishEvent(_cloudEvent, CancellationToken.None);

        // Assert
        Assert.False(actual);
    }

    [Fact]
    public async Task AuthorizeEvents_InputEventsFiveSubjects_LogicManipulateSubjectCorrectly()
    {
        List<CloudEvent> cloudEvents = [
            GetCloudEvent("e7c581bc-e931-46c8-bfc0-3c6716d8da15", "urn:altinn:person:identifier-no:02056241046"),
            GetCloudEvent("ef14212c-0f9d-4f88-89c5-255d946e5f18", "urn:altinn:organization:identifier-no:312508729"),
            GetCloudEvent("29168d79-b081-4299-9eec-2db9b5259fac", "urn:altinn:person:identifier-no:31073102351"),
            GetCloudEvent("bf1411fe-c55e-4472-9f37-10e2c2403ecb", "urn:altinn:person:identifier-no:31073102351"),
            GetCloudEvent("95d53441-5ecf-4165-83d8-a757afd8a7e2", "urn:altinn:person:identifier-no:notfound"),
            GetCloudEvent("a7c7b7bf-3151-4de8-b326-aeaa5c36e7c4", null)];

        List<PartyIdentifiers> partyIdentifiers =
            (await TestDataLoader.Load<PartiesRegisterQueryResponse>("twopersons")).Data;

        Mock<IRegisterService> registerMock = new();
        registerMock.Setup(r => r.PartyLookup(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<string> requestedUrnList, CancellationToken cancellationToken) =>
            {
                Assert.Equal(3, requestedUrnList.Count());
                Assert.Equal("urn:altinn:person:identifier-no:02056241046", requestedUrnList.ElementAt(0));
                Assert.Equal("urn:altinn:person:identifier-no:31073102351", requestedUrnList.ElementAt(1));
                Assert.Equal("urn:altinn:person:identifier-no:notfound", requestedUrnList.ElementAt(2));
            })
            .ReturnsAsync(partyIdentifiers);

        XacmlJsonResponse decisionResponse = await TestDataLoader.Load<XacmlJsonResponse>("permit_subscribe_five");

        Mock<IPDP> pdpMock = new();
        pdpMock
            .Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Callback((XacmlJsonRequestRoot authRequest) =>
            {
                List<XacmlJsonCategory> resources = authRequest.Request.Resource;

                // Analyze the generated resources with focus on the manipulated subjects
                Assert.Equal(6, resources.Count);

                Assert.Equal("urn:altinn:party:uuid", resources[0].Attribute[4].AttributeId);
                Assert.Equal("8be970b6-b361-42c6-9b53-5cd3ab5efbe9", resources[0].Attribute[4].Value);

                Assert.Equal("urn:altinn:organization:identifier-no", resources[1].Attribute[4].AttributeId);
                Assert.Equal("312508729", resources[1].Attribute[4].Value);

                Assert.Equal("urn:altinn:party:uuid", resources[2].Attribute[4].AttributeId);
                Assert.Equal("930423fe-8bbd-43f0-bf4c-3ddf5a5eb4e7", resources[2].Attribute[4].Value);

                Assert.Equal("urn:altinn:party:uuid", resources[3].Attribute[4].AttributeId);
                Assert.Equal("930423fe-8bbd-43f0-bf4c-3ddf5a5eb4e7", resources[3].Attribute[4].Value);

                Assert.Equal(4, resources[4].Attribute.Count); // No party identifier found
                Assert.Equal(4, resources[5].Attribute.Count); // No subject
            })
            .ReturnsAsync(decisionResponse);

        AuthorizationService target = new(pdpMock.Object, _principalMock.Object, registerMock.Object, NullLogger<AuthorizationService>.Instance);

        // Act
        List<CloudEvent> finalCloudEvents = await target.AuthorizeEvents(cloudEvents, CancellationToken.None);

        Assert.NotNull(finalCloudEvents);

        Assert.Equal(5, finalCloudEvents.Count); // The last event was not authorized
        Assert.Equal("urn:altinn:person:identifier-no:02056241046", finalCloudEvents[0].Subject);
        Assert.Null(finalCloudEvents[0]["originalsubjectreplacedforauthorization"]);

        Assert.Equal("urn:altinn:organization:identifier-no:312508729", finalCloudEvents[1].Subject);
        Assert.Null(finalCloudEvents[1]["originalsubjectreplacedforauthorization"]);

        Assert.Equal("urn:altinn:person:identifier-no:31073102351", finalCloudEvents[2].Subject);
        Assert.Null(finalCloudEvents[2]["originalsubjectreplacedforauthorization"]);

        Assert.Equal("urn:altinn:person:identifier-no:31073102351", finalCloudEvents[3].Subject);
        Assert.Null(finalCloudEvents[3]["originalsubjectreplacedforauthorization"]);

        Assert.Equal("urn:altinn:person:identifier-no:notfound", finalCloudEvents[4].Subject);
        Assert.Null(finalCloudEvents[4]["originalsubjectreplacedforauthorization"]);
    }

    [Fact]
    public async Task ExtractConsumerFromResult_PartyIdAttribute_ExtractsCorrectly()
    {
        // Arrange
        CloudEvent appEvent = new(CloudEventsSpecVersion.V1_0)
        {
            Id = Guid.NewGuid().ToString(),
            Type = "test.event",
            Subject = "/party/50001234",
            Source = new Uri("https://ttd.apps.altinn.no/ttd/test-app/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd")
        };
        appEvent["resource"] = "urn:altinn:resource:app_ttd_test-app";

        Mock<IPDP> pdpMock = new();
        pdpMock
            .Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .ReturnsAsync(new XacmlJsonResponse
            {
                Response =
                [
                    new XacmlJsonResult
                    {
                        Decision = "Permit",
                        Category =
                        [
                            new XacmlJsonCategory
                            {
                                CategoryId = XacmlConstants.MatchAttributeCategory.Subject,
                                Attribute =
                                [
                                    new XacmlJsonAttribute
                                    {
                                        AttributeId = "urn:altinn:partyid",
                                        Value = "50001234"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

        var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        // Act
        var result = await sut.AuthorizeMultipleConsumersForAltinnAppEvent(appEvent, ["/party/50001234"]);

        // Assert
        Assert.Single(result);
        Assert.Contains("/party/50001234", result.Keys);
        Assert.True(result["/party/50001234"]);
    }

    [Fact]
    public async Task ExtractConsumerFromResult_OrgAttribute_ExtractsCorrectly()
    {
        // Arrange
        Mock<IPDP> pdpMock = new();
        pdpMock
            .Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .ReturnsAsync(new XacmlJsonResponse
            {
                Response =
                [
                    new XacmlJsonResult
                    {
                        Decision = "Permit",
                        Category =
                        [
                            new XacmlJsonCategory
                            {
                                CategoryId = XacmlConstants.MatchAttributeCategory.Subject,
                                Attribute =
                                [
                                    new XacmlJsonAttribute
                                    {
                                        AttributeId = "urn:altinn:org",
                                        Value = "ttd"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

        var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        // Act
        var result = await sut.AuthorizeMultipleConsumersForAltinnAppEvent(_cloudEvent, ["/org/ttd"]);

        // Assert
        Assert.Single(result);
        Assert.Contains("/org/ttd", result.Keys);
        Assert.True(result["/org/ttd"]);
    }

    [Fact]
    public async Task ExtractConsumerFromResult_UserIdAttribute_ExtractsCorrectly()
    {
        // Arrange
        Mock<IPDP> pdpMock = new();
        pdpMock
            .Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .ReturnsAsync(new XacmlJsonResponse
            {
                Response =
                [
                    new XacmlJsonResult
                    {
                        Decision = "Permit",
                        Category =
                        [
                            new XacmlJsonCategory
                            {
                                CategoryId = XacmlConstants.MatchAttributeCategory.Subject,
                                Attribute =
                                [
                                    new XacmlJsonAttribute
                                    {
                                        AttributeId = "urn:altinn:userid",
                                        Value = "12345"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

        var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        // Act
        var result = await sut.AuthorizeMultipleConsumersForAltinnAppEvent(_cloudEvent, ["/user/12345"]);

        // Assert
        Assert.Single(result);
        Assert.Contains("/user/12345", result.Keys);
        Assert.True(result["/user/12345"]);
    }

    [Fact]
    public async Task ExtractConsumerFromResult_SystemUserAttribute_ExtractsCorrectly()
    {
        // Arrange
        var systemUserUuid = Guid.NewGuid().ToString();
        Mock<IPDP> pdpMock = new();
        pdpMock
            .Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .ReturnsAsync(new XacmlJsonResponse
            {
                Response =
                [
                    new XacmlJsonResult
                    {
                        Decision = "Permit",
                        Category =
                        [
                            new XacmlJsonCategory
                            {
                                CategoryId = XacmlConstants.MatchAttributeCategory.Subject,
                                Attribute =
                                [
                                    new XacmlJsonAttribute
                                    {
                                        AttributeId = "urn:altinn:systemuser:uuid",
                                        Value = systemUserUuid
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

        var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        // Act
        var result = await sut.AuthorizeMultipleConsumersForAltinnAppEvent(_cloudEvent, [$"/systemuser/{systemUserUuid}"]);

        // Assert
        Assert.Single(result);
        Assert.Contains($"/systemuser/{systemUserUuid}", result.Keys);
        Assert.True(result[$"/systemuser/{systemUserUuid}"]);
    }

    [Fact]
    public async Task ExtractConsumerFromResult_OrganizationIdentifierAttribute_ExtractsCorrectly()
    {
        // Arrange
        CloudEvent genericEvent = new(CloudEventsSpecVersion.V1_0)
        {
            Id = Guid.NewGuid().ToString(),
            Type = "test.event",
            Subject = "/organisation/312345678",
            Source = new Uri("https://example.com/event")
        };
        genericEvent["resource"] = "urn:altinn:resource:test-resource";

        Mock<IPDP> pdpMock = new();
        pdpMock
            .Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .ReturnsAsync(new XacmlJsonResponse
            {
                Response =
                [
                    new XacmlJsonResult
                    {
                        Decision = "Permit",
                        Category =
                        [
                            new XacmlJsonCategory
                            {
                                CategoryId = XacmlConstants.MatchAttributeCategory.Subject,
                                Attribute =
                                [
                                    new XacmlJsonAttribute
                                    {
                                        AttributeId = "urn:altinn:organization:identifier-no",
                                        Value = "312345678"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

        var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        // Act
        var result = await sut.AuthorizeMultipleConsumersForGenericEvent(genericEvent, ["/organisation/312345678"], CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Contains("/organisation/312345678", result.Keys);
        Assert.True(result["/organisation/312345678"]);
    }

    [Fact]
    public async Task ExtractConsumerFromResult_MultipleConsumers_ExtractsAllCorrectly()
    {
        // Arrange
        Mock<IPDP> pdpMock = new();
        pdpMock
            .Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .ReturnsAsync(new XacmlJsonResponse
            {
                Response =
                [
                    new XacmlJsonResult
                    {
                        Decision = "Permit",
                        Category =
                        [
                            new XacmlJsonCategory
                            {
                                CategoryId = XacmlConstants.MatchAttributeCategory.Subject,
                                Attribute =
                                [
                                    new XacmlJsonAttribute
                                    {
                                        AttributeId = "urn:altinn:org",
                                        Value = "ttd"
                                    }
                                ]
                            }
                        ]
                    },
                    new XacmlJsonResult
                    {
                        Decision = "Deny",
                        Category =
                        [
                            new XacmlJsonCategory
                            {
                                CategoryId = XacmlConstants.MatchAttributeCategory.Subject,
                                Attribute =
                                [
                                    new XacmlJsonAttribute
                                    {
                                        AttributeId = "urn:altinn:org",
                                        Value = "nav"
                                    }
                                ]
                            }
                        ]
                    },
                    new XacmlJsonResult
                    {
                        Decision = "Permit",
                        Category =
                        [
                            new XacmlJsonCategory
                            {
                                CategoryId = XacmlConstants.MatchAttributeCategory.Subject,
                                Attribute =
                                [
                                    new XacmlJsonAttribute
                                    {
                                        AttributeId = "urn:altinn:partyid",
                                        Value = "50001234"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

        var sut = new AuthorizationService(pdpMock.Object, _principalMock.Object, _registerServiceMock.Object, NullLogger<AuthorizationService>.Instance);

        // Act
        var result = await sut.AuthorizeMultipleConsumersForAltinnAppEvent(_cloudEvent, ["/org/ttd", "/org/nav", "/party/50001234"]);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result["/org/ttd"]);
        Assert.False(result["/org/nav"]);
        Assert.True(result["/party/50001234"]);
    }

    private static CloudEvent GetCloudEvent(string eventId, string? subject)
    {
        CloudEvent cloudEvent = new(CloudEventsSpecVersion.V1_0)
        {
            Id = eventId,
            Type = "something.important.happened",
            Time = DateTime.Now,
            Subject = subject,
            Source = new Uri($"https://dialogporten.no/api/v1/dialogs/{Guid.NewGuid()}")
        };

        cloudEvent["resource"] = "urn:altinn:resource:super-simple-service";

        return cloudEvent;
    }

    private static Mock<IPDP> GetPDPMockWithRespose(string decision)
    {
        var pdpMock = new Mock<IPDP>();
        pdpMock
            .Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .ReturnsAsync(new XacmlJsonResponse
            {
                Response =
                [
                    new XacmlJsonResult
                    {
                        Decision = decision
                    }
                ]
            });

        return pdpMock;
    }
}
