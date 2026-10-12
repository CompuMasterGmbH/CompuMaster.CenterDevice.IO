using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
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
        internal async Task<DocumentCommandResult> ExecuteDocumentListCommandAsync(string userId, string resource, string action,
            object parameters, string[] selectedDocuments, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var request = CreateRestRequest(resource, Method.Post, "application/json");
            request.AddJsonBody(new { action, @params = parameters });
            var authorization = await GetOAuthInfoAsync(userId, token).ConfigureAwait(false);
            var response = await ExecuteAsync(authorization, request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.NoContent }));
            if (response.StatusCode == HttpStatusCode.NoContent) return new DocumentCommandResult(new string[0]);
            try
            {
                var data = JObject.Parse(response.Content);
                if (!(data["failed-documents"] is JArray failed) || failed.Count == 0 ||
                    failed.Any(value => value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>())))
                    throw new InvalidDataException("A partial failure must identify failed documents.");
                var ids = failed.Values<string>().ToArray();
                if (ids.Any(id => !selectedDocuments.Any(selected => SameDocumentId(selected, id))))
                    throw new InvalidDataException("A partial failure identifies a document outside the selected request.");
                return new DocumentCommandResult(ids);
            }
            catch (Exception error) when (error is Newtonsoft.Json.JsonException || error is InvalidDataException || error is ArgumentNullException)
            { throw new InvalidResponseDataException("The document command returned an invalid partial-failure response.", error); }
        }

        internal static string[] SnapshotDocumentIds(IEnumerable<string> ids, string parameter)
        {
            if (ids == null) throw new ArgumentNullException(parameter);
            var result = ids.ToArray();
            if (result.Length == 0 || result.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("At least one selected document ID is required.", parameter);
            return result;
        }

        internal static string[] SnapshotDocumentCommandValues(IEnumerable<string> values, string parameter, bool locks, bool normalizeTags = false)
        {
            if (values == null) throw new ArgumentNullException(parameter);
            var result = values.ToArray();
            if (result.Length == 0 || result.Any(value => value == null))
                throw new ArgumentException("A nonempty sequence without null values is required.", parameter);
            if (locks && result.Any(value => value != RestApiConstants.CREATE_NEW_VERSION))
                throw new NotSupportedException("API 2.29 supports only the create-new-version document lock.");
            if (normalizeTags)
            {
                // Apply API 2.29 normalization before sending: some bridges do not deduplicate a single batch.
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return result.Select(value => value.Trim()).Where(value => value.Length > 0 && seen.Add(value)).ToArray();
            }
            return result;
        }

        private static bool SameDocumentId(string first, string second)
            => string.Equals(first, second, StringComparison.Ordinal) ||
                (Guid.TryParse(first, out var left) && Guid.TryParse(second, out var right) && left == right);
    }
}
