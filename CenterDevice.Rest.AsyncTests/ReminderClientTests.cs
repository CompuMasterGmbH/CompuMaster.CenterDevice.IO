using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Reminders;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
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
    public class ReminderClientTests
    {
        private static JObject Record(string id = "reminder") => new JObject { ["id"] = id, ["type"] = "document", ["status"] = "pending", ["entity"] = "document",
            ["expiry-date"] = "2026-10-12T12:30:00+02:00", ["creation-date"] = "2026-10-01T10:00:00Z" };

        [TestCase(false, null), TestCase(true, null), TestCase(false, ""), TestCase(true, "ä note")]
        public async Task CreationPreservesFutureOffsetAndOmittedVersusExplicitNote(bool sync, string note)
        {
            var expiry = DateTimeOffset.UtcNow.AddDays(1).ToOffset(TimeSpan.FromHours(2));
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" };
            var client = new Client(state);
            var id = sync ? client.CreateReminder("context", "visible-document", expiry, note) : await client.CreateReminderAsync("context", "visible-document", expiry, note);
            Assert.That(id, Is.EqualTo("created"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/reminders"));
            Assert.That(Body(state).Value<string>("type"), Is.EqualTo("document"));
            Assert.That(Body(state).Value<string>("entity"), Is.EqualTo("visible-document"));
            Assert.That(DateTimeOffset.Parse(Body(state).Value<string>("expiry-date")), Is.EqualTo(expiry));
            Assert.That(DateTimeOffset.Parse(Body(state).Value<string>("expiry-date")).Offset, Is.EqualTo(TimeSpan.FromHours(2)));
            if (note == null) Assert.That(Body(state)["note"], Is.Null); else Assert.That(Body(state).Value<string>("note"), Is.EqualTo(note));
            Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public async Task SelectedRetrievalRetainsIdentityStatusDatesWithoutInventingOwner(bool sync)
        {
            var state = new State { Json = Record().ToString() }; var client = new Client(state);
            var result = sync ? client.GetReminder("context", "reminder") : await client.GetReminderAsync("context", "reminder");
            Assert.That(result.Id, Is.EqualTo("reminder"));
            Assert.That(result.EntityId, Is.EqualTo("document"));
            Assert.That(result.Status, Is.EqualTo("pending"));
            Assert.That(result.Note, Is.Null);
            Assert.That(result.ExpiryDate.Value.Offset, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(result.CreationDate.Value.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(result.ExpiryDateText, Is.EqualTo("2026-10-12T12:30:00+02:00"));
            Assert.That(typeof(Reminder).GetProperty("Owner"), Is.Null);
            Assert.That(typeof(Reminder).GetProperty("Recipient"), Is.Null);
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
            Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
        }

        [TestCase(""), TestCase(null)]
        public async Task OffsetFreeDatesAndFutureStatusesRemainExplicitWithoutLocalTimezone(string note)
        {
            var data = Record(); data["note"] = note; data["status"] = "future-status"; data["type"] = "future-type";
            data["expiry-date"] = "2026-10-12T12:30:00"; data["creation-date"] = "2026-10-01T10:00:00";
            var result = await new Client(new State { Json = data.ToString() }).GetReminderAsync("context", "reminder");
            Assert.That(result.ExpiryDate, Is.Null); Assert.That(result.CreationDate, Is.Null);
            Assert.That(result.ExpiryDateText, Is.EqualTo("2026-10-12T12:30:00"));
            Assert.That(result.Status, Is.EqualTo("future-status")); Assert.That(result.Type, Is.EqualTo("future-type"));
            Assert.That(result.Note, Is.EqualTo(note));
        }

        [TestCase(false), TestCase(true)]
        public async Task ListSnapshotsFiltersAndPreservesServerOrderAndReadOnlyPage(bool sync)
        {
            var statuses = new List<string> { "pending", "triggered" };
            var first = Record("first"); var second = Record("second"); second["status"] = "triggered";
            var state = new State { Json = new JObject { ["reminders"] = new JArray(first, second) }.ToString(), BeforeAuthorize = () => statuses.Clear() };
            var client = new Client(state);
            var page = sync ? client.GetReminders("context", statuses, "document") : await client.GetRemindersAsync("context", statuses, "document");
            Assert.That(page.Select(reminder => reminder.Id), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(((IList<Reminder>)page).IsReadOnly, Is.True);
            Assert.That(state.Request.Parameters.Single(parameter => parameter.Name == "statuses").Value, Is.EqualTo("pending,triggered"));
            Assert.That(state.Request.Parameters.Single(parameter => parameter.Name == "entity-id").Value, Is.EqualTo("document"));
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public async Task OmittedAndExplicitEmptyStatusFiltersRemainDistinct(bool empty)
        {
            var state = new State { Json = "{\"reminders\":[]}" };
            await new Client(state).GetRemindersAsync("context", empty ? new string[0] : null);
            var query = state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString).ToArray();
            Assert.That(query.Length, Is.EqualTo(empty ? 1 : 0));
            if (empty) Assert.That(query[0].Value, Is.EqualTo(""));
        }

        [TestCase("complete", false), TestCase("complete", true), TestCase("remove", false), TestCase("remove", true)]
        public async Task CommandsRetainExactSelectionAndDistinctPostVersusBodylessDelete(string operation, bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state);
            if (operation == "complete") { if (sync) client.CompleteReminder("context", "id with/slash"); else await client.CompleteReminderAsync("context", "id with/slash"); }
            else { if (sync) client.RemoveReminder("context", "id with/slash"); else await client.RemoveReminderAsync("context", "id with/slash"); }
            Assert.That(state.Request.Resource, Is.EqualTo("v2/reminder/id%20with%2Fslash"));
            Assert.That(state.Request.Method, Is.EqualTo(operation == "complete" ? Method.Post : Method.Delete));
            if (operation == "complete") { Assert.That(Body(state).Value<string>("action"), Is.EqualTo("complete")); Assert.That(Body(state)["params"], Is.Null); }
            else Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("document"), TestCase("expiry"), TestCase("note")]
        public void InvalidCreationRejectsBeforeAuthorization(string problem)
        {
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new Client(state).CreateReminderAsync("context", problem == "document" ? " " : "document", problem == "expiry" ? DateTimeOffset.MinValue : DateTimeOffset.UtcNow.AddDays(1), problem == "note" ? new string('x', 10001) : null); }));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [Test]
        public async Task MaximumNoteAndStatusEnumerationAreNotSilentlyTruncatedOrRepeated()
        {
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" };
            await new Client(state).CreateReminderAsync("context", "document", DateTimeOffset.UtcNow.AddDays(1), new string('x', 10000));
            Assert.That(Body(state).Value<string>("note").Length, Is.EqualTo(10000));
            var count = 0;
            IEnumerable<string> Statuses() { count++; yield return "pending"; yield return "pending"; }
            state = new State { Json = "{\"reminders\":[]}" };
            await new Client(state).GetRemindersAsync("context", Statuses());
            Assert.That(count, Is.EqualTo(1));
            Assert.That(state.Request.Parameters.Single(parameter => parameter.Name == "statuses").Value, Is.EqualTo("pending,pending"));
        }

        [TestCase("id"), TestCase("entity"), TestCase("status")]
        public void InvalidSelectionsRejectBeforeAuthorization(string problem)
        {
            var state = new State();
            if (problem == "id") Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new Client(state).CompleteReminderAsync("context", " "); }));
            else Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new Client(state).GetRemindersAsync("context", problem == "status" ? new[] { "future" } : null, problem == "entity" ? " " : null); }));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"id\":null}"), TestCase("{\"id\":1}"), TestCase("{\"id\":\" \"}"), TestCase("{\"id\":\"created\"} {}")]
        public void MalformedCreationRetainsOriginalCauseAndDoesNotReplay(string json)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = json };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).CreateReminderAsync("context", "document", DateTimeOffset.UtcNow.AddDays(1)); }));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("id"), TestCase("type"), TestCase("status"), TestCase("entity"), TestCase("expiry-date"), TestCase("creation-date"), TestCase("note")]
        public void MalformedRequiredMetadataAndNoteCannotBeGuessed(string field)
        {
            var data = Record(); data[field] = field == "expiry-date" || field == "creation-date" ? (JToken)"yesterday" : (JToken)1;
            var state = new State { Json = data.ToString() };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).GetReminderAsync("context", "reminder"); }));
        }

        [TestCase("root"), TestCase("null"), TestCase("entry"), TestCase("repeat"), TestCase("limit"), TestCase("entity"), TestCase("status")]
        public void InvalidPagesRepeatedIdsCapAndFilterMismatchesCannotBeClaimedValid(string problem)
        {
            var array = new JArray(Record());
            if (problem == "entry") array[0] = null;
            if (problem == "repeat") array.Add(Record());
            if (problem == "limit") array = new JArray(Enumerable.Range(0, 501).Select(index => Record(index.ToString())));
            var data = new JObject { ["reminders"] = problem == "null" ? null : array };
            if (problem == "root") data.Remove("reminders");
            var state = new State { Json = data.ToString() };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).GetRemindersAsync("context", problem == "status" ? new[] { "completed" } : null, problem == "entity" ? "other" : null); }));
        }

        [Test]
        public async Task CanonicalGuidSelectionPreservesOriginalReturnedSpelling()
        {
            var id = Guid.NewGuid(); var data = Record(id.ToString("B").ToUpperInvariant());
            var result = await new Client(new State { Json = data.ToString() }).GetReminderAsync("context", id.ToString("D"));
            Assert.That(result.Id, Is.EqualTo(id.ToString("B").ToUpperInvariant()));
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(new State { Json = data.ToString() }).GetReminderAsync("context", "other"); }));
        }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void CreatorVisibilityStateAndUncertainMutationFailuresRetainCause(int status)
        {
            var cause = new InvalidOperationException("isolated failure");
            var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await new Client(state).CompleteReminderAsync("context", "reminder"); }));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task PreAndActiveCancellationReachAuthorizationAndNativeHttp()
        {
            var state = new State { Pending = true };
            using (var token = new CancellationTokenSource())
            {
                token.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await new Client(state).RemoveReminderAsync("context", "reminder", token.Token); }));
                Assert.That(state.Authorizations, Is.Zero);
            }
            using (var token = new CancellationTokenSource())
            {
                var pending = new Client(state).GetReminderAsync("context", "reminder", token.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(token.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(token.Token));
                token.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await pending; }));
            }
        }

        [TestCase("create", 201), TestCase("create", 503), TestCase("get", 200), TestCase("list", 200), TestCase("get", 503), TestCase("complete", 204), TestCase("complete", 503), TestCase("remove", 204), TestCase("remove", 503)]
        public async Task PublicFactoryVerifiesRealWireAndPreservesGetRetriesWithoutMutationReplay(string operation, int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string header = null, sent = null; var requests = 0;
                var expectedRequests = operation == "get" && status == 503 ? 3 : 1;
                var server = Task.Run(async () =>
                {
                    for (var attempt = 0; attempt < expectedRequests; attempt++)
                    using (var socket = await listener.AcceptTcpClientAsync())
                    using (var stream = socket.GetStream())
                    {
                        requests++; var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768)
                        {
                            if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException();
                            bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        header = Encoding.ASCII.GetString(bytes.ToArray());
                        var lengthHeader = header.Split(new[] { "\r\n" }, StringSplitOptions.None).SingleOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                        var length = lengthHeader == null ? 0 : int.Parse(lengthHeader.Split(':')[1].Trim());
                        var received = new byte[length]; var count = 0;
                        while (count < length) { var read = await stream.ReadAsync(received, count, length - count, cancellation.Token); if (read == 0) throw new EndOfStreamException(); count += read; }
                        sent = Encoding.UTF8.GetString(received);
                        var response = status == 503 ? "{}" : operation == "create" ? "{\"id\":\"created\"}" : operation == "get" ? Record().ToString() : operation == "list" ? new JObject { ["reminders"] = new JArray(Record()) }.ToString() : "";
                        var payload = Encoding.UTF8.GetBytes(response);
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 201 ? " Created" : status == 200 ? " OK" : status == 204 ? " No Content" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token);
                        if (payload.Length != 0) await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var client = factory.Reminders; client.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        if (operation == "create") Assert.That(await client.CreateReminderAsync("context", "document", DateTimeOffset.UtcNow.AddDays(1), "ä note", cancellation.Token), Is.EqualTo("created"));
                        else if (operation == "get") Assert.That((await client.GetReminderAsync("context", "reminder", cancellation.Token)).Id, Is.EqualTo("reminder"));
                        else if (operation == "list") Assert.That((await client.GetRemindersAsync("context", new[] { "pending" }, "document", cancellation.Token)).Count, Is.EqualTo(1));
                        else if (operation == "complete") await client.CompleteReminderAsync("context", "reminder", cancellation.Token);
                        else await client.RemoveReminderAsync("context", "reminder", cancellation.Token);
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    var target = operation == "create" ? "POST /bridge/v2/reminders" : operation == "list" ? "GET /bridge/v2/reminders?statuses=pending&entity-id=document" :
                        (operation == "get" ? "GET" : operation == "complete" ? "POST" : "DELETE") + " /bridge/v2/reminder/reminder";
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo(target + " HTTP/1.1"));
                    if (operation == "create") Assert.That(JObject.Parse(sent).Value<string>("note"), Is.EqualTo("ä note"));
                    else if (operation == "complete") Assert.That(JObject.Parse(sent).Properties().Select(property => property.Name), Is.EqualTo(new[] { "action" }));
                    else Assert.That(sent, Is.Empty);
                    Assert.That(requests, Is.EqualTo(expectedRequests)); Assert.That(listener.Pending(), Is.False);
                }
                finally
                {
                    cancellation.Cancel(); listener.Stop();
                    try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private static JObject Body(State state)
        {
            using (var text = new StringReader((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader);
        }
        private sealed class Client : RemindersRestClient
        {
            private readonly State state;
            internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override async Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken))
            {
                state.Calls++; state.Request = request; state.Token = token; state.Entered.TrySetResult(true);
                if (state.Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse(request) { StatusCode = state.Status, Content = state.Json, ErrorException = state.Error };
            }
        }
        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(State state, Config config) : base(state, config, null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }
        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-reminder-tests";
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal string Json, Context;
            internal RestRequest Request;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal Exception Error;
            internal Action BeforeAuthorize;
            internal bool Pending;
            internal int Calls, Authorizations;
            internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization is required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken))
            {
                Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke();
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" });
            }
        }
    }
}
