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
        /// Subject id for multi requests. Index should be appended.
        /// </summary>
        internal const string SubjectId = "s";

        /// <summary>
        /// Action id for multi requests. Index should be appended.
        /// </summary>
        internal const string ActionId = "a";

        /// <summary>
        /// Resource id for multi requests. Index should be appended.
        /// </summary>
        internal const string ResourceId = "r";

        /// <summary>
        /// Create XACML request for multiple resources and multiple actions using the claims principal user as the only subject.
        /// </summary>
        internal static XacmlJsonRequestRoot CreateMultiDecisionRequest(ClaimsPrincipal user, List<string> actionTypes, List<XacmlJsonCategory> resourceCategory)
        {
            ArgumentNullException.ThrowIfNull(user);

            XacmlJsonRequest request = new()
            {
                AccessSubject = [],
                Resource = resourceCategory,
                Action = CreateMultipleActionCategory(actionTypes)
            };

            request.AccessSubject.Add(CreateMultipleSubjectCategory(user.Claims));
            request.MultiRequests = CreateMultiRequestsCategory(request.AccessSubject, request.Action, request.Resource);

            XacmlJsonRequestRoot jsonRequest = new() { Request = request };

            return jsonRequest;
        }

        /// <summary>
        /// Creates an action category with the provided action type as an attribute.
        /// </summary>
        /// <param name="actionType">Action type represented as a string.</param>
        /// <param name="includeResult">A value indicating whether the value should be included in the result.</param>
        /// <returns>A XacmlJsonCategory object representing the action.</returns>
        internal static XacmlJsonCategory CreateActionCategory(string actionType, bool includeResult = false)
        {
            ArgumentNullException.ThrowIfNull(actionType);

            var actionAttributes = new XacmlJsonCategory
            {
                Attribute =
                [
                    DecisionHelper.CreateXacmlJsonAttribute(MatchAttributeIdentifiers.ActionId, actionType, DefaultType, DefaultIssuer, includeResult)
                ]
            };
            return actionAttributes;
        }

        /// <summary>
        /// Creates multiple requests by creating requests for all combinations of the provided subjects, actions, and resources.
        /// </summary>
        /// <param name="subjects">List of subject categories.</param>
        /// <param name="actions">List of action categories.</param>
        /// <param name="resources">List of resource categories.</param>
        /// <returns>A XacmlJsonMultiRequests object containing the request references.</returns>
        internal static XacmlJsonMultiRequests CreateMultiRequestsCategory(List<XacmlJsonCategory> subjects, List<XacmlJsonCategory> actions, List<XacmlJsonCategory> resources)
        {
            ArgumentNullException.ThrowIfNull(subjects);
            ArgumentNullException.ThrowIfNull(actions);
            ArgumentNullException.ThrowIfNull(resources);

            var actionIds = actions.Select(e => e.Id);
            var subjectIds = subjects.Select(e => e.Id);
            var resourceIds = resources.Select(e => e.Id);

            var multiRequests = new XacmlJsonMultiRequests
            {
                RequestReference = CreateRequestReference(subjectIds, actionIds, resourceIds).ToList()
            };

            return multiRequests;
        }

        /// <summary>
        /// Creates multiple action categories containing all provided action types.
        /// </summary>
        /// <param name="actionTypes">List of action types.</param>
        /// <returns>List of XacmlJsonCategory objects.</returns>
        internal static List<XacmlJsonCategory> CreateMultipleActionCategory(List<string> actionTypes)
        {
            var actionCategories = new List<XacmlJsonCategory>(actionTypes.Count);

            for (int i = 0; i < actionTypes.Count; i++)
            {
                var actionCategory = DecisionHelper.CreateActionCategory(actionTypes[i], true);
                actionCategory.Id = $"{ActionId}{i + 1}";
                actionCategories.Add(actionCategory);
            }

            return actionCategories;
        }

        /// <summary>
        /// Creates multiple subject categories containing a single subject based on provided claims.
        /// </summary>
        /// <remarks>
        /// Only Altinn-supported claims will be included as attributes.
        /// </remarks>
        /// <param name="claims">The claims to be included in the subject category.</param>
        /// <returns>A XacmlJsonCategory object representing the subject.</returns>
        internal static XacmlJsonCategory CreateMultipleSubjectCategory(IEnumerable<Claim> claims)
        {
            ArgumentNullException.ThrowIfNull(claims);

            var subjectAttributes = DecisionHelper.CreateSubjectCategory(claims);
            subjectAttributes.Id = $"{SubjectId}1";

            return subjectAttributes;
        }

        /// <summary>
        /// Creates request references for all combinations of the provided subject, action, and resource IDs.
        /// </summary>
        /// <param name="subjectIds">List of subject IDs.</param>
        /// <param name="actionIds">List of action IDs.</param>
        /// <param name="resourceIds">List of resource IDs.</param>
        /// <returns>List of XacmlJsonRequestReference objects.</returns>
        private static IEnumerable<XacmlJsonRequestReference> CreateRequestReference(IEnumerable<string> subjectIds, IEnumerable<string> actionIds, IEnumerable<string> resourceIds)
        {
            ArgumentNullException.ThrowIfNull(subjectIds);
            ArgumentNullException.ThrowIfNull(actionIds);
            ArgumentNullException.ThrowIfNull(resourceIds);

            var references = from resourceId in resourceIds
                             from actionId in actionIds
                             from subjectId in subjectIds
                             select new XacmlJsonRequestReference
                             {
                                 ReferenceId = [subjectId, actionId, resourceId]
                             };

            return references;
        }
    }
}
