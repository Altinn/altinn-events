using System;

namespace Altinn.Platform.Events.Extensions;

/// <summary>
/// Yes
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Yes
    /// </summary>
    public static bool SpecifiesTimezone(this DateTime dateTime)
    {
        return dateTime.Kind != DateTimeKind.Unspecified;
    }
}
