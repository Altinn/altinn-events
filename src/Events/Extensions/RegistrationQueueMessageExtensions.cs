using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="RegistrationQueueMessage"/> serialization and deserialization.
    /// </summary>
    public static class RegistrationQueueMessageExtensions
    {
        /// <summary>
        /// Serializes a <see cref="RegistrationQueueMessage"/> envelope to JSON for the registration queue.
        /// </summary>
        public static string Serialize(this RegistrationQueueMessage message)
        {
            return System.Text.Json.JsonSerializer.Serialize(message);
        }

        /// <summary>
        /// Deserializes a JSON string from the registration queue into a <see cref="RegistrationQueueMessage"/> envelope.
        /// </summary>
        public static RegistrationQueueMessage DeserializeToRegistrationQueueMessage(this string item)
        {
            return System.Text.Json.JsonSerializer.Deserialize<RegistrationQueueMessage>(item);
        }
    }
}
