#nullable enable

using System;

using Npgsql;

namespace Altinn.Platform.Events.Extensions;

/// <summary>
/// This class contains a set of extension methods for the <see cref="NpgsqlParameterCollection"/> class.
/// </summary>
public static class NpgsqlParameterCollectionExtensions
{
    /// <summary>
    /// Add a new parameter to the collection. Provide it with the given value if defined. 
    /// If value is null give the parameter <c>DBNull.Value</c> instead.
    /// </summary>
    /// <param name="collection">The <see cref="NpgsqlParameterCollection"/> to add a new parameter to.</param>
    /// <param name="parameterName">The name of the new parameter to be added.</param>
    /// <param name="value">The nullable string value to be given to the parameter.</param>
    /// <returns>The created <see cref="NpgsqlParameter"/> with given name and appropriate value.</returns>
    public static NpgsqlParameter AddWithNullableString(
        this NpgsqlParameterCollection collection, string parameterName, string? value)
    {
        if (value is not null)
        {
            return collection.AddWithValue(parameterName, value);
        }

        return collection.AddWithValue(parameterName, DBNull.Value);
    }
}
