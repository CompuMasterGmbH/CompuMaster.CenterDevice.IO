using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    public abstract partial class CenterDeviceRestClient
    {
        internal static void ValidateCopyDestination(string collectionId, string folderId)
        {
            if (collectionId != null && folderId != null) throw new ArgumentException("Collection and folder targets are mutually exclusive.");
            if (collectionId != null && string.IsNullOrWhiteSpace(collectionId)) throw new ArgumentException("The collection target is empty.", nameof(collectionId));
            if (folderId != null && string.IsNullOrWhiteSpace(folderId)) throw new ArgumentException("The folder target is empty.", nameof(folderId));
        }

        internal async Task<string[]> ExecuteDocumentCopyAsync(string userId, string resource, JObject parameters, string[] sourceIds, bool bulk, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var request = CreateRestRequest(resource, Method.Post, "application/json");
            // Encode the JObject once: the default System.Text.Json serializer treats JToken as a sequence.
            request.AddStringBody(new JObject { ["action"] = "copy", ["params"] = parameters }.ToString(Newtonsoft.Json.Formatting.None), "application/json");
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(bulk ? HttpStatusCode.OK : HttpStatusCode.Created));
            try
            {
                var data = JObject.Parse(response.Content);
                var ids = bulk ? data["documents"] as JArray : new JArray(data["id"]);
                if (ids == null || ids.Count == 0 || ids.Any(id => id.Type != JTokenType.String || string.IsNullOrWhiteSpace(id.Value<string>())))
                    throw new InvalidDataException("The copy response must identify newly created documents.");
                var result = ids.Values<string>().ToArray();
                for (var index = 0; index < result.Length; index++)
                {
                    if (sourceIds.Any(source => SameDocumentId(source, result[index])) || result.Take(index).Any(previous => SameDocumentId(previous, result[index])))
                        throw new InvalidDataException("Copy identifiers must be distinct from sources and from other copies.");
                }
                return result;
            }
            catch (Exception error) when (error is Newtonsoft.Json.JsonException || error is InvalidDataException || error is ArgumentNullException)
            { throw new InvalidResponseDataException("The document copy returned invalid new-document identifiers.", error); }
        }
    }
}
