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
            Assert.Single(xacmlJsonProfile.Request.Action);
            Assert.Single(xacmlJsonProfile.Request.AccessSubject);
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
            int actualResourceAttCount = xacmlJsonProfile.Request.Resource.First().Attribute.Count;

            // Assert
            int expectedResourceAttCount = 4;

            Assert.NotNull(xacmlJsonProfile);
            Assert.Single(xacmlJsonProfile.Request.Resource);
            Assert.Equal(expectedResourceAttCount, actualResourceAttCount);
            Assert.Single(xacmlJsonProfile.Request.Action);
            Assert.Single(xacmlJsonProfile.Request.AccessSubject);
        }
    }
}
