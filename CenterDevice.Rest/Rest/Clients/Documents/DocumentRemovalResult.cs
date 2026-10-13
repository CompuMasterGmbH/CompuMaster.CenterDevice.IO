using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Reports complete removal success or exact per-document failures.</summary>
    /// <remarks>Successful HTTP transport can carry failures. The result does not prove that existing idempotent state changed, establish permissions or authorize a retry.</remarks>
    public sealed class DocumentRemovalResult
    {
        internal DocumentRemovalResult(IEnumerable<DocumentRemovalFailure> failures, string rawJson)
        {
            Failures = failures.ToList().AsReadOnly();
            FailedDocuments = Failures.Select(failure => failure.DocumentId).ToList().AsReadOnly();
            RawJson = rawJson;
        }
        /// <summary>Gets the server-reported failures in their original order.</summary>
        public IReadOnlyList<DocumentRemovalFailure> Failures { get; }
        /// <summary>Gets the failed document identifiers in their original spelling and order.</summary>
        public IReadOnlyList<string> FailedDocuments { get; }
        /// <summary>Gets whether the server reported complete success.</summary>
        public bool AllSucceeded => Failures.Count == 0;
        /// <summary>Gets the original partial-failure JSON, or null after HTTP 204.</summary>
        public string RawJson { get; }
    }

    /// <summary>Describes one server-reported document removal failure.</summary>
    public sealed class DocumentRemovalFailure
    {
        private readonly JObject details;
        internal DocumentRemovalFailure(string documentId, JObject details)
        {
            DocumentId = documentId;
            this.details = (JObject)details?.DeepClone();
            ErrorCode = details?["code"]?.ToString(Formatting.None);
            Message = details?["message"]?.Value<string>();
        }
        /// <summary>Gets the failed document identifier in its original spelling.</summary>
        public string DocumentId { get; }
        /// <summary>Gets the original integer error-code text, or null when detailed errors were not requested.</summary>
        public string ErrorCode { get; }
        /// <summary>Gets the original error message, or null when detailed errors were not requested.</summary>
        public string Message { get; }
        /// <summary>Gets a defensive copy of complete error data, or null when detailed errors were not requested.</summary>
        public JObject Data => (JObject)details?["data"]?.DeepClone();
        /// <summary>Gets a defensive copy of the complete detailed failure, including future fields, or null for identifier-only failures.</summary>
        public JObject Details => (JObject)details?.DeepClone();
    }

    internal static class DocumentRemovalJson
    {
        internal static DocumentRemovalResult Read(string content, string[] selected, bool detailed, bool single)
        {
            try
            {
                using (var source = new StringReader(content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var json = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("The removal response contains trailing JSON.");
                    if (!(json["failed-documents"] is JArray failures) || failures.Count == 0 || (single && failures.Count != 1))
                        throw new InvalidDataException("A partial removal failure must identify the failed documents.");
                    var result = new List<DocumentRemovalFailure>();
                    foreach (var failure in failures)
                    {
                        string id;
                        JObject details = null;
                        if (detailed)
                        {
                            details = failure as JObject;
                            if (details == null || details["code"]?.Type != JTokenType.Integer || details["message"]?.Type != JTokenType.String ||
                                !(details["data"] is JObject data) || data["document-id"]?.Type != JTokenType.String)
                                throw new InvalidDataException("A detailed removal failure requires an integer code, message and document identity.");
                            id = data["document-id"].Value<string>();
                        }
                        else
                        {
                            if (failure.Type != JTokenType.String) throw new InvalidDataException("An identifier-only failure must contain a document identifier.");
                            id = failure.Value<string>();
                        }
                        if (string.IsNullOrWhiteSpace(id) || !selected.Any(value => SameIdentity(value, id)))
                            throw new InvalidDataException("A removal failure identifies a document outside the request.");
                        result.Add(new DocumentRemovalFailure(id, details));
                    }
                    return new DocumentRemovalResult(result, content);
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException)
            {
                throw new Exceptions.InvalidResponseDataException("The document removal returned an invalid partial-failure response.", error);
            }
        }

        private static bool SameIdentity(string first, string second)
            => string.Equals(first, second, StringComparison.Ordinal) ||
                (Guid.TryParse(first, out var left) && Guid.TryParse(second, out var right) && left == right);
    }
}
