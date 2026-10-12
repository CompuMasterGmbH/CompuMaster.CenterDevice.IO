using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Reminders
{
    /// <summary>Manages reminders created by the authenticated caller.</summary>
    /// <remarks>Administrator, internal and external users can create reminders for visible documents. Retrieval, completion and removal are restricted to the creator. The server enforces visibility, status transitions and notification settings.</remarks>
    public class RemindersRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes a reminder client with the existing authorization and transport configuration.</summary>
        /// <param name="oauthInfoProvider">The authorization information provider.</param>
        /// <param name="configuration">The REST origin and user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public RemindersRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>Creates a future document reminder and returns its confirmed identifier.</summary>
        /// <inheritdoc cref="CreateReminderAsync"/>
        public string CreateReminder(string userId, string documentId, DateTimeOffset expiryDate, string note = null, CancellationToken cancellationToken = default(CancellationToken))
            => CreateReminderAsync(userId, documentId, expiryDate, note, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Creates a future document reminder using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentId">The nonblank visible document identifier.</param>
        /// <param name="expiryDate">The future trigger date with its explicit timezone offset.</param>
        /// <param name="note">The optional note of at most 10000 UTF-16 code units; null omits it and an empty string remains explicit.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The server-confirmed new reminder identifier.</returns>
        /// <remarks>Creation is non-idempotent and may cause later notification mail according to account settings. Retain the identifier for reconciliation or cleanup; uncertain outcomes are not replayed. The server validates expiry again after authorization.</remarks>
        /// <exception cref="ArgumentException">The document, expiry or note is invalid.</exception>
        /// <exception cref="InvalidResponseDataException">The successful response does not confirm a usable identity.</exception>
        public async Task<string> CreateReminderAsync(string userId, string documentId, DateTimeOffset expiryDate, string note = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireId(documentId, nameof(documentId));
            if (expiryDate <= DateTimeOffset.UtcNow) throw new ArgumentException("The expiry date must be in the future.", nameof(expiryDate));
            if (note != null && note.Length > 10000) throw new ArgumentException("The note exceeds 10000 UTF-16 code units.", nameof(note));
            var data = new JObject { ["type"] = "document", ["entity"] = documentId, ["expiry-date"] = expiryDate.ToString("o", CultureInfo.InvariantCulture) };
            if (note != null) data["note"] = note;
            var response = await SendAsync(userId, ApiVersionPrefix + "reminders", Method.Post, data, HttpStatusCode.Created, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, root => RequiredText(root["id"]));
        }

        /// <summary>Retrieves a reminder created by the authenticated caller.</summary>
        /// <inheritdoc cref="GetReminderAsync"/>
        public Reminder GetReminder(string userId, string reminderId, CancellationToken cancellationToken = default(CancellationToken))
            => GetReminderAsync(userId, reminderId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves an owned reminder using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="reminderId">The nonblank selected reminder identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The original reminder metadata without inferred ownership or dates.</returns>
        /// <exception cref="InvalidResponseDataException">The successful response has malformed metadata or a different identity.</exception>
        public async Task<Reminder> GetReminderAsync(string userId, string reminderId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await SendAsync(userId, Resource(reminderId), Method.Get, null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, root =>
            {
                var reminder = ParseReminder(root);
                if (Identity(reminder.Id) != Identity(reminderId)) throw new InvalidDataException("The reminder identity differs from the selection.");
                return reminder;
            });
        }

        /// <summary>Retrieves the latest visible reminders created by the authenticated caller.</summary>
        /// <inheritdoc cref="GetRemindersAsync"/>
        public IReadOnlyList<Reminder> GetReminders(string userId, IEnumerable<string> statuses = null, string entityId = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetRemindersAsync(userId, statuses, entityId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves the latest owned reminders using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="statuses">Optional pending, triggered or completed filters, snapshotted once before authorization; null omits the filter and an empty sequence remains explicit.</param>
        /// <param name="entityId">The optional nonblank referenced entity filter, or null to omit it.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only page preserving server order and identities.</returns>
        /// <remarks>The server returns at most the latest 500 matching reminders. This is not a complete history and supplies no paging, total or next-page flag; a full page does not establish completeness. Creator/recipient metadata is not inferred.</remarks>
        public async Task<IReadOnlyList<Reminder>> GetRemindersAsync(string userId, IEnumerable<string> statuses = null, string entityId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = statuses?.ToArray();
            if (selected != null && selected.Any(status => status != "pending" && status != "triggered" && status != "completed"))
                throw new ArgumentException("Reminder status filters must use pending, triggered or completed.", nameof(statuses));
            if (entityId != null) RequireId(entityId, nameof(entityId));
            var request = CreateRestRequest(ApiVersionPrefix + "reminders", Method.Get);
            if (selected != null) request.AddQueryParameter("statuses", string.Join(",", selected));
            if (entityId != null) request.AddQueryParameter("entity-id", entityId);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return Parse<IReadOnlyList<Reminder>>(response.Content, root =>
            {
                var entries = root["reminders"] as JArray;
                if (entries == null || entries.Count > 500) throw new InvalidDataException("A bounded reminder array is required.");
                var result = new List<Reminder>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries)
                {
                    if (!(entry is JObject item)) throw new InvalidDataException("A reminder must be an object.");
                    var reminder = ParseReminder(item);
                    if (!ids.Add(Identity(reminder.Id))) throw new InvalidDataException("The reminder page contains repeated identities.");
                    if (entityId != null && Identity(reminder.EntityId) != Identity(entityId)) throw new InvalidDataException("A reminder is outside the selected entity.");
                    if (selected != null && selected.Length > 0 && !selected.Contains(reminder.Status)) throw new InvalidDataException("A reminder is outside the selected statuses.");
                    result.Add(reminder);
                }
                return result.AsReadOnly();
            });
        }

        /// <summary>Completes a triggered reminder created by the authenticated caller.</summary>
        /// <inheritdoc cref="CompleteReminderAsync"/>
        public void CompleteReminder(string userId, string reminderId, CancellationToken cancellationToken = default(CancellationToken))
            => CompleteReminderAsync(userId, reminderId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Completes a triggered reminder using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="reminderId">The nonblank selected reminder identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing only after confirmed success.</returns>
        /// <remarks>Only the creator may complete a triggered reminder. Completion is non-idempotent; repeat or invalid-state errors remain server errors and no local status is guessed. Uncertain outcomes are not replayed.</remarks>
        public Task CompleteReminderAsync(string userId, string reminderId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SendAsync(userId, Resource(reminderId), Method.Post, new JObject { ["action"] = "complete" }, HttpStatusCode.NoContent, cancellationToken);
        }

        /// <summary>Removes a reminder created by the authenticated caller.</summary>
        /// <inheritdoc cref="RemoveReminderAsync"/>
        public void RemoveReminder(string userId, string reminderId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveReminderAsync(userId, reminderId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes an owned reminder using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="reminderId">The nonblank selected reminder identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing only after confirmed removal.</returns>
        /// <remarks>It removes only the reminder, preserving the referenced document. The server enforces creator visibility; original failures remain errors and uncertain outcomes are not replayed.</remarks>
        public Task RemoveReminderAsync(string userId, string reminderId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SendAsync(userId, Resource(reminderId), Method.Delete, null, HttpStatusCode.NoContent, cancellationToken);
        }

        private string Resource(string id) { RequireId(id, "reminderId"); return ApiVersionPrefix + "reminder/" + Uri.EscapeDataString(id); }
        private static void RequireId(string id, string name) { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A usable selected identifier is required.", name); }
        private static string Identity(string id) => Guid.TryParse(id, out var value) ? value.ToString("D") : id;
        private async Task<RestResponse> SendAsync(string userId, string resource, Method method, JObject body, HttpStatusCode expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var request = CreateRestRequest(resource, method);
            if (body != null) request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(expected));
            return response;
        }
        private static T Parse<T>(string content, Func<JObject, T> convert)
        {
            try
            {
                using (var text = new StringReader(content ?? ""))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                {
                    var root = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the reminder response.");
                    return convert(root);
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is FormatException)
            { throw new InvalidResponseDataException("The reminder response has invalid identity, metadata or page structure.", error); }
        }
        private static Reminder ParseReminder(JObject data)
        {
            var expiryText = RequiredText(data["expiry-date"]); var creationText = RequiredText(data["creation-date"]);
            var noteToken = data["note"];
            if (noteToken != null && noteToken.Type != JTokenType.String && noteToken.Type != JTokenType.Null) throw new InvalidDataException("A reminder note must be textual.");
            return new Reminder(RequiredText(data["id"]), RequiredText(data["type"]), RequiredText(data["status"]), RequiredText(data["entity"]), noteToken?.Value<string>(),
                expiryText, ParseDate(expiryText), creationText, ParseDate(creationText));
        }
        private static string RequiredText(JToken value)
        {
            if (value?.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>())) throw new InvalidDataException("Required reminder metadata must be usable text.");
            return value.Value<string>();
        }
        private static DateTimeOffset? ParseDate(string value)
        {
            if (!Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}(?:T|$)") || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                throw new InvalidDataException("A reminder date must be usable ISO8601 data.");
            return Regex.IsMatch(value, @"(?:[Zz]|[+-]\d{2}:?\d{2})$") ? (DateTimeOffset?)parsed : null;
        }
    }
}
