using System;

namespace Altinn.Platform.Events.Extensions;

/// <summary>
/// Extension methods for DateTime
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Indicates if the time instance is based on either local time or UTC
    /// </summary>
    public static bool SpecifiesTimezone(this DateTime dateTime)
    {
        return dateTime.Kind != DateTimeKind.Unspecified;
    }
}
