using System;

using Altinn.Platform.Events.Extensions;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingExtensions
{
    public class UriExtensionsTests
    {
        [Theory]
        [InlineData("https://ttd.apps.altinn.cloud/ttd/apps-test", "E9F8E9ED2A9DABC8123FFCF1B14AE6A8")]
        [InlineData("https://ttd.apps.altinn.cloud/ttd/apps-test-v2", "1D53F5D431A0A84FA0DF31AB0E06672C")]
        [InlineData("https://ttd.apps.altinn.cloud/ttd/apps-test/-v2", "AF00680EAB3ABA2A595D5C6B308B384C")]

        public void MD5HashUri(string uri, string expected)
        {
            var actual = UriExtensions.MD5HashUri(new Uri(uri));

            Assert.Equal(expected, actual);
        }
    }
}
