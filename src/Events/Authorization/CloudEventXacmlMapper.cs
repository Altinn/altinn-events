using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;

using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Platform.Events.Authorization
{
    /// <summary>
    /// Utility class for converting Events to XACML request
    /// </summary>
    public static class CloudEventXacmlMapper
    {
        /// <summary>
        /// Default issuer for attributes
        /// </summary>
        internal const string DefaultIssuer = "Altinn";

        /// <summary>
        /// Default type for attributes
        /// </summary>
        internal const string DefaultType = "string";

        /// <summary>
        /// Subject id for multi requests. Inde should be appended.
        /// </summary>
        internal const string SubjectId = "s";

        /// <summary>
        /// Action id for multi requests. Inde should be appended.
        /// </summary>
        internal const string ActionId = "a";

        /// <summary>
        /// Resource id for multi requests. Inde should be appended.
        /// </summary>
        internal const string ResourceId = "r";

        /// <summary>
        /// Create XACML request for multiple resources and multiple actions using the claims principal user as the only subject.
        /// </summary>
        internal static XacmlJsonRequestRoot CreateMultiDecisionRequest(ClaimsPrincipal user, List<string> actionTypes, List<XacmlJsonCategory> resourceCategory)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            XacmlJsonRequest request = new()
            {
                AccessSubject = new List<XacmlJsonCategory>(),
                Action = CreateMultipleActionCategory(actionTypes),
                Resource = resourceCategory
            };

            request.AccessSubject.Add(CreateMultipleSubjectCategory(user.Claims));
            request.MultiRequests = CreateMultiRequestsCategory(request.AccessSubject, request.Action, request.Resource);

            XacmlJsonRequestRoot jsonRequest = new() { Request = request };

            return jsonRequest;
        }

        /// <summary>
        /// Creates an action category with the porovided action type as an attribute
        /// </summary>
        /// <param name="actionType">Action type represented as a string</param>
        /// <param name="includeResult">A value indicating whether the value should be included in the result</param>
        /// <returns>A XacmlJsonCategory</returns>
        internal static XacmlJsonCategory CreateActionCategory(string actionType, bool includeResult = false)
        {
            XacmlJsonCategory actionAttributes = new()
            {
                Attribute = new List<XacmlJsonAttribute>
                {
                    DecisionHelper.CreateXacmlJsonAttribute(MatchAttributeIdentifiers.ActionId, actionType, DefaultType, DefaultIssuer, includeResult)
                }
            };
            return actionAttributes;
        }

        /// <summary>
        /// Creates multiple request by creating requests for all combinations of the provided subjects, actions and resources.
        /// </summary>
        internal static XacmlJsonMultiRequests CreateMultiRequestsCategory(List<XacmlJsonCategory> subjects, List<XacmlJsonCategory> actions, List<XacmlJsonCategory> resources)
        {
            List<string> subjectIds = subjects.Select(s => s.Id).ToList();
            List<string> actionIds = actions.Select(a => a.Id).ToList();
            List<string> resourceIds = resources.Select(r => r.Id).ToList();

            XacmlJsonMultiRequests multiRequests = new()
            {
                RequestReference = CreateRequestReference(subjectIds, actionIds, resourceIds)
            };

            return multiRequests;
        }

        /// <summary>
        /// Creates multiple action category containing all provided action types.
        /// </summary>
        internal static List<XacmlJsonCategory> CreateMultipleActionCategory(List<string> actionTypes)
        {
            List<XacmlJsonCategory> actionCategories = new();
            int counter = 1;

            foreach (string actionType in actionTypes)
            {
                XacmlJsonCategory actionCategory;
                actionCategory = DecisionHelper.CreateActionCategory(actionType, true);
                actionCategory.Id = ActionId + counter.ToString();
                actionCategories.Add(actionCategory);
                counter++;
            }

            return actionCategories;
        }

        /// <summary>
        /// Creates multiple subject category containing a single subject based on provided claims.
        /// </summary>
        /// <remarks>
        /// Only Altinn-supported claims will be included as attributes.
        /// </remarks>
        internal static XacmlJsonCategory CreateMultipleSubjectCategory(IEnumerable<Claim> claims)
        {
            XacmlJsonCategory subjectAttributes = DecisionHelper.CreateSubjectCategory(claims);
            subjectAttributes.Id = SubjectId + "1";

            return subjectAttributes;
        }

        private static List<XacmlJsonRequestReference> CreateRequestReference(List<string> subjectIds, List<string> actionIds, List<string> resourceIds)
        {
            List<XacmlJsonRequestReference> references = new();

            foreach (string resourceId in resourceIds)
            {
                foreach (string actionId in actionIds)
                {
                    foreach (string subjectId in subjectIds)
                    {
                        XacmlJsonRequestReference reference = new();
                        List<string> referenceId = new()
                        {
                            subjectId,
                            actionId,
                            resourceId
                        };
                        reference.ReferenceId = referenceId;
                        references.Add(reference);
                    }
                }
            }

            return references;
        }
    }
}
