using CenterDevice.Rest.Clients.Groups;
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
        internal static JObject SnapshotGroupOptions(object options, bool create)
        {
            if (options == null) throw new ArgumentNullException("request");
            var data = JObject.FromObject(options, JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            if ((create && string.IsNullOrWhiteSpace(data.Value<string>("name"))) ||
                (data["name"] != null && string.IsNullOrWhiteSpace(data.Value<string>("name"))))
                throw new ArgumentException("A supplied group name must be usable.", "request");
            if (data["type"] != null && data.Value<string>("type") != "public" && data.Value<string>("type") != "private")
                throw new ArgumentException("Group type must be public or private.", "request");
            if (!create && !data.HasValues) throw new ArgumentException("At least one group field must be selected.", "request");
            if (data["users"] is JArray users) ValidateGroupIds(users);
            if (data["admins"] is JArray admins) ValidateGroupAdmins(admins);
            if (create && data["admins"] != null && data.Value<string>("type") != "private")
                throw new ArgumentException("Administrators are configured only for explicit private-group creation.", "request");
            if (create && data.Value<string>("type") == "private" &&
                !(data["admins"] is JArray privateAdmins && privateAdmins.Any(admin => admin.Value<string>("privilege") == "group-management")))
                throw new ArgumentException("Private creation requires a group-management administrator.", "request");
            return data;
        }

        internal static JArray SnapshotGroupIds(IEnumerable<string> users)
        {
            if (users == null) throw new ArgumentNullException(nameof(users));
            var snapshot = new JArray(users.ToArray());
            if (snapshot.Count == 0) throw new ArgumentException("At least one selected user is required.", nameof(users));
            ValidateGroupIds(snapshot);
            return snapshot;
        }

        internal static JArray SnapshotGroupAdmins(IEnumerable<GroupAdministrator> admins)
        {
            if (admins == null) throw new ArgumentNullException(nameof(admins));
            var values = admins.ToArray();
            var snapshot = JArray.FromObject(values, JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            if (snapshot.Count == 0) throw new ArgumentException("At least one selected administrator is required.", nameof(admins));
            ValidateGroupAdmins(snapshot);
            return snapshot;
        }

        private static void ValidateGroupIds(JArray values)
        {
            if (values.Any(value => value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>())))
                throw new ArgumentException("Selected users must have usable identifiers.", "request");
        }

        private static void ValidateGroupAdmins(JArray values)
        {
            if (values.Any(value => value.Type != JTokenType.Object || string.IsNullOrWhiteSpace(value.Value<string>("user-id")) ||
                (value.Value<string>("privilege") != "group-management" && value.Value<string>("privilege") != "group-membership")))
                throw new ArgumentException("Administrators require usable IDs and known privileges.", "request");
            if (values.GroupBy(value => GroupIdentity(value.Value<string>("user-id")), StringComparer.Ordinal)
                .Any(group => group.Select(value => value.Value<string>("privilege")).Distinct().Count() > 1))
                throw new ArgumentException("An administrator cannot select conflicting privileges in the same command.", "request");
        }

        internal static string GroupResource(string prefix, string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentException("A selected group is required.", nameof(groupId));
            return prefix + "group/" + Uri.EscapeDataString(groupId);
        }

        internal async Task<GroupCommandResult> ExecuteGroupCommandAsync(string userId, string resource, Method method,
            JObject body, bool create, bool partialAllowed, string[] selectedUsers, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var request = CreateRestRequest(resource, method);
            if (body != null) request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(create ? new[] { HttpStatusCode.Created } :
                partialAllowed ? new[] { HttpStatusCode.OK, HttpStatusCode.NoContent } : new[] { HttpStatusCode.NoContent }));
            if (!create && response.StatusCode == HttpStatusCode.NoContent) return new GroupCommandResult(null, new string[0]);
            try
            {
                JObject data;
                using (var text = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                { data = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the group response."); }
                var failedToken = data["failed-users"];
                var failed = new string[0];
                if (failedToken != null)
                {
                    if (!(failedToken is JArray array) || array.Count == 0 || array.Any(value => value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>())))
                        throw new InvalidDataException("Partial failures must identify failed selected users.");
                    failed = array.Values<string>().ToArray();
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    if (failed.Any(id => !seen.Add(GroupIdentity(id)) || selectedUsers == null || !selectedUsers.Any(selected => GroupIdentity(id) == GroupIdentity(selected))))
                        throw new InvalidDataException("Partial failures identify repeated or unselected users.");
                }
                else if (!create) throw new InvalidDataException("A partial-success response must identify failed users.");
                string createdId = null;
                if (create)
                {
                    var id = data["id"];
                    if (id != null && (id.Type != JTokenType.String || string.IsNullOrWhiteSpace(id.Value<string>())))
                        throw new InvalidDataException("A created group ID must be usable.");
                    createdId = id?.Value<string>();
                    var locations = response.Headers?.Where(header => string.Equals(header.Name, "Location", StringComparison.OrdinalIgnoreCase)).Select(header => header.Value?.ToString()).ToArray();
                    if (locations != null && locations.Length > 0)
                    {
                        if (locations.Length != 1 || string.IsNullOrWhiteSpace(locations[0])) throw new InvalidDataException("Creation returned an ambiguous Location.");
                        var origin = client.BuildUri(request);
                        var expected = client.BuildUri(CreateRestRequest(ApiVersionPrefix + "group/", Method.Get));
                        if (!Uri.TryCreate(origin, locations[0], out var location) || location.Scheme != expected.Scheme || location.Authority != expected.Authority ||
                            !string.IsNullOrEmpty(location.Query) || !string.IsNullOrEmpty(location.Fragment) || !string.IsNullOrEmpty(location.UserInfo) ||
                            !location.AbsolutePath.StartsWith(expected.AbsolutePath, StringComparison.Ordinal))
                            throw new InvalidDataException("Creation Location does not identify a group on the configured API origin.");
                        var segment = location.AbsolutePath.Substring(expected.AbsolutePath.Length);
                        if (segment.Length == 0 || segment.Contains("/")) throw new InvalidDataException("Creation Location has no single group ID.");
                        var locationId = Uri.UnescapeDataString(segment);
                        if (string.IsNullOrWhiteSpace(locationId) || locationId.Contains("/") || (createdId != null && GroupIdentity(createdId) != GroupIdentity(locationId)))
                            throw new InvalidDataException("Creation Location disagrees with the new group ID.");
                        if (createdId == null) createdId = locationId;
                    }
                    if (createdId == null) throw new InvalidDataException("Creation must confirm the new group ID in the body or Location.");
                }
                return new GroupCommandResult(createdId, failed);
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException || error is UriFormatException)
            { throw new InvalidResponseDataException("The group command returned an invalid response; its mutation must not be replayed automatically.", error); }
        }

        private static string GroupIdentity(string value) => Guid.TryParse(value, out var id) ? id.ToString("D") : value;
    }
}
