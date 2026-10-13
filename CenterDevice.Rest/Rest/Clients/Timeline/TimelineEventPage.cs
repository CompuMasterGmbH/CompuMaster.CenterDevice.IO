using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CenterDevice.Rest.Clients.Timeline
{
    /// <summary>Retains complete original metadata of one visible timeline event.</summary>
    /// <remarks>Unknown event types and future details remain available. Missing metadata is not inferred from the authentication context or current resource state.</remarks>
    public sealed class TimelineEventDetails
    {
        private readonly JObject details;
        internal TimelineEventDetails(JObject details)
        {
            this.details = (JObject)details.DeepClone();
            Id = ReadText(details, "id"); Type = ReadText(details, "type"); Timestamp = ReadText(details, "timestamp"); Creator = ReadText(details, "creator");
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Type)) throw new InvalidDataException("A timeline event requires an identity and type.");
            if (details["details"] != null && details["details"].Type != JTokenType.Null && details["details"].Type != JTokenType.Object) throw new InvalidDataException("Timeline event details must be an object.");
        }
        /// <summary>Gets the original event identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original event type, including future types.</summary>
        public string Type { get; }
        /// <summary>Gets the original timestamp text, or null when omitted or hidden.</summary>
        public string Timestamp { get; }
        /// <summary>Gets the original event-creator identifier, or null when omitted or hidden.</summary>
        public string Creator { get; }
        /// <summary>Gets a defensive copy of complete event details, or null when omitted or hidden.</summary>
        public JObject Details => (JObject)(details["details"] as JObject)?.DeepClone();
        /// <summary>Copies complete original event metadata, including future fields.</summary>
        /// <returns>A defensive copy of the event object.</returns>
        public JObject GetRawDetails() => (JObject)details.DeepClone();
        private static string ReadText(JObject value, string name)
        {
            var token = value[name]; if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.String) throw new InvalidDataException("Timeline text metadata has an invalid JSON type.");
            return token.Value<string>();
        }
    }

    /// <summary>Retains a complete bounded page of original timeline events.</summary>
    /// <remarks>The result does not infer a next cursor, permissions or synchronization completeness.</remarks>
    public sealed class TimelineEventPage
    {
        private readonly JObject details;
        private TimelineEventPage(JObject details, string rawJson, IEnumerable<TimelineEventDetails> entries)
        { this.details = (JObject)details.DeepClone(); RawJson = rawJson; Entries = entries.ToList().AsReadOnly(); }
        /// <summary>Gets the events in their original server order.</summary>
        public IReadOnlyList<TimelineEventDetails> Entries { get; }
        /// <summary>Gets the exact original response JSON.</summary>
        public string RawJson { get; }
        /// <summary>Copies the complete original page, including future fields.</summary>
        /// <returns>A defensive copy of the response object.</returns>
        public JObject GetRawDetails() => (JObject)details.DeepClone();
        internal static TimelineEventPage Parse(string content, int maximumRows, string previousId)
        {
            try
            {
                using (var source = new StringReader(content ?? "")) using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("The timeline page contains trailing JSON.");
                    if (!(data["entries"] is JArray entries) || entries.Count > maximumRows) throw new InvalidDataException("A bounded timeline entry array is required.");
                    var result = new List<TimelineEventDetails>(); var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var entry in entries)
                    {
                        if (!(entry is JObject value)) throw new InvalidDataException("A timeline entry must be an object.");
                        var eventDetails = new TimelineEventDetails(value); var identity = Identity(eventDetails.Id);
                        if (!seen.Add(identity) || (previousId != null && identity == Identity(previousId))) throw new InvalidDataException("A timeline page repeats an event or its exclusive previous cursor.");
                        result.Add(eventDetails);
                    }
                    return new TimelineEventPage(data, content, result);
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException)
            { throw new InvalidResponseDataException("The timeline returned an invalid event page.", error); }
        }
        private static string Identity(string value) => Guid.TryParse(value, out var id) ? id.ToString("D") : value;
    }
}
