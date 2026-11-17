using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Platform.Events.Models;

/// <summary>
/// Input model with the query parameters and headers shared by the app event search endpoints.
/// </summary>
public class AppEventsQueryModel
{
    /// <summary>
    /// Retrieve events registered after the event with this id. The event itself is not included.
    /// Use the next link from the previous response to fetch the next page.
    /// Either after or from must be specified.
    /// </summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    [FromQuery(Name = "after")]
    public string After { get; set; }

    /// <summary>
    /// Include events whose time is at or after this value. The value must include a timezone,
    /// e.g. 2022-02-14T07:22:19Z. Either from or after must be specified.
    /// </summary>
    /// <example>2022-02-14 07:22:19Z</example>
    [FromQuery(Name = "from")]
    public DateTime? From { get; set; }

    /// <summary>
    /// Include events whose time is at or before this value. The value must include a timezone,
    /// e.g. 2022-06-16T13:37:00Z.
    /// </summary>
    /// <example>2022-06-16 13:37:00Z</example>
    [FromQuery(Name = "to")]
    public DateTime? To { get; set; }

    /// <summary>
    /// Search for app events where the subject is the specified party id. This parameter 
    /// is mutually exclusive with unit and person.
    /// </summary>
    /// <example>50002108</example>
    [FromQuery(Name = "party")]
    public int Party { get; set; }

    /// <summary>
    /// Search for app events where the subject is the party id of the specified organization.
    /// The value should be a valid organization number. This parameter is mutually exclusive
    /// with party and person.
    /// </summary>
    /// <example>989271156</example>
    [FromQuery(Name = "unit")]
    public string Unit { get; set; }

    /// <summary>
    /// Search for app events where the subject is the party id of the specified person.
    /// The value should be a valid national identity number. This parameter is mutually exclusive
    /// with party and unit.
    /// </summary>
    /// <example>01014922047</example>
    [FromHeader(Name = "person")]
    public string Person { get; set; }

    /// <summary>
    /// Search for app events where the event type is one of the types specified in the list.
    /// An empty list will include all event types.
    /// </summary>
    /// <example>["app.instance.created", "app.instance.process.completed"]</example>
    [FromQuery(Name = "type")]
    public List<string> Type { get; set; }

    /// <summary>
    /// The maximum number of events to return. Defaults to 50. Must be 1 or larger.
    /// </summary>
    [FromQuery(Name = "size")]
    public int Size { get; set; } = 50;
}
