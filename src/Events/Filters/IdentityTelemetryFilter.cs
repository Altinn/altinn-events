#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Altinn.AccessManagement.Core.Models;
using Altinn.Platform.Events.Extensions;

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;

namespace Altinn.Platform.Events.Filters;

/// <summary>
/// Filter to enrich request telemetry with identity information.
/// </summary>
[ExcludeFromCodeCoverage]
public class IdentityTelemetryFilter : ITelemetryProcessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITelemetryProcessor _nextTelemetryProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityTelemetryFilter"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
    /// <param name="nextTelemetryProcessor">The next telemetry processor in the chain.</param>
    public IdentityTelemetryFilter(IHttpContextAccessor httpContextAccessor, ITelemetryProcessor nextTelemetryProcessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _nextTelemetryProcessor = nextTelemetryProcessor;
    }

    /// <inheritdoc />
    public void Process(ITelemetry item)
    {
        if (item is RequestTelemetry requestTelemetry && IsRelevantTelemetry(requestTelemetry))
        {
            EnrichRequestTelemetry(requestTelemetry);
        }

        _nextTelemetryProcessor.Process(item);
    }

    /// <summary>
    /// Determines whether the given request telemetry is relevant based on its URL.
    /// </summary>
    /// <param name="requestTelemetry">The request telemetry to evaluate.</param>
    /// <returns><c>true</c> if the telemetry is relevant; otherwise, <c>false</c>.</returns>
    private static bool IsRelevantTelemetry(RequestTelemetry requestTelemetry)
    {
        return requestTelemetry?.Url?.ToString().Contains("events/api/") == true;
    }

    /// <summary>
    /// Enriches the request telemetry with user information from the current HTTP context.
    /// </summary>
    /// <param name="requestTelemetry">The request telemetry to enrich.</param>
    private void EnrichRequestTelemetry(RequestTelemetry requestTelemetry)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User == null)
        {
            return;
        }

        AddTelemetryProperties(requestTelemetry, context);
    }

    /// <summary>
    /// Adds relevant user properties to the request telemetry.
    /// </summary>
    /// <param name="requestTelemetry">The request telemetry to which properties will be added.</param>
    /// <param name="context">The current HTTP context containing user information.</param>
    private static void AddTelemetryProperties(RequestTelemetry requestTelemetry, HttpContext context)
    {
        var user = context.User;

        AddProperty(requestTelemetry, "partyId", user.GetPartyId());
        AddProperty(requestTelemetry, "authLevel", user.GetAuthenticationLevel());

        string? userId = user.GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            AddProperty(requestTelemetry, "userId", userId);
        }

        string? organizationNUmber = user.GetOrganizationNumber();
        if (!string.IsNullOrWhiteSpace(organizationNUmber))
        {
            AddProperty(requestTelemetry, "orgNumber", organizationNUmber);
        }

        var systemUser = user.GetSystemUser();
        if (systemUser != null)
        {
            AddProperty(requestTelemetry, "systemUserId", GetSystemUserId(systemUser));
            AddProperty(requestTelemetry, "systemUserOrgId", systemUser.Systemuser_org.ID);
        }
    }

    /// <summary>
    /// Retrieves the identifier of the system user from the system user's claims.
    /// </summary>
    /// <param name="systemUser">The <see cref="SystemUserClaim"/> instance representing the system user.</param>
    /// <returns>The identifier of the system user if the claim exists; otherwise, <c>null</c>.</returns>
    private static string? GetSystemUserId(SystemUserClaim systemUser)
    {
        if (systemUser is null)
        {
            return null;
        }

        if (systemUser.Systemuser_id == null)
        {
            return null;
        }

        if (systemUser.Systemuser_id.Count == 0)
        {
            return null;
        }

        return Convert.ToString(systemUser.Systemuser_id[0]);
    }

    /// <summary>
    /// Adds a property to the request telemetry if the value is not null or empty.
    /// </summary>
    /// <param name="telemetry">The request telemetry to which the property will be added.</param>
    /// <param name="key">The key of the property to add.</param>
    /// <param name="value">The value of the property to add.</param>
    private static void AddProperty(RequestTelemetry telemetry, string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        telemetry.Properties[key] = value;
    }
}
