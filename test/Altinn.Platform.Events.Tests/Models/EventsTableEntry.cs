using System;
using System.Text.Json.Serialization;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Tests.Models
{
    public class EventsTableEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("source")]
        public Uri Source { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        [JsonPropertyName("alternativesubject")]
        public string AlternativeSubject { get; set; }

        [JsonPropertyName("time")]
        public DateTime? Time { get; set; }

        [JsonPropertyName("sequenceno")]
        public int SequenceNo { get; set; }

        [JsonPropertyName("cloudEvent")]
        public CloudEvent CloudEvent { get; set; }
    }
}
