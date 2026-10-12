using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Folders
{
    public partial class FoldersRestClient
    {
        /// <summary>Retrieves visible folders with explicitly configured filters.</summary>
        /// <inheritdoc cref="GetConfiguredFoldersAsync"/>
        public FoldersResponse GetConfiguredFolders(string userId, FolderListingOptions options, CancellationToken cancellationToken = default(CancellationToken))
            => GetConfiguredFoldersAsync(userId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves explicitly filtered visible folders using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="options">The copied explicit selection of scope, fields and nullable flags.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The original folder metadata; HTTP 204 produces an explicit empty list.</returns>
        /// <remarks>API 2.29 section 5.4.11 controls filter precedence and role visibility. Text only combines with fields. Full-path requires explicit shared/top-most flags; inherited-sharings is unsupported. The server ignores document/IDs when shared-only is true, and enforces scope relationships. No flags, parent, path conversion or permissions are inferred. Existing listings and interfaces remain unchanged.</remarks>
        /// <exception cref="ArgumentNullException">The options are null.</exception>
        /// <exception cref="ArgumentException">The fields/identifiers or text combination are unusable.</exception>
        /// <exception cref="NotSupportedException">Requested listing fields violate manufacturer disclosure constraints.</exception>
        /// <exception cref="InvalidResponseDataException">Successful data lacks a folders array.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<FoldersResponse> GetConfiguredFoldersAsync(string userId, FolderListingOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); if (options == null) throw new ArgumentNullException(nameof(options));
            var collection = options.Collection; var parent = options.Parent; var document = options.Document; var path = options.Path; var group = options.Group; var text = options.Text;
            var shared = options.OnlySharedFolders; var top = options.OnlyTopMost; var includePublic = options.IncludePublicCollections;
            var ids = SnapshotFolderValues(options.Ids, "ids"); var fields = SnapshotFolderValues(options.Fields, "fields");
            if (text != null && (collection != null || parent != null || document != null || path != null || group != null || ids != null || shared.HasValue || top.HasValue || includePublic.HasValue)) throw new ArgumentException("A folder text query only combines with fields.", nameof(options));
            if (fields?.Contains("inherited-sharings") == true) throw new NotSupportedException("Inherited sharings are unavailable in folder listings.");
            if (fields?.Contains("full-path") == true && (shared != true || top != true)) throw new NotSupportedException("Full-path requires explicitly shared-only and top-most filters.");
            var request = CreateRestRequest(URI_RESOURCE, Method.Get);
            foreach (var pair in new[] { new KeyValuePair<string, string>("collection", collection), new KeyValuePair<string, string>("parent", parent), new KeyValuePair<string, string>("document", document), new KeyValuePair<string, string>("path", path), new KeyValuePair<string, string>("group", group), new KeyValuePair<string, string>("text", text) })
                if (pair.Value != null) request.AddQueryParameter(pair.Key, pair.Value);
            if (ids != null) request.AddQueryParameter("ids", string.Join(",", ids)); if (fields != null) request.AddQueryParameter("fields", string.Join(",", fields));
            if (shared.HasValue) request.AddQueryParameter("only-shared-folders", shared.Value ? "true" : "false");
            if (top.HasValue) request.AddQueryParameter("only-top-most", top.Value ? "true" : "false");
            if (includePublic.HasValue) request.AddQueryParameter("include-public-collections", includePublic.Value ? "true" : "false");
            var response = await ExecuteAsync<FoldersResponse>(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); var result = UnwrapResponse(response, new GetFoldersResponseHandler());
            if (result?.Folders == null) throw new InvalidResponseDataException("The folder listing has no folders array.", new InvalidDataException("Expected folders in a successful listing."));
            return result;
        }
        private static string[] SnapshotFolderValues(IEnumerable<string> values, string name)
        {
            var selected = values?.ToArray(); if (selected != null && (selected.Length == 0 || selected.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains(",")))) throw new ArgumentException("A supplied " + name + " selection requires individual nonblank values.", name); return selected;
        }

        /// <summary>Creates a folder with both explicit parent and collection selections preserved.</summary>
        /// <inheritdoc cref="CreateSelectedFolderAsync"/>
        public FolderCreationResponse CreateSelectedFolder(string userId, string name, string collection = null, string parent = null, CancellationToken cancellationToken = default(CancellationToken))
            => CreateSelectedFolderAsync(userId, name, collection, parent, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates an explicitly located folder using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="name">The original folder name, validated for uniqueness by the server.</param>
        /// <param name="collection">The explicit collection, or null when an existing parent identifies it.</param>
        /// <param name="parent">The explicit parent, including none for top-level creation, or null to omit it.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The server's created folder identifier.</returns>
        /// <remarks>API 2.29 section 5.4.1 validates normal-collection state, actual roles and matching parent/collection identity. Both provided selections are sent unchanged; no top-level parent is inserted or existing parent looked up. Creation is not idempotent; no new replay is added. Existing creation defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The name or explicit location is missing.</exception>
        /// <exception cref="InvalidResponseDataException">Successful creation has no usable identifier.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and cannot establish that no folder was created.</exception>
        public async Task<FolderCreationResponse> CreateSelectedFolderAsync(string userId, string name, string collection = null, string parent = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); if (name == null || collection == null && (string.IsNullOrWhiteSpace(parent) || parent == "none") || collection != null && string.IsNullOrWhiteSpace(collection) || parent != null && string.IsNullOrWhiteSpace(parent)) throw new ArgumentException("An original name and explicit collection or existing parent are required.");
            var body = new Newtonsoft.Json.Linq.JObject { ["name"] = name }; if (collection != null) body["collection"] = collection; if (parent != null) body["parent"] = parent;
            var request = CreateRestRequest(URI_RESOURCE, Method.Post, "application/json"); request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync<FolderCreationResponse>(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); var result = UnwrapResponse(response, new CreateFolderResponseHandler());
            if (string.IsNullOrWhiteSpace(result?.Id)) throw new InvalidResponseDataException("Folder creation returned no usable identifier.", new InvalidDataException("Expected the created folder identifier.")); return result;
        }
    }
    public partial class FolderRestClient
    {
        /// <summary>Moves a selected folder while preserving both explicit parent and collection choices.</summary>
        /// <inheritdoc cref="MoveFolderToAsync"/>
        public void MoveFolderTo(string userId, string folderId, string parent, string collection = null, CancellationToken cancellationToken = default(CancellationToken))
            => MoveFolderToAsync(userId, folderId, parent, collection, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Moves a selected folder using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="folderId">The explicitly selected folder identifier.</param>
        /// <param name="parent">The explicit existing parent or none for a top-level move.</param>
        /// <param name="collection">The explicit destination collection, or null to retain server parent/current-collection semantics.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>API 2.29 section 5.4.3 enforces cycles, matching parent/collection, uniqueness, roles and reference topology. Both supplied choices are retained without lookup, inferred permissions or new replay. Existing move methods/defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selected folder or location is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and cannot establish that the folder stayed in place.</exception>
        public async Task MoveFolderToAsync(string userId, string folderId, string parent, string collection = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); if (string.IsNullOrWhiteSpace(folderId) || folderId == "." || folderId == ".." || string.IsNullOrWhiteSpace(parent) || collection != null && string.IsNullOrWhiteSpace(collection)) throw new ArgumentException("An explicit folder and destination parent are required.");
            var body = new Newtonsoft.Json.Linq.JObject { ["parent"] = parent }; if (collection != null) body["collection"] = collection;
            var request = CreateRestRequest(URI_RESOURCE + Uri.EscapeDataString(folderId), Method.Put, "application/json"); request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
