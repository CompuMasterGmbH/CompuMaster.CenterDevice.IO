using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    public abstract partial class CenterDeviceRestClient
    {
        internal static JObject SnapshotSharingRecipients(DocumentSharingRecipients recipients)
        {
            if (recipients == null) throw new ArgumentNullException(nameof(recipients));
            // Capture all enumerable references before enumerating; snapshot before asynchronous authorization.
            var users = recipients.Users; var emails = recipients.Emails; var groups = recipients.Groups;
            var data = new JObject();
            foreach (var pair in new[] { new KeyValuePair<string, IEnumerable<string>>("users", users),
                new KeyValuePair<string, IEnumerable<string>>("emails", emails), new KeyValuePair<string, IEnumerable<string>>("groups", groups) })
            {
                if (pair.Value == null) continue;
                var values = pair.Value.ToArray();
                if (values.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Recipients must contain usable values.", nameof(recipients));
                data[pair.Key] = new JArray(values);
            }
            if (!data.Properties().Any(property => ((JArray)property.Value).Count > 0))
                throw new ArgumentException("Select at least one recipient; removing all sharings requires the explicit UnshareAll command.", nameof(recipients));
            return data;
        }

        internal static string SelectedSharingResource(string prefix, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A selected document ID is required.", nameof(id));
            if (id == "." || id == "..") throw new ArgumentException("A document ID must not be a relative path segment.", nameof(id));
            return prefix + Uri.EscapeDataString(id);
        }

        internal async Task<DocumentSharingResult> ExecuteDocumentSharingAsync(string userId, string resource, bool share,
            JObject parameters, string[] documents, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            // Keep recipient selection separate from documents/comment for response reconciliation.
            var selections = (JObject)parameters.DeepClone();
            var request = CreateRestRequest(resource, Method.Post, "application/json");
            request.AddStringBody(new JObject { ["action"] = share ? "share" : "unshare", ["params"] = parameters }.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(new[] { HttpStatusCode.OK, HttpStatusCode.NoContent }));
            if (response.StatusCode == HttpStatusCode.NoContent)
                return new DocumentSharingResult(new string[0], new string[0], new string[0], new string[0],
                    new DocumentSharingInvitation[0], new DocumentSharingInvitationFailure[0]);
            try
            {
                JObject data;
                using (var text = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                { data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the sharing result."); }
                var failedDocuments = ReadSharingFailures(data, "failed-documents", documents, false);
                var failedUsers = ReadSharingFailures(data, "failed-users", SharingSelection(selections, "users"), false);
                var failedGroups = ReadSharingFailures(data, "failed-groups", SharingSelection(selections, "groups"), false);
                var failedEmails = ReadSharingFailures(data, "failed-emails", SharingSelection(selections, "emails"), true);
                var invited = new List<DocumentSharingInvitation>();
                var notInvited = new List<DocumentSharingInvitationFailure>();
                var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in new[] { "invited", "not-invited" })
                {
                    if (data[key] == null) continue;
                    if (!(data[key] is JArray entries)) throw new InvalidDataException("Invitation results must be arrays.");
                    foreach (var entry in entries)
                    {
                        if (!(entry is JObject item) || item["email"]?.Type != JTokenType.String ||
                            string.IsNullOrWhiteSpace(item.Value<string>("email"))) throw new InvalidDataException("Invitation results require e-mail addresses.");
                        var email = item.Value<string>("email");
                        if (!SharingSelection(selections, "emails").Any(selected => string.Equals(selected, email, StringComparison.OrdinalIgnoreCase)) ||
                            !seenEmails.Add(email)) throw new InvalidDataException("Invitation results contain an unselected or repeated e-mail address.");
                        if (key == "invited")
                        {
                            if (item["user-id"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(item.Value<string>("user-id")))
                                throw new InvalidDataException("A successful invitation requires a new user ID.");
                            invited.Add(new DocumentSharingInvitation(email, item.Value<string>("user-id")));
                        }
                        else
                        {
                            if (item["code"]?.Type != JTokenType.Integer) throw new InvalidDataException("An invitation failure requires a numeric code.");
                            notInvited.Add(new DocumentSharingInvitationFailure(email, item.Value<long>("code")));
                        }
                    }
                }
                if ((!share && (invited.Count > 0 || notInvited.Count > 0)) || (share && failedEmails.Length > 0))
                    throw new InvalidDataException("The response does not match the selected sharing action.");
                var result = new DocumentSharingResult(failedDocuments, failedUsers, failedEmails, failedGroups, invited, notInvited);
                if (result.AllSucceeded && invited.Count == 0) throw new InvalidDataException("HTTP 200 must identify invitations or partial failures.");
                return result;
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException || error is OverflowException)
            { throw new InvalidResponseDataException("The document sharing response is invalid; do not replay its mutation automatically.", error); }
        }

        private static string[] SharingSelection(JObject selections, string key)
            => (selections[key] as JArray)?.Values<string>().ToArray() ?? new string[0];

        private static string[] ReadSharingFailures(JObject data, string key, string[] selected, bool emails)
        {
            if (data[key] == null) return new string[0];
            if (!(data[key] is JArray array) || array.Any(value => value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>())))
                throw new InvalidDataException("Sharing failure lists must contain usable identifiers.");
            var values = array.Values<string>().ToArray();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var identity = emails ? value.ToUpperInvariant() : Guid.TryParse(value, out var guid) ? guid.ToString("D") : value;
                if (!seen.Add(identity) || !selected.Any(id => emails ? string.Equals(id, value, StringComparison.OrdinalIgnoreCase) : SameDocumentId(id, value)))
                    throw new InvalidDataException("Sharing failures contain repeated or unselected recipients/documents.");
            }
            return values;
        }
    }
}
