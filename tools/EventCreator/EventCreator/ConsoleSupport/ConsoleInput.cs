namespace EventCreator.ConsoleSupport;

/// <summary>
/// Shared console prompt/parsing helpers used across the interactive workflows.
/// </summary>
public static class ConsoleInput
{
    /// <summary>
    /// Prompts for an instance GUID, reusing <paramref name="previousGuid"/> if the user presses enter without typing one.
    /// </summary>
    public static bool TryReadInstanceGuid(string previousGuid, out Guid instanceGuid)
    {
        string prompt = string.IsNullOrEmpty(previousGuid)
            ? "Enter instance GUID: "
            : $"Enter instance GUID [{previousGuid}]: ";

        Console.Write(prompt);
        string? guidInput = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(guidInput))
        {
            guidInput = previousGuid;
        }

        return Guid.TryParse(guidInput, out instanceGuid);
    }

    /// <summary>
    /// Prompts for an integer, falling back to <paramref name="defaultValue"/> when the input is missing, unparsable, or fails <paramref name="isValid"/>.
    /// </summary>
    public static int ReadInt(string promptLabel, int defaultValue, Func<int, bool>? isValid = null)
    {
        Console.Write($"{promptLabel} [{defaultValue}]: ");
        string? input = Console.ReadLine()?.Trim();

        if (int.TryParse(input, out int parsed) && (isValid is null || isValid(parsed)))
        {
            return parsed;
        }

        return defaultValue;
    }

    /// <summary>
    /// Prompts for an explicit yes/no confirmation. Anything other than "y"/"yes" (case-insensitive) is treated as "no".
    /// </summary>
    public static bool Confirm(string prompt)
    {
        Console.Write($"{prompt} [y/N]: ");
        string? input = Console.ReadLine()?.Trim();
        return string.Equals(input, "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
