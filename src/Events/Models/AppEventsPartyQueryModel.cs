using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Events.Models;

/// <summary>
/// Input model for retrieving app events related to a party.
/// </summary>
public class AppEventsPartyQueryModel
{
    /// <summary>
    /// Id of the latter event that should be included.
    /// </summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    [FromQuery(Name = "after")]
    public string After { get; set; }

    /// <summary>
    /// The lower limit for when the cloud event was created in UTC.
    /// </summary>
    /// <example>2022-02-14 07:22:19Z</example>
    [FromQuery(Name = "from")]
    public DateTime? From { get; set; }

    /// <summary>
    /// The upper limit for when the cloud event was created in UTC.
    /// </summary>
    /// <example>2022-06-16 13:37:00Z</example>
    [FromQuery(Name = "to")]
    public DateTime? To { get; set; }

    /// <summary>
    /// The party id representing the subjects.
    /// </summary>
    /// <example>50002108</example>
    [FromQuery(Name = "party")]
    public int Party { get; set; }

    /// <summary>
    /// The organisation number representing the subject.
    /// </summary>
    /// <example>989271156</example>
    [FromQuery(Name = "unit")]
    public string Unit { get; set; }

    /// <summary>
    /// The person number representing the subject.
    /// </summary>
    /// <example>01014922047</example>
    [FromHeader(Name = "person")]
    public string Person { get; set; }

    /// <summary>
    /// A list of the sources to include.
    /// </summary>
    /// <example>["https://ttd.apps.at22.altinn.cloud/ttd/apps-test/%", "https://ttd.apps.at22.altinn.cloud/digdir/bli-tjenesteeier/%"]</example>
    [FromQuery(Name = "source")]
    public List<string> Source { get; set; }

    /// <summary>
    /// A list of the event types to include.
    /// </summary>
    /// <example>["app.instance.created", "app.instance.process.completed"]</example>
    [FromQuery(Name = "type")]
    public List<string> Type { get; set; }

    /// <summary>
    /// The maximum number of events to include in the response.
    /// </summary>
    [FromQuery(Name = "size")]
    public int Size { get; set; } = 50;
}
