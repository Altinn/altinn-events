using System.Linq;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingExtensions
{
    public class AppCloudEventExtensionsTests
    {
        [Fact]
        public void CreateEvent_AppEventWithAlternativeSubject_AlternativeSubjectIsPersisted()
        {
            // Arrange
            AppCloudEventRequestModel requestModel = new()
            {
                AlternativeSubject = "/person/14029112345",
                Type = "app.instance.created",
                Source = new System.Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/7806177e-5594-431b-8240-f173d92ed84d")
            };

            string expectedAltSubject = "/person/14029112345";

            // Act
            var actual = AppCloudEventExtensions.CreateEvent(requestModel);
            string actualValue = actual.GetPopulatedAttributes().Where(att => att.Key.Name == "alternativesubject").Select(att => att.Value.ToString()).First();

            // Asseert
            Assert.NotNull(actual.Id);
            Assert.NotNull(actual.Time);
            Assert.Equal(3, actual.ExtensionAttributes.Count());
            Assert.Equal(expectedAltSubject, actualValue);
        }

        [Fact]
        public void CreateEvent_AppEventWithoutAltSubject_ResourceAndResourceInstanceIsSet()
        {
            // Arrange
            AppCloudEventRequestModel requestModel = new()
            {
                Type = "app.instance.created",
                Source = new System.Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/7806177e-5594-431b-8240-f173d92ed84d"),
                Subject = "/party/50000001"
            };

            string expextedSpecVersion = "1.0";
            string expectedResource = "urn:altinn:resource:altinnapp.ttd.apps-test";
            string expectedResourceInstance = "50002108/7806177e-5594-431b-8240-f173d92ed84d";

            // Act
            var actual = AppCloudEventExtensions.CreateEvent(requestModel);
            string actualResourceValue = actual.GetPopulatedAttributes().Where(att => att.Key.Name == "resource").Select(att => att.Value.ToString()).First();
            string actualResourceInstanceValue = actual.GetPopulatedAttributes().Where(att => att.Key.Name == "resourceinstance").Select(att => att.Value.ToString()).First();

            // Asseert
            Assert.NotNull(actual.Id);
            Assert.NotNull(actual.Time);
            Assert.Equal(expextedSpecVersion, actual.SpecVersion.VersionId);
            Assert.Equal(expectedResource, actualResourceValue);
            Assert.Equal(expectedResourceInstance, actualResourceInstanceValue);
        }
    }
}
