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
        public void CreateAppSubscriptionRequesWithSubjectForOrg()
        {
            // Arrange
            Subscription subscription = new()
            {
                EndPoint = new Uri("https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                SourceFilter = new Uri("https://ttd.apps.altinn.no/ttd/apps-test"),
                ResourceFilter = "urn:altinn:resource:app_ttd_apps-test",
                AlternativeSubjectFilter = "/organisation/897069650",
                SubjectFilter = "/party/500000",
                TypeFilter = "app.instance.process.completed",
                Consumer = "/org/ttd"
            };

            // Act
            XacmlJsonRequestRoot xacmlJsonProfile = SubscriptionXacmlMapper.CreateDecisionRequest(subscription);
            int actualResourceAttCount = xacmlJsonProfile.Request.Resource.First().Attribute.Count;

            // Assert
            int expectedResourceAttCount = 5;

            Assert.NotNull(xacmlJsonProfile);
            Assert.Single(xacmlJsonProfile.Request.Resource);
            Assert.Equal(expectedResourceAttCount, actualResourceAttCount);

            string actualAppResource = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:appresource").Select(a => a.Value).First();
            Assert.Equal("events", actualAppResource);

            string actualOrgId = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:org").Select(a => a.Value).First();
            Assert.Equal("ttd", actualOrgId);

            string actualAppId = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:app").Select(a => a.Value).First();
            Assert.Equal("apps-test", actualAppId);

            string actualPartyId = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:partyid").Select(a => a.Value).First();
            Assert.Equal("500000", actualPartyId);

            string actualResource = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:resource").Select(a => a.Value).First();
            Assert.Equal("altinnapp.ttd.apps-test", actualResource);

            Assert.Single(xacmlJsonProfile.Request.Action);
            Assert.Equal("read", xacmlJsonProfile.Request.Action.First().Attribute.First().Value);

            Assert.Single(xacmlJsonProfile.Request.AccessSubject);
            string actualSubjectValue = xacmlJsonProfile.Request.AccessSubject.First().Attribute.Where(a => a.AttributeId == "urn:altinn:org").Select(a => a.Value).First();
            Assert.Equal("ttd", actualSubjectValue);
        }

        /// <summary>
        /// Test creation of XACML request for events subscription. Org subject.
        /// </summary>
        [Fact]
        public void CreateSubscriptionRequesWithSubjectForOrg()
        {
            // Arrange
            Subscription subscription = new()
            {
                EndPoint = new Uri("https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                ResourceFilter = "urn:altinn:resource:automated-tests",
                AlternativeSubjectFilter = "/organisation/897069650",
                TypeFilter = "random.event.type",
                Consumer = "/org/ttd"
            };

            // Act
            XacmlJsonRequestRoot xacmlJsonProfile = SubscriptionXacmlMapper.CreateDecisionRequest(subscription);
            int actualResourceAttCount = xacmlJsonProfile.Request.Resource.First().Attribute.Count;

            // Assert
            int expectedResourceAttCount = 1;

            Assert.NotNull(xacmlJsonProfile);
            Assert.Single(xacmlJsonProfile.Request.Resource);
            Assert.Equal(expectedResourceAttCount, actualResourceAttCount);

            string actualResource = xacmlJsonProfile.Request.Resource.First().Attribute.Where(a => a.AttributeId == "urn:altinn:resource").Select(a => a.Value).First();
            Assert.Equal("automated-tests", actualResource);

            Assert.Single(xacmlJsonProfile.Request.Action);
            Assert.Equal("subscribe", xacmlJsonProfile.Request.Action.First().Attribute.First().Value);

            Assert.Single(xacmlJsonProfile.Request.AccessSubject);
            string actualSubjectValue = xacmlJsonProfile.Request.AccessSubject.First().Attribute.Where(a => a.AttributeId == "urn:altinn:org").Select(a => a.Value).First();
            Assert.Equal("ttd", actualSubjectValue);
        }

        /// <summary>
        /// Test creation of XACML request for events subscription. User subject.
        /// </summary>
        [Fact]
        public void CreateAppSubscriptionRequesWithSubjectForUser()
        {
            // Arrange
            Subscription subscription = new()
            {
                EndPoint = new Uri("https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                ResourceFilter = "urn:altinn:resource:app_ttd_apps-test",
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
