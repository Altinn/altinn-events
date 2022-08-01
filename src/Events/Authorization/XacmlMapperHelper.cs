using System.Collections.Generic;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;

namespace Altinn.Platform.Events.Authorization
{
    /// <summary>
    /// Shared methods for mapping to xacml
    /// </summary>
    public static class XacmlMapperHelper
    {
        private const string DefaultIssuer = "Altinn";

        private const string UserPrefix = "/user/";
        private const string OrgPrefix = "/org/";
        private const string PartyPrefix = "/party/";

        private const string ClaimUserId = "urn:altinn:userid";
        private const string ClaimPartyID = "urn:altinn:partyid";
        private const string ClaimOrg = "urn:altinn:org";

        /// <summary>
        /// Generates subject attribute list 
        /// </summary>
        /// <returns></returns>
        public static XacmlJsonCategory CreateSubjectAttributes(string subjectOrResource)
        {
            XacmlJsonCategory category = new()
            {
                Attribute = new List<XacmlJsonAttribute>()
            };

            if (subjectOrResource.StartsWith(UserPrefix))
            {
                string value = subjectOrResource.Replace(UserPrefix, string.Empty);
                category.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimUserId, value, ClaimValueTypes.String, DefaultIssuer));
            }
            else if (subjectOrResource.StartsWith(OrgPrefix))
            {
                string value = subjectOrResource.Replace(OrgPrefix, string.Empty);
                category.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimOrg, value, ClaimValueTypes.String, DefaultIssuer));
            }
            else if (subjectOrResource.StartsWith(PartyPrefix))
            {
                string value = subjectOrResource.Replace(PartyPrefix, string.Empty);
                category.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(ClaimPartyID, value, ClaimValueTypes.Integer, DefaultIssuer));
            }

            return category;
        }
    }
}
