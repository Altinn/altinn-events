using System;
using System.Collections.Generic;
using System.Linq;

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
        [InlineData("urn:namespaceid:ttd:apps:apps-test", "C3DE88BE83CDA065F60C53867E7320A7")]
        [InlineData("urn:uuid:6e8bc430-9c3a-11d9-9669-0800200c9a66", "BB85C459B7C780AF57BF6417A491F70A")]
        public void MD5HashUri(string uri, string expected)
        {
            var actual = UriExtensions.GetMD5Hash(new Uri(uri));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("https://ttd.apps.altinn.cloud/ttd/apps-test/-v2", 4, "E9F8E9ED2A9DABC8123FFCF1B14AE6A8")]
        [InlineData("urn:uuid:6e8bc430-9c3a-11d9-9669-0800200c9a66", 2, "C30C0F37EDD5D9CBF36C03B16D963A32")]
        public void GetMD5HashSet(string uri, int expectedCount, string expectedContainingHash)
        {
            var actual = UriExtensions.GetMD5HashSets(new Uri(uri));

            Assert.Equal(expectedCount, actual.Count);
            Assert.Contains(expectedContainingHash, actual);
        }

        [Fact]
        public void GetMD5HashSet_URLProdivided()
        {
            // Arrange
            Uri uri = new Uri("https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/25724855-f16c-4f10-b988-4542929df1aa");

            List<string> expected = new()
            {
                "B868534C7B2118BEAA1E957B96C18D2E",
                "AAC207E1179BC85457BA05BE85E1BFEF",
                "03E4D9CA0902493533E9C62AB437EF50",
                "8023CB72C43BA908EC2A693295653D40",
                "2EEBAE2FB494AF1F956E47ABA1636BCF",
                "D71DDE2D3C3A1DC3E90325B90B6B028C"
            };

            // Act
            var actual = UriExtensions.GetMD5HashSets(uri);

            // Assert
            Assert.Empty(actual.Except(expected));
        }

        [Fact]
        public void GetMD5HashSet_URNProdivided()
        {
            // Arrange
            Uri uri = new Uri("urn:namespaceid:ttd:apps:apps-test.12345");

            List<string> expected = new()
            {
                "6D2953F62B3E589DD2A1968983BC25FD",
                "438FDB93E741601F01640685FDA90DCA",
                "6F20B102CBCDEB237C88E2ACCA00C858",
                "76BC13AE2483E7241CA7CD0E89924AF7"
            };

            // Act
            var actual = UriExtensions.GetMD5HashSets(uri);

            // Assert
            Assert.Empty(actual.Except(expected));
        }

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
    }
}