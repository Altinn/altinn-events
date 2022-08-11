using System;
using System.Linq;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Platform.Events.Authorization;
using Altinn.Platform.Events.Models;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingUtils
{
    /// <summary>
    /// Test class for SubscriptionXacmlMapper
    /// </summary>
    public class SubscriptionXacmlMapperTests
    {
        /// <summary>
        /// Test creation of XACML request for events subscription. Org subject.
        /// </summary>
        [Fact]
        public void CreateSingleEventRequestUserForOrg()
        {
            // Arrange
            Subscription subscription = new()
            {
                EndPoint = new Uri("https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                SourceFilter = new Uri("https://ttd.apps.altinn.no/ttd/apps-test"),
                AlternativeSubjectFilter = "/org/897069650",
                SubjectFilter = "/party/500000",
                TypeFilter = "app.instance.process.completed",
                Consumer = "/user/1337"
            };

            // Act
            XacmlJsonRequestRoot xacmlJsonProfile = SubscriptionXacmlMapper.CreateDecisionRequest(subscription);
            int actualResourceAttCount = xacmlJsonProfile.Request.Resource.First().Attribute.Count;

            // Assert
            int expectedResourceAttCount = 4;

            Assert.NotNull(xacmlJsonProfile);
            Assert.Single(xacmlJsonProfile.Request.Resource);
            Assert.Equal(expectedResourceAttCount, actualResourceAttCount);

            string actualAppResource = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:appresource").Select(a => a.Value).First();
            Assert.Equal("events", actualAppResource);

            string actualOrgId = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:org").Select(a => a.Value).First();
            Assert.Equal("ttd", actualOrgId);

            string actualAppId = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:app").Select(a => a.Value).First();
            Assert.Equal("apps-test", actualAppId);

            string actualpartyId = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:partyid").Select(a => a.Value).First();
            Assert.Equal("500000", actualpartyId);

            Assert.Single(xacmlJsonProfile.Request.Action);
            Assert.Equal("read", xacmlJsonProfile.Request.Action.First().Attribute.First().Value);

            Assert.Single(xacmlJsonProfile.Request.AccessSubject);
            string actualSubjectValue = xacmlJsonProfile.Request.AccessSubject.First().Attribute.Where(a => a.AttributeId == "urn:altinn:userid").Select(a => a.Value).First();
            Assert.Equal("1337", actualSubjectValue);
        }

        /// <summary>
        /// Test creation of XACML request for events subscription. Person subject.
        /// </summary>
        [Fact]
        public void CreateSingleEventRequestUserForPerson()
        {
            // Arrange
            Subscription subscription = new()
            {
                EndPoint = new Uri("https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                SourceFilter = new Uri("https://ttd.apps.altinn.no/ttd/apps-test"),
                AlternativeSubjectFilter = "/person/01039012345",
                SubjectFilter = "/party/54321",
                TypeFilter = "app.instance.process.completed",
                Consumer = "/user/1337"
            };

            // Act
            XacmlJsonRequestRoot xacmlJsonProfile = SubscriptionXacmlMapper.CreateDecisionRequest(subscription);

            // Assert
            Assert.NotNull(xacmlJsonProfile);
            Assert.Single(xacmlJsonProfile.Request.Resource);
            string actualAppResource = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:appresource").Select(a => a.Value).First();
            Assert.Equal("events", actualAppResource);

            string actualOrgId = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:org").Select(a => a.Value).First();
            Assert.Equal("ttd", actualOrgId);

            string actualAppId = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:app").Select(a => a.Value).First();
            Assert.Equal("apps-test", actualAppId);

            string actualpartyId = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:partyid").Select(a => a.Value).First();
            Assert.Equal("54321", actualpartyId);

            Assert.Single(xacmlJsonProfile.Request.Action);
            Assert.Equal("read", xacmlJsonProfile.Request.Action.First().Attribute.First().Value);

            Assert.Single(xacmlJsonProfile.Request.AccessSubject);
            string actualSubjectValue = xacmlJsonProfile.Request.AccessSubject.First().Attribute.Where(a => a.AttributeId == "urn:altinn:userid").Select(a => a.Value).First();
            Assert.Equal("1337", actualSubjectValue);
        }
    }
}
