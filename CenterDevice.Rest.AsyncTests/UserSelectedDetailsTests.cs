using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.User;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class UserSelectedDetailsTests
    {
        [TestCase(false), TestCase(true)]
        public async Task OriginalNullableMetadataAndUnknownFieldsRemainImmutable(bool sync)
        {
            var state = new State { Json = "{\"id\":\"selected\",\"first-name\":\" First \",\"last-name\":\"\",\"tenant-name\":\"Tenant\",\"tenant-id\":\"tenant\",\"role\":\"future\",\"status\":\"deleted\",\"future-field\":{\"date\":\"2026-10-12\"}}" };
            var client = new Client(state); var result = sync ? client.GetUserDetails("context", "selected") : await client.GetUserDetailsAsync("context", "selected");
            Assert.That(result.FirstName, Is.EqualTo(" First ")); Assert.That(result.LastName, Is.Empty); Assert.That(result.Email, Is.Null);
            Assert.That(result.TenantName, Is.EqualTo("Tenant")); Assert.That(result.TenantId, Is.EqualTo("tenant")); Assert.That(result.Role, Is.EqualTo("future")); Assert.That(result.Status, Is.EqualTo("deleted"));
            Assert.That(result.TechnicalUser, Is.Null); Assert.That(result.Groups, Is.Null); Assert.That(result.Collections, Is.Null); Assert.That(result.Picture, Is.Null);
            var raw = result.GetRawDetails(); Assert.That(raw["future-field"]["date"].Type, Is.EqualTo(JTokenType.String)); raw["first-name"] = "mutated";
            Assert.That(result.GetRawDetails().Value<string>("first-name"), Is.EqualTo(" First ")); Assert.That(result.FirstName, Is.EqualTo(" First "));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/user/selected")); Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
            Assert.That(state.Request.Parameters.Where(p => p.Type == ParameterType.QueryString), Is.Empty); Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Context, Is.EqualTo("context"));
        }

        [TestCase("true", true), TestCase("false", false), TestCase("null", null)]
        public async Task ExplicitTechnicalValuesAndOriginalPictureEncodingRemainUnchanged(string json, bool? expected)
        {
            var state = new State { Json = "{\"technical-user\":" + json + ",\"picture\":{\"mimetype\":\"image/png\",\"encoding\":\"future\",\"data\":\"original-data\"}}" };
            var result = await new Client(state).GetUserDetailsAsync("context", "selected", new[] { "picture" });
            Assert.That(result.Id, Is.Null); Assert.That(result.TechnicalUser, Is.EqualTo(expected)); Assert.That(result.Picture.MimeType, Is.EqualTo("image/png")); Assert.That(result.Picture.Encoding, Is.EqualTo("future")); Assert.That(result.Picture.Data, Is.EqualTo("original-data"));
        }

        [TestCase("null", null), TestCase("[]", 0), TestCase("[{\"id\":\"all-users\"},{\"id\":\"group\",\"name\":\"\",\"color\":0}]", 2)]
        public async Task GroupProjectionDistinguishesUnknownEmptyAndAllUsersMetadata(string json, int? count)
        {
            var result = await new Client(new State { Json = "{\"groups\":" + json + "}" }).GetUserDetailsAsync("context", "current");
            Assert.That(result.Groups?.Count, Is.EqualTo(count));
            if (count == 2) { Assert.That(result.Groups[0].Name, Is.Null); Assert.That(result.Groups[0].Color, Is.Null); Assert.That(result.Groups[1].Name, Is.Empty); Assert.That(result.Groups[1].Color, Is.Zero); }
        }

        [TestCase("null", null), TestCase("[]", 0)]
        public async Task CollectionProjectionPreservesUnknownVersusExplicitEmpty(string json, int? count)
        { Assert.That((await new Client(new State { Json = "{\"collections\":" + json + "}" }).GetUserDetailsAsync("context", "current")).Collections?.Count, Is.EqualTo(count)); }

        [Test]
        public async Task CollectionSharingFilterAndExtendedMetadataRemainOriginalAndDefensivelyCopied()
        {
            var state = new State { Json = "{\"collections\":[{\"id\":\"collection\",\"name\":\"Name\",\"owner\":\"owner\",\"public\":false,\"users\":[],\"groups\":[\"group\"],\"filter-params\":{\"time-from\":\"2026-10-12\",\"future\":5},\"extended-metadata\":{\"future\":null}}]}" };
            var result = (await new Client(state).GetUserDetailsAsync("context", "current")).Collections.Single();
            Assert.That(result.Owner, Is.EqualTo("owner")); Assert.That(result.Public, Is.False); Assert.That(result.Users, Is.Empty); Assert.That(result.Groups, Is.EqualTo(new[] { "group" }));
            Assert.That(((ICollection<string>)result.Groups).IsReadOnly, Is.True); var raw = result.GetRawDetails();
            Assert.That(raw["filter-params"]["time-from"].Type, Is.EqualTo(JTokenType.String)); Assert.That(raw["extended-metadata"]["future"].Type, Is.EqualTo(JTokenType.Null));
            raw["filter-params"]["future"] = 99; Assert.That(result.GetRawDetails()["filter-params"]["future"].Value<int>(), Is.EqualTo(5));
        }

        [TestCase(null), TestCase(false), TestCase(true)]
        public async Task SelectedFieldsAndNullableDeletedQueryAreCopiedWithoutInferredDefaults(bool? deleted)
        {
            var fields = new[] { "tenant", "future&field", "groups" }; var state = new State { BeforeAuthorize = () => fields[0] = "mutated" };
            await new Client(state).GetUserDetailsAsync("context", "selected/with?query", fields, deleted);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/user/selected%2Fwith%3Fquery"));
            var query = state.Request.Parameters.Where(p => p.Type == ParameterType.QueryString).ToDictionary(p => p.Name, p => p.Value.ToString());
            Assert.That(query["data"], Is.EqualTo("tenant,future&field,groups")); Assert.That(query.ContainsKey("fields"), Is.False);
            Assert.That(query.ContainsKey("include-deleted"), Is.EqualTo(deleted.HasValue)); if (deleted.HasValue) Assert.That(query["include-deleted"], Is.EqualTo(deleted.Value ? "true" : "false"));
        }

        [TestCase("current", "resolved"), TestCase("550e8400-e29b-41d4-a716-446655440000", "550E8400-E29B-41D4-A716-446655440000")]
        public async Task CurrentAliasAndCanonicalGuidEquivalenceRetainReturnedIdentity(string selected, string returned)
        { Assert.That((await new Client(new State { Json = "{\"id\":\"" + returned + "\"}" }).GetUserDetailsAsync("context", selected)).Id, Is.EqualTo(returned)); }

        [TestCase("{\"id\":\"other\"}"), TestCase("{\"id\":\" \"}"), TestCase("{\"id\":2}"), TestCase("{\"technical-user\":\"true\"}")]
        [TestCase("{\"first-name\":2}"), TestCase("{\"picture\":[]}"), TestCase("{\"picture\":{\"data\":2}}"), TestCase("{\"groups\":{}}")]
        [TestCase("{\"groups\":[null]}"), TestCase("{\"groups\":[{}]}"), TestCase("{\"groups\":[{\"id\":\"x\",\"color\":2147483648}]}")]
        [TestCase("{\"groups\":[{\"id\":\"x\",\"color\":\"0\"}]}"), TestCase("{\"collections\":[{}]}"), TestCase("{\"collections\":[{\"id\":\"x\",\"public\":0}]}")]
        [TestCase("{\"collections\":[{\"id\":\"x\",\"users\":[null]}]}"), TestCase("{\"collections\":[{\"id\":\"x\",\"filter-params\":[]}]}")]
        [TestCase("{\"role\":\"x\",\"role\":\"y\"}"), TestCase("{\"groups\":[{\"id\":\"x\",\"id\":\"y\"}]}"), TestCase("{} {}"), TestCase("[]")]
        public void InvalidSuccessfulMetadataRetainsAnInnerDiagnostic(string json)
        { Assert.That(Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = json }).GetUserDetailsAsync("context", "selected"))).InnerException, Is.Not.Null); }

        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidUserIdsFailBeforeAuthorization(string id)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetUserDetailsAsync("context", id))); Assert.That(state.Authorizations, Is.Zero); }

        [TestCase("empty"), TestCase("null"), TestCase("blank"), TestCase("comma")]
        public void InvalidFieldSelectionsFailBeforeAuthorization(string kind)
        {
            var state = new State(); var fields = kind == "empty" ? Array.Empty<string>() : new[] { kind == "null" ? null : kind == "blank" ? " " : "id,email" };
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetUserDetailsAsync("context", "selected", fields))); Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase(204), TestCase(403), TestCase(404), TestCase(500)]
        public void UnexpectedStatusesAreNotEmptySuccessfulDetails(int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).GetUserDetailsAsync("context", "selected"))); Assert.That(state.Calls, Is.EqualTo(1)); }

        [Test]
        public void PreCancellationDoesNotAuthorizeOrSend()
        {
            var state = new State(); using (var source = new CancellationTokenSource())
            { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).GetUserDetailsAsync("context", "selected", cancellationToken: source.Token))); }
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [Test]
        public async Task ActiveCancellationReachesNativeAuthorizationAndHttp()
        {
            var state = new State { Pending = true }; using (var source = new CancellationTokenSource())
            {
                var pending = new Client(state).GetUserDetailsAsync("context", "selected", cancellationToken: source.Token); await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending));
            }
        }

        [TestCase(false), TestCase(true)]
        public async Task FactorySendsBodylessSelectedProjectionThroughPrivateLoopback(bool sync)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string headers = null;
                var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync())
                    using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768)
                        {
                            if (await stream.ReadAsync(one, 0, 1, source.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        headers = Encoding.ASCII.GetString(bytes.ToArray()); var payload = Encoding.UTF8.GetBytes("{\"groups\":[{\"id\":\"all-users\"}],\"tenant-name\":\"Fixture\"}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, source.Token); await stream.WriteAsync(payload, 0, payload.Length, source.Token);
                    }
                });
                try
                {
                    var client = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).User; client.DisableOfflineModeSimulation();
                    var result = sync ? client.GetUserDetails("context", "current", new[] { "tenant", "groups" }, true, source.Token) : await client.GetUserDetailsAsync("context", "current", new[] { "tenant", "groups" }, true, source.Token);
                    await server; Assert.That(result.TenantName, Is.EqualTo("Fixture")); Assert.That(result.Groups.Single().Name, Is.Null);
                    var target = headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ')[1]; var query = target.Split('?')[1].Split('&').Select(p => p.Split(new[] { '=' }, 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
                    Assert.That(target.Split('?')[0], Is.EqualTo("/bridge/v2/user/current")); Assert.That(query["data"], Is.EqualTo("tenant,groups")); Assert.That(query["include-deleted"], Is.EqualTo("true")); Assert.That(query.Count, Is.EqualTo(2));
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Where(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).All(line => line.EndsWith(" 0")), Is.True);
                }
                finally
                { source.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { } }
            }
        }

        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-user-selected-details-tests";
        }
        private sealed class Factory : CenterDeviceClientBase
        { internal Factory(Config config) : base(new State(), config, null, "v2/") { } protected override string UploadLinkBaseUrl => "https://unused.invalid/"; }
        private sealed class Client : UserRestClient
        {
            private readonly State state; internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token);
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.OK; internal string Context, Json = "{}";
            internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken))
            { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token)
            { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json }; }
        }
    }
}
