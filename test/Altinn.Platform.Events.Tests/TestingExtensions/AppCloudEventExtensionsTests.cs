using System.Linq;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;

using CloudNative.CloudEvents;

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
                Type = "app.instance.created"
            };

            string expectedAltSubject = "/person/14029112345";

            // Act
            var actual = AppCloudEventExtensions.CreateEvent(requestModel);
            string actualValue = actual.GetPopulatedAttributes().Where(att => att.Key.Name == "alternativesubject").Select(att => att.Value.ToString()).First();

            // Asseert
            Assert.NotNull(actual.Id);
            Assert.NotNull(actual.Time);
            Assert.Single(actual.ExtensionAttributes);
            Assert.Equal(expectedAltSubject, actualValue);
        }

        [Fact]
        public void CreateEvent_AppEventWithoutAltSubject_NonExistingPropsSetByMethod()
        {
            // Arrange
            AppCloudEventRequestModel requestModel = new()
            {
                Type = "app.instance.created",
                Source = new System.Uri("https://vg.no"),
                Subject = "/party/50000001"
            };

            string expextedSpecVersion = "1.0";

            // Act
            var actual = AppCloudEventExtensions.CreateEvent(requestModel);

            // Asseert
            Assert.Empty(actual.ExtensionAttributes);
            Assert.NotNull(actual.Id);
            Assert.NotNull(actual.Time);
            Assert.Equal(expextedSpecVersion, actual.SpecVersion.VersionId);
        }
    }
}
