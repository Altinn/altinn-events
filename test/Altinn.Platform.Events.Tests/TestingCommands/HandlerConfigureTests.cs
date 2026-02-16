using System;

using Altinn.Platform.Events.Commands;
using Altinn.Platform.Events.Configuration;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingCommands;

/// <summary>
/// Tests for handler Configure methods to verify Settings null guard behavior.
/// </summary>
public class HandlerConfigureTests
{
    [Fact]
    public void SaveEventHandler_Configure_SettingsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var original = SaveEventHandler.Settings;
        SaveEventHandler.Settings = null;

        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => SaveEventHandler.Configure(null));
        }
        finally
        {
            SaveEventHandler.Settings = original;
        }
    }

    [Fact]
    public void SendToOutboundHandler_Configure_SettingsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var original = SendToOutboundHandler.Settings;
        SendToOutboundHandler.Settings = null;

        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => SendToOutboundHandler.Configure(null));
        }
        finally
        {
            SendToOutboundHandler.Settings = original;
        }
    }

    [Fact]
    public void SendEventToSubscriberHandler_Configure_SettingsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var original = SendEventToSubscriberHandler.Settings;
        SendEventToSubscriberHandler.Settings = null;

        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => SendEventToSubscriberHandler.Configure(null));
        }
        finally
        {
            SendEventToSubscriberHandler.Settings = original;
        }
    }

    [Fact]
    public void ValidateSubscriptionHandler_Configure_SettingsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var original = ValidateSubscriptionHandler.Settings;
        ValidateSubscriptionHandler.Settings = null;

        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ValidateSubscriptionHandler.Configure(null));
        }
        finally
        {
            ValidateSubscriptionHandler.Settings = original;
        }
    }
}
