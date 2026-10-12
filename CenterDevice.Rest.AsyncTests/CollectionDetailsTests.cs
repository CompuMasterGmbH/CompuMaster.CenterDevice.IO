using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.OAuth;
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
    public class CollectionDetailsTests
    {
        [TestCase(false), TestCase(true)]
        public async Task ExplicitSelectionAndOwnerFieldsAreCopiedWithoutImplicitDefaults(bool sync)
        {
            var fields = new[] { "extended-metadata", "auditing-users", "has-folders" }; var state = new State { BeforeAuthorize = () => fields[0] = "name" };
            var result = sync ? new Client(state).GetCollectionDetails("context", "part/with?query", fields) : await new Client(state).GetCollectionDetailsAsync("context", "part/with?query", fields);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/collection/part%2Fwith%3Fquery")); Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
            Assert.That(state.Request.Parameters.OfType<QueryParameter>().Single().Value, Is.EqualTo("extended-metadata,auditing-users,has-folders")); Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(result.Id, Is.Null); Assert.That(result.Public, Is.Null); Assert.That(result.HasFolders, Is.Null); Assert.That(result.Users, Is.Null); Assert.That(result.ArchivedDate, Is.Null);
        }
        [TestCase(false), TestCase(true)]
        public async Task OmittedFieldsKeepServerDefaultsAndExplicitZeroFalseRemainKnown(bool sync)
        {
            var state = new State { Content = "{\"id\":\"selected\",\"public\":false,\"has-folders\":false,\"users\":{\"visible\":[],\"not-visible-count\":0}}" };
            var result = sync ? new Client(state).GetCollectionDetails("context", "selected") : await new Client(state).GetCollectionDetailsAsync("context", "selected");
            Assert.That(state.Request.Parameters.OfType<QueryParameter>(), Is.Empty); Assert.That(result.Public, Is.False); Assert.That(result.HasFolders, Is.False); Assert.That(result.Users.Visible, Is.Empty); Assert.That(result.Users.NotVisibleCount, Is.EqualTo("0")); Assert.That(result.Groups, Is.Null);
        }
        [Test]
        public async Task CompleteMetadataPreservesOriginalDatesHiddenIdentitiesAndUnknownFields()
        {
            var state = new State { Content = "{\"id\":\"selected\",\"name\":\"original\",\"owner\":\"not-visible\",\"link\":\"link\",\"unique\":false,\"public\":true,\"inherited-sharings\":false,\"full-path\":true,\"auditing\":true,\"need-to-opt-in\":false,\"has-folders\":true,\"users\":{\"visible\":[\"u\",\"u\"],\"not-visible-count\":9223372036854775808},\"groups\":{\"visible\":[],\"not-visible-count\":1},\"extended-metadata\":{\"user.dates.original\":\"2099-12-31T23:59:00+02:00\",\"tags\":[\"a\",\"a\"],\"empty\":[]},\"filter-params\":{\"text\":\"exact\",\"future\":false},\"auditing-users\":{\"opted-in\":[\"u\"],\"opted-out\":[],\"pending\":[\"v\"]},\"auditing-email-settings\":{\"user-opted-in\":false,\"user-opted-out\":true,\"pending-responses\":\"future-period\"},\"archived-date\":\"2099-12-31T23:59:00+02:00\",\"expiry-date\":\"2100-01-01T00:00:00Z\",\"expiry-period\":{\"unit\":\"future-unit\",\"amount\":9223372036854775808,\"calendar\":false},\"future\":{\"date\":\"2099-12-31\"}}" };
            var result = await new Client(state).GetCollectionDetailsAsync("context", "selected");
            Assert.That(result.Name, Is.EqualTo("original")); Assert.That(result.Owner, Is.EqualTo("not-visible")); Assert.That(result.Link, Is.EqualTo("link")); Assert.That(result.Unique, Is.False); Assert.That(result.Public, Is.True); Assert.That(result.InheritedSharings, Is.False); Assert.That(result.FullPath, Is.True); Assert.That(result.Auditing, Is.True); Assert.That(result.NeedToOptIn, Is.False); Assert.That(result.HasFolders, Is.True);
            Assert.That(result.Users.Visible, Is.EqualTo(new[] { "u", "u" })); Assert.That(result.Users.NotVisibleCount, Is.EqualTo("9223372036854775808")); Assert.That(result.Groups.Visible, Is.Empty); Assert.That(result.Groups.NotVisibleCount, Is.EqualTo("1"));
            Assert.That(result.ArchivedDate, Is.EqualTo("2099-12-31T23:59:00+02:00")); Assert.That(result.ExpiryDate, Is.EqualTo("2100-01-01T00:00:00Z")); Assert.That(result.ExpiryPeriod.Amount, Is.EqualTo("9223372036854775808")); Assert.That(result.ExpiryPeriod.Unit, Is.EqualTo("future-unit")); Assert.That(result.ExpiryPeriod.Calendar, Is.False);
            Assert.That(result.AuditingUsers.OptedIn, Is.EqualTo(new[] { "u" })); Assert.That(result.AuditingUsers.OptedOut, Is.Empty); Assert.That(result.AuditingUsers.Pending, Is.EqualTo(new[] { "v" })); Assert.That(result.AuditingEmailSettings.UserOptedIn, Is.False); Assert.That(result.AuditingEmailSettings.UserOptedOut, Is.True); Assert.That(result.AuditingEmailSettings.PendingResponses, Is.EqualTo("future-period"));
            Assert.That(result.GetExtendedMetadata().Value<string>("user.dates.original"), Is.EqualTo("2099-12-31T23:59:00+02:00")); Assert.That(result.GetExtendedMetadata()["empty"], Is.Empty); Assert.That(result.GetFilterParameters().Value<string>("text"), Is.EqualTo("exact")); Assert.That(result.GetRawDetails()["future"]["date"].Type, Is.EqualTo(JTokenType.String));
            result.GetRawDetails()["owner"] = "changed"; result.GetExtendedMetadata()["tags"][0] = "changed"; result.GetFilterParameters()["text"] = "changed";
            Assert.That(result.Owner, Is.EqualTo("not-visible")); Assert.That(result.GetExtendedMetadata()["tags"][0].Value<string>(), Is.EqualTo("a")); Assert.That(result.GetFilterParameters().Value<string>("text"), Is.EqualTo("exact"));
            Assert.Throws<NotSupportedException>((Action)(() => ((IList<string>)result.Users.Visible).Add("changed")));
        }
        [Test]
        public async Task ExplicitNullsRemainUnknownAndInTheRawResponse()
        {
            var state = new State { Content = "{\"id\":null,\"public\":null,\"users\":null,\"has-folders\":null,\"extended-metadata\":null,\"filter-params\":null,\"expiry-period\":null,\"auditing-users\":null,\"auditing-email-settings\":null}" }; var result = await new Client(state).GetCollectionDetailsAsync("context", "selected");
            Assert.That(result.Id, Is.Null); Assert.That(result.Public, Is.Null); Assert.That(result.HasFolders, Is.Null); Assert.That(result.Users, Is.Null); Assert.That(result.GetExtendedMetadata(), Is.Null); Assert.That(result.GetFilterParameters(), Is.Null); Assert.That(result.GetRawDetails()["public"].Type, Is.EqualTo(JTokenType.Null));
        }
        [TestCase(""), TestCase("[]"), TestCase("null"), TestCase("{}{}"), TestCase("{\"public\":true,\"public\":false}"), TestCase("{\"id\":\"another\"}"), TestCase("{\"id\":\" \"}"), TestCase("{\"id\":5}")]
        [TestCase("{\"public\":0}"), TestCase("{\"has-folders\":\"false\"}"), TestCase("{\"users\":[]}"), TestCase("{\"users\":{\"visible\":[null]}}"), TestCase("{\"groups\":{\"not-visible-count\":-1}}"), TestCase("{\"groups\":{\"not-visible-count\":1.5}}")]
        [TestCase("{\"extended-metadata\":[]}"), TestCase("{\"extended-metadata\":{\"key\":null}}"), TestCase("{\"extended-metadata\":{\"key\":[false]}}"), TestCase("{\"filter-params\":[]}"), TestCase("{\"auditing-users\":{\"pending\":false}}"), TestCase("{\"auditing-email-settings\":{\"user-opted-in\":0}}"), TestCase("{\"expiry-period\":{\"amount\":1.5}}"), TestCase("{\"archived-date\":false}")]
        public void InvalidSuccessRetainsItsDiagnosticRatherThanFabricatingDetails(string content)
        { var state = new State { Content = content }; var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).GetCollectionDetailsAsync("context", "selected"))); Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1)); }
        [Test]
        public async Task CanonicalGuidIdentityComparisonPreservesOriginalReturnedIdentifier()
        { var state = new State { Content = "{\"id\":\"{A1111111-1111-1111-1111-111111111111}\"}" }; var result = await new Client(state).GetCollectionDetailsAsync("context", "a1111111-1111-1111-1111-111111111111"); Assert.That(result.Id, Is.EqualTo("{A1111111-1111-1111-1111-111111111111}")); }
        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void UnusableSelectionDoesNotAuthorize(string id)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetCollectionDetailsAsync("context", id))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("empty"), TestCase("blank"), TestCase("comma")]
        public void UnusableFieldSelectionDoesNotAuthorize(string kind)
        { var fields = kind == "empty" ? new string[0] : new[] { kind == "blank" ? " " : "id,name" }; var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetCollectionDetailsAsync("context", "selected", fields))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(204), TestCase(403), TestCase(404), TestCase(500)]
        public void OriginalStatusErrorsDoNotInferEmptyDetailsOrReplay(int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).GetCollectionDetailsAsync("context", "selected"))); Assert.That(state.Calls, Is.EqualTo(1)); }
        [Test]
        public void PreCancellationDoesNotAuthorizeOrSend()
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).GetCollectionDetailsAsync("context", "selected", cancellationToken: source.Token))); } Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public async Task ActiveCancellationReachesAuthorizationAndNativeHttp()
        { var state = new State { Pending = true }; using (var source = new CancellationTokenSource()) { var task = new Client(state).GetCollectionDetailsAsync("context", "selected", cancellationToken: source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await task)); } }
        [Test]
        public async Task FactoryReadsOriginalMetadataOverPrivateTcpLoopback()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string headers = null; var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync()) using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1]; while (bytes.Count < 32768) { if (await stream.ReadAsync(one, 0, 1, source.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]); if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break; }
                        headers = Encoding.ASCII.GetString(bytes.ToArray()); var body = "{\"id\":\"selected\",\"has-folders\":false,\"extended-metadata\":{\"key\":\"2099-12-31\"}}";
                        var reply = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\nConnection: close\r\n\r\n" + body); await stream.WriteAsync(reply, 0, reply.Length, source.Token);
                    }
                });
                try { var client = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).Collection; client.DisableOfflineModeSimulation(); var result = await client.GetCollectionDetailsAsync("context", "selected", new[] { "extended-metadata", "has-folders" }, source.Token); await server; Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Does.StartWith("GET /bridge/v2/collection/selected?fields=")); Assert.That(result.HasFolders, Is.False); Assert.That(result.GetExtendedMetadata()["key"].Type, Is.EqualTo(JTokenType.String)); }
                finally { source.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { } }
            }
        }
        private sealed class Config : IRestClientConfiguration
        { internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; } public string BaseAddress { get; } public string UserAgent => "isolated-collection-details-tests"; }
        private sealed class Factory : CenterDeviceClientBase
        { internal Factory(Config config) : base(new State(), config, null, "v2/") { } protected override string UploadLinkBaseUrl => "https://unused.invalid/"; }
        private sealed class Client : CollectionRestClient
        { private readonly State state; internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.OK; internal string Content = "{}", Context; internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Content }; }
        }
    }
}
