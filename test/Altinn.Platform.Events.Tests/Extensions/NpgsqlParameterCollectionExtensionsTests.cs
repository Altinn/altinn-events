#nullable enable

using Altinn.Platform.Events.Extensions;

using Npgsql;
using System;
using Xunit;

namespace Altinn.Platform.Events.Tests.Extensions;

public class NpgsqlParameterCollectionExtensionsTests
{
    [Fact]
    public void AddWithNullableString_ValueIsNotNull_ParameterValueMatchInput()
    {
        // Arrange
        NpgsqlCommand command = new(); // Can't construct a parameter collection directly.

        const string ParameterName = "parameterName";
        const string ParameterValue = "test";

        // Act
        command.Parameters.AddWithNullableString(ParameterName, ParameterValue);

        // Assert
        Assert.Equal(ParameterValue, command.Parameters[ParameterName].Value);
    }

    [Fact]
    public void AddWithNullableString_ValueIsNull_ParameterValueIsDBNull()
    {
        // Arrange
        NpgsqlCommand command = new(); // Can't construct a parameter collection directly.

        const string ParameterName = "parameterName";
        const string? ParameterValue = null;

        // Act
        command.Parameters.AddWithNullableString(ParameterName, ParameterValue);

        // Assert
        Assert.Equal(DBNull.Value, command.Parameters[ParameterName].Value);
    }
}
