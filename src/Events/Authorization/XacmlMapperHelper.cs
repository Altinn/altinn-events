#nullable enable

using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;
using Altinn.Platform.Events.Extensions;

namespace Altinn.Platform.Events.Authorization;

/// <summary>
/// Shared methods for mapping to xacml
/// </summary>
public static class XacmlMapperHelper
{
    private const string DefaultIssuer = "Altinn";

    private const string UserPrefix = "/user/";
    private const string OrgPrefix = "/org/";
    private const string PartyPrefix = "/party/";
    private const string OrganisationPrefix = "/organisation/";
    private const string SystemUserPrefix = "/systemuser/";

    private const string ClaimUserId = "urn:altinn:userid";
    private const string ClaimOrg = "urn:altinn:org";
    private const string ClaimPartyID = "urn:altinn:partyid";
    private const string ClaimSystemUserId = "urn:altinn:systemuser";

    private const string ClaimOrganizationNumber = "urn:altinn:organization:identifier-no";
    private const string ClaimIdentitySeparator = ":";

    /// <summary>
    /// Adds attribute to the XacmlJsonCateogry from the provided subject string
    /// </summary>
    public static XacmlJsonCategory AddSubjectAttribute(this XacmlJsonCategory xacmlCategory, string subject)
    {
        if (string.IsNullOrEmpty(subject))
        {
            return xacmlCategory;
        }

        xacmlCategory.Attribute ??= [];

        if (subject.StartsWith(UserPrefix))
        {
            string value = subject.Replace(UserPrefix, string.Empty);
            xacmlCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimUserId, value, ClaimValueTypes.String, DefaultIssuer));
        }
        else if (subject.StartsWith(OrgPrefix))
        {
            string value = subject.Replace(OrgPrefix, string.Empty);
            xacmlCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimOrg, value, ClaimValueTypes.String, DefaultIssuer));
        }
        else if (subject.StartsWith(PartyPrefix))
        {
            string value = subject.Replace(PartyPrefix, string.Empty);
            xacmlCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimPartyID, value, ClaimValueTypes.Integer, DefaultIssuer));
        }
        else if (subject.StartsWith(OrganisationPrefix))
        {
            string value = subject.Replace(OrganisationPrefix, string.Empty);
            xacmlCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimOrganizationNumber, value, ClaimValueTypes.String, DefaultIssuer));
        }
        else if (subject.StartsWith(SystemUserPrefix))
        {
            string value = subject.Replace(SystemUserPrefix, string.Empty);
            xacmlCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimSystemUserId, value, ClaimValueTypes.String, DefaultIssuer));
        }
        else if (UriExtensions.IsValidUrn(subject))
        {
            xacmlCategory.AddAttributeFromUrn(subject);
        }

        return xacmlCategory;
    }

    /// <summary>
    /// Adds a new json attribute to a provided xacml category instance based of a urn.
    /// </summary>
    /// <remarks>
    /// URN is split at the last colon (`:`) of the string an first part is used as the
    /// attribute type and last part is used as the value.
    /// Given an invalid URN or empty string the category is left unmodified.
    /// </remarks>
    /// <param name="xacmlCategory">The XACML category to be populated</param>
    /// <param name="urn">The urn to retrieve attribute type and value from</param>
    public static void AddAttributeFromUrn(this XacmlJsonCategory xacmlCategory, string urn)
    {
        if (string.IsNullOrEmpty(urn))
        {
            return;
        }

        string[] typeAndValue = urn.Split(ClaimIdentitySeparator);

        if (typeAndValue.Length < 2)
        {
            return;
        }

        var value = typeAndValue[^1];
        var type = string.Join(ClaimIdentitySeparator, typeAndValue[..^1]);

        xacmlCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(type, value, CloudEventXacmlMapper.DefaultType, CloudEventXacmlMapper.DefaultIssuer));
    }

    /// <summary>
    /// Splits the resource attribute at the final ':'
    /// </summary>
    /// <param name="resource">The resource string</param>
    /// <returns>A tuple conaining the attribute id and attribute value</returns>
    /// <remarks>
    /// First entry should be used as the attribute id in the xacml request
    /// Second entry should be used as the atttribute value in the xaml request
    /// For an Altinn App resource second entry is on format altinnapp.{org}.{app}
    /// </remarks>
    public static (string AttributeId, string AttributeValue) SplitResourceInTwoParts(string resource)
    {
        int index = resource.LastIndexOf(ClaimIdentitySeparator);
        string id = resource.Substring(0, index);
        string value = resource.Substring(index + 1);

        return (id, value);
    }
}
