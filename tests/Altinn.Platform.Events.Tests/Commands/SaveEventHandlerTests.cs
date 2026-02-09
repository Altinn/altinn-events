using Altinn.Platform.Events.Commands;
using Altinn.Platform.Events.Contracts;
using Altinn.Platform.Events.Models;
using Xunit;

namespace Altinn.Platform.Events.Tests.Commands;

public class SaveEventHandlerTests
{
    [Fact]
    public void Handle_WithValidCommand_ProcessesSuccessfully()
    {
        // Arrange
        var registerEvent = new RegisterEvent
        {
            Id = Guid.NewGuid().ToString(),
            Source = "test-source",
            Type = "test.event.type"
        };
        
        var command = new RegisterEventCommand
        {
            RegisterEvent = registerEvent
        };

        // Capture console output
        using var sw = new StringWriter();
        Console.SetOut(sw);

        // Act
        SaveEventHandler.Handle(command);

        // Assert
        var output = sw.ToString();
        Assert.Contains("I got a message!", output);
        Assert.Contains($"MessageId: {registerEvent.Id}", output);
    }

    [Fact]
    public void Handle_WithNullMessage_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<NullReferenceException>(() => SaveEventHandler.Handle(null));
    }

    [Fact]
    public void Handle_WithNullRegistrationEvent_ThrowsException()
    {
        // Arrange
        var command = new RegisterEventCommand
        {
            RegisterEvent = null
        };

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => SaveEventHandler.Handle(command));
    }
}
