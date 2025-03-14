﻿#nullable enable

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Platform.Events.Authorization;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingUtils;

public class XacmlMapperHelperTests
{
    [Theory]
    [InlineData("/user/342345", "urn:altinn:userid", "342345")]
    [InlineData("/org/ttd", "urn:altinn:org", "ttd")]
    [InlineData("/party/532345", "urn:altinn:partyid", "532345")]
    [InlineData("/organisation/876765454", "urn:altinn:organization:identifier-no", "876765454")]
    [InlineData("/systemuser/systemUserName", "urn:altinn:systemuser:uuid", "systemUserName")]
    public void CreateSubjectAttributes_Assert_correct_attribute_id_and_value(string subject, string attributId, string value)
    {
        // Act
        XacmlJsonCategory actual = new XacmlJsonCategory().AddSubjectAttribute(subject);

        // Assert
        Assert.Single(actual.Attribute);
        Assert.Equal(attributId, actual.Attribute[0].AttributeId);
        Assert.Equal(value, actual.Attribute[0].Value);
    }
}
