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
    public class UserSettingsContractsTests
    {
        private const string Complete = "{\"locale\":{\"language\":\"de\",\"country\":\"DE\"},\"notifications\":{\"email\":{\"new-sharing\":true,\"new-collection-sharing\":false,\"new-version\":true,\"new-comment\":false,\"group-membership\":true,\"new-workflow\":false,\"new-workflow-confirmation-response\":true,\"new-workflow-rejection-response\":false,\"reminder-triggered\":true,\"daily-summary\":false},\"push\":{\"all\":false}},\"default-view-mode\":{\"view\":\"card\"},\"workflow-config\":{\"confirmation-mode\":\"one\",\"share-document-with-users\":false,\"visibility\":\"participant\"},\"default-collection\":\"my-documents\",\"high-contrast-mode\":false,\"search-include-archived-documents\":true,\"use-search-history\":false,\"favorites\":{\"users\":[\"user\"],\"groups\":[],\"collections\":[\"collection\"],\"folders\":[\"folder\"]},\"documents-sort-mode\":{\"fulltext\":{\"field\":\"score\",\"order\":\"desc\"},\"filter\":{\"field\":\"filename\",\"order\":\"asc\"}}}";

        [TestCase(false), TestCase(true)]
        public async Task CompleteTypedReadPreservesEveryDocumentedConfigurationAndDefensiveCopies(bool sync)
        {
            var state = new State { Json = Complete }; var client = new Client(state);
            var result = sync ? client.GetUserSettingsDetails("context", "current") : await client.GetUserSettingsDetailsAsync("context", "current");
            Assert.That(JToken.DeepEquals(result.GetRawSettings(), JObject.Parse(Complete)), Is.True);
            Assert.That(result.Locale.Language, Is.EqualTo("de")); Assert.That(result.Locale.Country, Is.EqualTo("DE"));
            var email = result.Notifications.Email;
            Assert.That(new[] { email.NewSharing, email.NewCollectionSharing, email.NewVersion, email.NewComment, email.GroupMembership, email.NewWorkflow, email.NewWorkflowConfirmationResponse, email.NewWorkflowRejectionResponse, email.ReminderTriggered, email.DailySummary }, Is.EqualTo(new bool?[] { true, false, true, false, true, false, true, false, true, false }));
            Assert.That(result.Notifications.Push.All, Is.False); Assert.That(result.DefaultViewMode.View, Is.EqualTo("card"));
            Assert.That(result.WorkflowConfig.ConfirmationMode, Is.EqualTo("one")); Assert.That(result.WorkflowConfig.ShareDocumentWithUsers, Is.False); Assert.That(result.WorkflowConfig.Visibility, Is.EqualTo("participant"));
            Assert.That(result.DefaultCollection, Is.EqualTo("my-documents")); Assert.That(result.HighContrastMode, Is.False); Assert.That(result.SearchIncludeArchivedDocuments, Is.True); Assert.That(result.UseSearchHistory, Is.False);
            Assert.That(result.Favorites.Users, Is.EqualTo(new[] { "user" })); Assert.That(result.Favorites.Groups, Is.Empty); Assert.That(result.Favorites.Collections, Is.EqualTo(new[] { "collection" })); Assert.That(result.Favorites.Folders, Is.EqualTo(new[] { "folder" }));
            Assert.That(result.DocumentsSortMode.Fulltext.Field, Is.EqualTo("score")); Assert.That(result.DocumentsSortMode.Fulltext.Order, Is.EqualTo("desc")); Assert.That(result.DocumentsSortMode.Filter.Field, Is.EqualTo("filename")); Assert.That(result.DocumentsSortMode.Filter.Order, Is.EqualTo("asc"));
            result.Locale.Language = "mutated"; result.Notifications.Push.All = true; result.GetRawSettings()["default-collection"] = "mutated";
            Assert.That(JToken.DeepEquals(result.GetRawSettings(), JObject.Parse(Complete)), Is.True);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/user/current/settings")); Assert.That(state.Request.Method, Is.EqualTo(Method.Get)); Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Request.Parameters.Where(p => p.Type == ParameterType.QueryString), Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public async Task CompleteTypedUpdateSendsOriginalWireFieldsAndSnapshotsBeforeAuthorization(bool sync)
        {
            var settings = TypedConfiguration(); var state = new State { Status = HttpStatusCode.NoContent, BeforeAuthorize = () => settings["locale"]["language"] = "mutated" }; var client = new Client(state);
            if (sync) client.UpdateUserSettings("context", "own/selected?", settings); else await client.UpdateUserSettingsAsync("context", "own/selected?", settings);
            Assert.That(JToken.DeepEquals(Body(state), JObject.Parse(Complete)), Is.True); Assert.That(state.Request.Resource, Is.EqualTo("v2/user/own%2Fselected%3F/settings")); Assert.That(state.Request.Method, Is.EqualTo(Method.Put));
            Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("users"), TestCase("groups"), TestCase("collections"), TestCase("folders")]
        public async Task IndividualEmptyFavoritesAreValidPartialUpdatesWithoutOtherDefaults(string selection)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var settings = new UserSettingsUpdateRequest { Favorites = new UserFavoritesConfiguration(new JObject { [selection] = new JArray() }) };
            await new Client(state).UpdateUserSettingsAsync("context", "current", settings);
            Assert.That(Body(state).Properties().Select(p => p.Name), Is.EqualTo(new[] { "favorites" })); Assert.That(((JObject)Body(state)["favorites"]).Properties().Select(p => p.Name), Is.EqualTo(new[] { selection })); Assert.That(Body(state)["favorites"][selection], Is.Empty);
        }

        [Test]
        public async Task TypedNullOmitsWhileFalseAndOriginalFutureFieldsRemainExplicit()
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var settings = new UserSettingsUpdateRequest { HighContrastMode = true, DefaultCollection = "first" };
            settings.HighContrastMode = null; settings.DefaultCollection = ""; settings.UseSearchHistory = false; settings["future"] = new JObject { ["date"] = "2026-10-12" };
            await new Client(state).UpdateUserSettingsAsync("context", "current", settings);
            Assert.That(Body(state).Property("high-contrast-mode"), Is.Null); Assert.That(Body(state).Value<string>("default-collection"), Is.Empty); Assert.That(Body(state).Value<bool>("use-search-history"), Is.False); Assert.That(Body(state)["future"]["date"].Type, Is.EqualTo(JTokenType.String));
        }

        [TestCase("{}"), TestCase("{\"locale\":null,\"high-contrast-mode\":null}"), TestCase("{\"locale\":{\"language\":\"future\"}}")]
        public async Task ReadProjectionsRemainNullableAndDoNotRequireCompleteWriteShapes(string json)
        {
            var result = await new Client(new State { Json = json }).GetUserSettingsDetailsAsync("context", "current");
            Assert.That(result.HighContrastMode, Is.Null); Assert.That(result.Notifications, Is.Null); Assert.That(result.TenantSettings, Is.Null);
            if (result.Locale != null) { Assert.That(result.Locale.Language, Is.EqualTo("future")); Assert.That(result.Locale.Country, Is.Null); }
        }

        [Test]
        public async Task FalseTenantIndicationsAreRetainedWithoutDenyingConditionalRights()
        {
            var result = await new Client(new State { Json = "{\"tenant-settings\":{\"unarchiving\":false,\"future-grant\":true,\"unknown\":null}}" }).GetUserSettingsDetailsAsync("context", "current");
            Assert.That(result.TenantSettings["unarchiving"], Is.False); Assert.That(result.TenantSettings["future-grant"], Is.True); Assert.That(result.TenantSettings["unknown"], Is.Null);
        }

        [Test]
        public async Task QuerySelectionIsCopiedBeforeAuthorizationAndUsesData()
        {
            var fields = new[] { "locale", "tenant-settings" }; var state = new State { BeforeAuthorize = () => fields[0] = "mutated" };
            await new Client(state).GetUserSettingsDetailsAsync("context", "current", fields);
            var query = state.Request.Parameters.Single(p => p.Type == ParameterType.QueryString); Assert.That(query.Name, Is.EqualTo("data")); Assert.That(query.Value, Is.EqualTo("locale,tenant-settings"));
        }

        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidSelectedIdentityFailsBeforeReadingOrUpdating(string id)
        {
            var state = new State(); var client = new Client(state);
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await client.GetUserSettingsDetailsAsync("context", id)));
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await client.UpdateUserSettingsAsync("context", id, new UserSettingsUpdateRequest())));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("empty"), TestCase("null"), TestCase("blank"), TestCase("comma")]
        public void InvalidReadSelectionsFailBeforeAuthorization(string kind)
        {
            var state = new State(); var fields = kind == "empty" ? Array.Empty<string>() : new[] { kind == "null" ? null : kind == "blank" ? " " : "locale,favorites" };
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetUserSettingsDetailsAsync("context", "current", fields)));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [Test]
        public void NullUpdateIsNotAnImplicitDefaultOrReset()
        {
            var state = new State(); Assert.ThrowsAsync<ArgumentNullException>((Func<Task>)(async () => await new Client(state).UpdateUserSettingsAsync("context", "current", null)));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [Test]
        public async Task NestedTypedConfigurationAndFavoriteListsAreCopiedAndNullOmitted()
        {
            var users = new[] { "original" }; var favorites = new UserFavoritesConfiguration { Users = users, Groups = Array.Empty<string>() };
            var settings = new UserSettingsUpdateRequest { Favorites = favorites, Locale = new UserLocaleConfiguration { Language = "de", Country = "DE" } };
            users[0] = "mutated"; favorites.Users = new[] { "changed" }; settings.Locale = null;
            var state = new State { Status = HttpStatusCode.NoContent }; await new Client(state).UpdateUserSettingsAsync("context", "current", settings);
            Assert.That(Body(state).Property("locale"), Is.Null); Assert.That(Body(state)["favorites"]["users"].Values<string>(), Is.EqualTo(new[] { "original" }));
            Assert.That(Body(state)["favorites"]["groups"], Is.Empty);
        }

        [Test]
        public async Task EmptyExplicitUpdateDoesNotInsertSettings()
        {
            var state = new State { Status = HttpStatusCode.NoContent }; await new Client(state).UpdateUserSettingsAsync("context", "current", new UserSettingsUpdateRequest());
            Assert.That(Body(state).Properties(), Is.Empty);
        }

        [TestCase("email"), TestCase("push")]
        public async Task ACompleteIndividualNotificationChannelDoesNotFillTheOtherChannel(string channel)
        {
            var settings = TypedConfiguration(); ((JObject)settings["notifications"]).Remove(channel == "email" ? "push" : "email");
            var state = new State { Status = HttpStatusCode.NoContent }; await new Client(state).UpdateUserSettingsAsync("context", "current", settings);
            Assert.That(((JObject)Body(state)["notifications"]).Properties().Select(p => p.Name), Is.EqualTo(new[] { channel }));
        }

        [TestCase("{\"locale\":{\"language\":\"de\"}}"), TestCase("{\"locale\":null}"), TestCase("{\"notifications\":{}}"), TestCase("{\"notifications\":{\"push\":{}}}")]
        [TestCase("{\"notifications\":{\"email\":{\"new-sharing\":true}}}"), TestCase("{\"default-view-mode\":{}}"), TestCase("{\"workflow-config\":{\"confirmation-mode\":\"one\",\"visibility\":\"participant\"}}")]
        [TestCase("{\"high-contrast-mode\":\"false\"}"), TestCase("{\"favorites\":{\"users\":[null]}}"), TestCase("{\"favorites\":{\"groups\":{}}}")]
        [TestCase("{\"documents-sort-mode\":{\"fulltext\":{\"field\":\"score\",\"order\":\"desc\"}}}"), TestCase("{\"documents-sort-mode\":{\"fulltext\":{\"field\":\"score\",\"order\":\"DESC\"},\"filter\":{\"field\":\"filename\",\"order\":\"asc\"}}}")]
        [TestCase("{\"documents-sort-mode\":{\"fulltext\":{\"field\":\"score\",\"order\":\"desc\"},\"filter\":{\"field\":\"score\",\"order\":\"asc\"}}}"), TestCase("{\"tenant-settings\":{\"unarchiving\":true}}")]
        public void IncompleteOrReadOnlyUpdatesFailBeforeAuthorization(string json)
        {
            var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).UpdateUserSettingsAsync("context", "current", new UserSettingsUpdateRequest(JObject.Parse(json)))));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("[]"), TestCase("{} {}"), TestCase("{\"locale\":{},\"locale\":{}}"), TestCase("{\"high-contrast-mode\":1}"), TestCase("{\"notifications\":{\"push\":{\"all\":\"true\"}}}"), TestCase("{\"tenant-settings\":{\"unarchiving\":1}}")]
        public void MalformedSuccessfulSettingsRetainAnInnerDiagnostic(string json)
        { Assert.That(Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = json }).GetUserSettingsDetailsAsync("context", "current"))).InnerException, Is.Not.Null); }

        [TestCase(false, 204), TestCase(false, 403), TestCase(true, 200), TestCase(true, 403), TestCase(true, 500)]
        public void UnexpectedStatusesDoNotBecomeSuccessfulSettings(bool write, int status)
        {
            var state = new State { Status = (HttpStatusCode)status }; var client = new Client(state);
            Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { if (write) await client.UpdateUserSettingsAsync("context", "current", new UserSettingsUpdateRequest()); else await client.GetUserSettingsDetailsAsync("context", "current"); })); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public void PreCancellationDoesNotAuthorizeOrSend(bool write)
        {
            var state = new State(); var client = new Client(state); using (var source = new CancellationTokenSource())
            { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => { if (write) await client.UpdateUserSettingsAsync("context", "current", new UserSettingsUpdateRequest(), source.Token); else await client.GetUserSettingsDetailsAsync("context", "current", cancellationToken: source.Token); })); }
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [TestCase(false), TestCase(true)]
        public async Task ActiveCancellationReachesNativeAuthorizationAndHttp(bool write)
        {
            var state = new State { Pending = true }; var client = new Client(state); using (var source = new CancellationTokenSource())
            {
                var pending = write ? client.UpdateUserSettingsAsync("context", "current", new UserSettingsUpdateRequest(), source.Token) : (Task)client.GetUserSettingsDetailsAsync("context", "current", cancellationToken: source.Token);
                await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending));
            }
        }

        [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
        public async Task FactoryUsesOriginalGetProjectionOrJsonPutOverPrivateLoopback(bool write, bool sync)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string headers = null, sent = null;
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
                        headers = Encoding.ASCII.GetString(bytes.ToArray());
                        if (write)
                        {
                            var size = int.Parse(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1]);
                            var body = new byte[size]; var count = 0;
                            while (count < size) { var read = await stream.ReadAsync(body, count, size - count, source.Token); if (read == 0) throw new EndOfStreamException(); count += read; }
                            sent = Encoding.UTF8.GetString(body);
                        }
                        var payload = write ? Array.Empty<byte>() : Encoding.UTF8.GetBytes("{\"locale\":{\"language\":\"de\",\"country\":\"DE\"}} ");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + (write ? "204 No Content" : "200 OK") + "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, source.Token); if (payload.Length > 0) await stream.WriteAsync(payload, 0, payload.Length, source.Token);
                    }
                });
                try
                {
                    var client = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).UserSettings; client.DisableOfflineModeSimulation();
                    if (write)
                    {
                        var settings = new UserSettingsUpdateRequest { Favorites = new UserFavoritesConfiguration { Collections = Array.Empty<string>() }, HighContrastMode = false };
                        if (sync) client.UpdateUserSettings("context", "current", settings, source.Token); else await client.UpdateUserSettingsAsync("context", "current", settings, source.Token);
                    }
                    else
                    {
                        var result = sync ? client.GetUserSettingsDetails("context", "current", new[] { "locale", "future&field" }, source.Token) : await client.GetUserSettingsDetailsAsync("context", "current", new[] { "locale", "future&field" }, source.Token);
                        Assert.That(result.Locale.Country, Is.EqualTo("DE"));
                    }
                    await server; var requestLine = headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
                    Assert.That(requestLine.Split(' ')[0], Is.EqualTo(write ? "PUT" : "GET"));
                    if (write)
                    {
                        Assert.That(requestLine.Split(' ')[1], Is.EqualTo("/bridge/v2/user/current/settings"));
                        Assert.That(JToken.DeepEquals(JObject.Parse(sent), JObject.Parse("{\"favorites\":{\"collections\":[]},\"high-contrast-mode\":false}")), Is.True);
                    }
                    else
                    {
                        var target = requestLine.Split(' ')[1]; Assert.That(target.Split('?')[0], Is.EqualTo("/bridge/v2/user/current/settings"));
                        Assert.That(Uri.UnescapeDataString(target.Split('?')[1]), Is.EqualTo("data=locale,future&field"));
                        Assert.That(target.Split('?')[1].Split('&').Length, Is.EqualTo(1));
                    }
                }
                finally
                { source.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { } }
            }
        }

        private static UserSettingsUpdateRequest TypedConfiguration() => new UserSettingsUpdateRequest
        {
            Locale = new UserLocaleConfiguration { Language = "de", Country = "DE" },
            Notifications = new UserNotificationConfiguration
            {
                Email = new UserEmailNotificationConfiguration
                {
                    NewSharing = true, NewCollectionSharing = false, NewVersion = true, NewComment = false,
                    GroupMembership = true, NewWorkflow = false, NewWorkflowConfirmationResponse = true,
                    NewWorkflowRejectionResponse = false, ReminderTriggered = true, DailySummary = false
                },
                Push = new UserPushNotificationConfiguration { All = false }
            },
            DefaultViewMode = new UserDefaultViewConfiguration { View = "card" },
            WorkflowConfig = new UserWorkflowConfiguration { ConfirmationMode = "one", ShareDocumentWithUsers = false, Visibility = "participant" },
            DefaultCollection = "my-documents", HighContrastMode = false, SearchIncludeArchivedDocuments = true, UseSearchHistory = false,
            Favorites = new UserFavoritesConfiguration { Users = new[] { "user" }, Groups = Array.Empty<string>(), Collections = new[] { "collection" }, Folders = new[] { "folder" } },
            DocumentsSortMode = new UserDocumentsSortConfiguration
            {
                Fulltext = new UserDocumentSortConfiguration { Field = "score", Order = "desc" },
                Filter = new UserDocumentSortConfiguration { Field = "filename", Order = "asc" }
            }
        };

        private static JObject Body(State state) => JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private sealed class Config : IRestClientConfiguration
        { internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; } public string BaseAddress { get; } public string UserAgent => "isolated-user-settings-tests"; }
        private sealed class Factory : CenterDeviceClientBase
        { internal Factory(Config config) : base(new State(), config, null, "v2/") { } protected override string UploadLinkBaseUrl => "https://unused.invalid/"; }
        private sealed class Client : UserSettingsRestClient
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
