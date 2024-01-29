using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Authorization
{
    /// <summary>
    /// Shared methods for mapping to xacml
    /// </summary>
    public static class XacmlMapperHelper
    {
        private const string DefaultIssuer = "Altinn";

        private const string UserPrefix = "/user/";
        private const string PersonPrefix = "/person/";
        private const string OrgPrefix = "/org/";
        private const string PartyPrefix = "/party/";

        private const string ClaimUserId = "urn:altinn:userid";
        private const string ClaimPartyID = "urn:altinn:partyid";
        private const string ClaimOrg = "urn:altinn:org";

        /// <summary>
        /// Generates subject attribute list 
        /// </summary>
        /// <returns>The XacmlJsonCategory or the subject</returns>
        public static XacmlJsonCategory CreateSubjectAttributes(string subject)
        {
            XacmlJsonCategory category = new()
            {
                Attribute = new List<XacmlJsonAttribute>()
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
        /// A recognized subject property value begins with one of the prefixes "/org/", "/person/" or "/party/", which
        /// is mapped to the appropriate attribute URNs. This will enable the PDP to enrich the request
        /// with roles etc. that the user has in the context of the CloudEvent subject (aka reportee). Note we do not
        /// attempt to lookup the partyId if given /org/ or /person/, as this is up to the PDP to do if the particular
        /// policy requires it (ie. it needs to check rules containing subject attributes for roles and/or access groups).
        ///
        /// Also note that this requires a XACML subject attribute that the PDP understands in order to look up the user's
        /// roles/access groups for that particular reportee, typically "urn:altinn:userid". This claim is present on all
        /// Altinn tokens, so it should be available in most cases, and will also in future Maskinporten-with-system-user
        /// tokens.  We do not check for this here though, as the PDP might add support for handling ie. urn:altinn:ssn
        /// attributes at some point.
        /// </remarks>
        /// <param name="cloudEvent">CloudEvent containing a subject representing a party (ie. reportee)</param>
        /// <param name="resourceCategory">The XACML resource category to be populated</param>
        public static void AddResourceReporteeAttributeFromCloudEventSubject(CloudEvent cloudEvent, XacmlJsonCategory resourceCategory)
        {
            if (string.IsNullOrEmpty(cloudEvent.Subject))
            {
                return;
            }

            string defaultType = CloudEventXacmlMapper.DefaultType;
            string defaultIssuer = CloudEventXacmlMapper.DefaultIssuer;

            (string subjectType, string subjectValue) = GetSubjectTypeAndValue(cloudEvent.Subject);
            if (subjectValue == null)
            {
                return;
            }

            switch (subjectType)
            {
                case UserPrefix:
                    // TODO! ClaimUserId Should be in AltinnXacmlUrns
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimUserId, subjectValue, defaultType, defaultIssuer));
                    break;
                case OrgPrefix:
                    // TODO! We probably shouldn't be using /org/ for both service owner identifiers and organization numbers
                    string claimValue = subjectValue.Length == 9 && subjectValue.All(char.IsAsciiDigit) ? AltinnXacmlUrns.OrganizationNumber : AltinnXacmlUrns.OrgId;
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(claimValue, subjectValue, defaultType, defaultIssuer));
                    break;
                case PersonPrefix:
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.Ssn, subjectValue, defaultType, defaultIssuer));
                    break;
                case PartyPrefix:
                    resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.PartyId, subjectValue, defaultType, defaultIssuer));
                    break;
            }
        }

        private static (string SubjectType, string SubjectValue) GetSubjectTypeAndValue(string subject)
        {
            string[] subjectParts = subject.Split('/');
            return subjectParts.Length != 3 ? (null, null) : ("/" + subjectParts[1] + "/", subjectParts[2]);
        }
    }
}
