using System.Security.Claims;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;
using CloudNative.CloudEvents;

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

    private const string ClaimUserId = "urn:altinn:userid";
    private const string ClaimOrg = "urn:altinn:org";
    private const string ClaimPartyID = "urn:altinn:partyid";

    // urn:altinn:organization:identifier-no is a value defined by Authorization so we need to use 'z' here.
    private const string ClaimOrganizationNumber = "urn:altinn:organization:identifier-no";
    private const string ClaimPersonNumber = "urn:altinn:person:identifier-no";
    private const string ClaimIdentitySeparator = "::";

    /// <summary>
    /// Generates subject attribute list
    /// </summary>
    /// <returns>The XacmlJsonCategory or the subject</returns>
    public static XacmlJsonCategory CreateSubjectAttributes(string subject)
    {
        XacmlJsonCategory category = new()
        {
            Attribute = []
        };

        if (subject.StartsWith(UserPrefix))
        {
            string value = subject.Replace(UserPrefix, string.Empty);
            category.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimUserId, value, ClaimValueTypes.String, DefaultIssuer));
        }
        else if (subject.StartsWith(OrgPrefix))
        {
            string value = subject.Replace(OrgPrefix, string.Empty);
            category.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimOrg, value, ClaimValueTypes.String, DefaultIssuer));
        }
        else if (subject.StartsWith(PartyPrefix))
        {
            string value = subject.Replace(PartyPrefix, string.Empty);
            category.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimPartyID, value, ClaimValueTypes.Integer, DefaultIssuer));
        }
        else if (subject.StartsWith(OrganisationPrefix))
        {
            string value = subject.Replace(OrganisationPrefix, string.Empty);
            category.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimOrganizationNumber, value, ClaimValueTypes.String, DefaultIssuer));
        }

        return category;
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
        int index = resource.LastIndexOf(':');
        string id = resource.Substring(0, index);
        string value = resource.Substring(index + 1);

        return (id, value);
    }

    /// <summary>
    /// Maps a subject in the provided CloudEvent to a XACML resource attribute in the provided resource category instance.
    /// </summary>
    /// <remarks>
    /// A recognized subject property value begins with one of the prefixes "urn:altinn:organization:identifier-no::",
    /// or "urn:altinn:person:identifier-no::", which is copied to the resource attribute with the corresponding URN.
    /// This will enable the PDP to enrich the request with roles etc. that the user has in the context of the CloudEvent
    /// subject (aka reportee).
    ///
    /// Also note that this requires a XACML subject attribute that the PDP understands in order to look up the user's
    /// roles/access groups for that particular reportee, eg. "urn:altinn:userid" or "urn:altinn:person:identifier-no".
    /// </remarks>
    /// <param name="cloudEvent">CloudEvent containing a subject representing a party (ie. reportee)</param>
    /// <param name="resourceCategory">The XACML resource category to be populated</param>
    public static void AddResourceReporteeAttributeFromCloudEventSubject(CloudEvent cloudEvent, XacmlJsonCategory resourceCategory)
    {
        if (string.IsNullOrEmpty(cloudEvent.Subject))
        {
            return;
        }

        string[] typeAndValue = cloudEvent.Subject.Split(ClaimIdentitySeparator);
        if (typeAndValue.Length != 2 || (typeAndValue[0] != ClaimOrganizationNumber && typeAndValue[0] != ClaimPersonNumber))
        {
            return;
        }

        resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(typeAndValue[0], typeAndValue[1], CloudEventXacmlMapper.DefaultType, CloudEventXacmlMapper.DefaultIssuer));
    }
}
