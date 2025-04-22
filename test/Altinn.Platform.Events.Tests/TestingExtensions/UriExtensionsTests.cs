using System;

using Altinn.Platform.Events.Extensions;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingExtensions
{
    public class UriExtensionsTests
    {
        [Theory]
        [InlineData("https://ttd.apps.altinn.cloud/ttd/apps-test", true)]
        [InlineData("https://ttd.apps.altinn.cloud/ttd/apps-test-v2/", true)]
        [InlineData("urn:namespaceid:ttd:apps:apps-test", true)]
        [InlineData("urn:uuid:6e8bc430-9c3a-11d9-9669-0800200c9a66", true)]
        [InlineData("telnet://ole:qwerty@altinn.no:45432/", false)]
        [InlineData("http://vg.no", false)]
        public void IsValidUrlOrUrn(string uri, bool expected)
        {
            bool actual = UriExtensions.IsValidUrlOrUrn(new Uri(uri));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("urn:namespaceid:ttd:apps:apps-test", true)]
        [InlineData("urn:uuid:6e8bc430-9c3a-11d9-9669-0800200c9a66", true)]
        [InlineData("  urn:namespaceid:ttd:apps:apps-test", false)]
        [InlineData("urn:foo::::", false)]
        public void IsValidUrn(string urn, bool expected)
        {
            bool actual = UriExtensions.IsValidUrn(urn);

            Assert.Equal(expected, actual);
        }
    }
}
