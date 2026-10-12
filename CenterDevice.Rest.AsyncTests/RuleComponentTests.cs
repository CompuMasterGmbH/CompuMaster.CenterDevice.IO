using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
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
    public class RuleComponentTests
    {
        [TestCase(false, null), TestCase(true, null), TestCase(false, false), TestCase(true, true)]
        public void ScopePreservesOmittedServerDefaultVersusExplicitDescendants(bool folder, bool? descendants)
        {
            var scope = folder ? RuleComponents.FolderScope(" selected ", descendants) : RuleComponents.CollectionScope(" selected ", descendants);
            Assert.That(scope.Type, Is.EqualTo(folder ? "folder" : "collection"));
            Assert.That(scope.Data.Value<string>("id"), Is.EqualTo(" selected "));
            if (descendants.HasValue) Assert.That(scope.Data.Value<bool>("include-subfolders"), Is.EqualTo(descendants.Value));
            else Assert.That(scope.Data["include-subfolders"], Is.Null);
        }

        [TestCase(false), TestCase(true)]
        public void TriggerIsMatchedToScopeWithExplicitEmptyData(bool folder)
        {
            var trigger = folder ? RuleComponents.DocumentAddedToFolderTrigger() : RuleComponents.DocumentAddedToCollectionTrigger();
            Assert.That(trigger.Type, Is.EqualTo(folder ? "document-added-to-folder" : "document-added-to-collection")); Assert.That(trigger.Data.Properties(), Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public void TagsSnapshotOnceAndRetainOriginalOrderCaseAndWhitespace(bool remove)
        {
            var enumerations = 0;
            IEnumerable<string> Tags() { enumerations++; yield return " ä "; yield return "Ä"; yield return ""; }
            var action = remove ? RuleComponents.RemoveTags(Tags()) : RuleComponents.AddTags(Tags());
            Assert.That(enumerations, Is.EqualTo(1)); Assert.That(action.Type, Is.EqualTo(remove ? "remove-tags" : "add-tags"));
            Assert.That(action.Data["tags"].Values<string>(), Is.EqualTo(new[] { " ä ", "Ä", "" }));
            action.Data["tags"][0] = "changed";
            Assert.That(action.Data["tags"][0].Value<string>(), Is.EqualTo(" ä "));
        }

        [TestCase("null"), TestCase("empty"), TestCase("null-item")]
        public void RequiredTagSelectionsRejectMissingOrEmptyShapes(string problem)
        {
            var values = problem == "null" ? null : problem == "empty" ? new string[0] : new[] { (string)null };
            Assert.Catch<ArgumentException>((TestDelegate)(() => RuleComponents.AddTags(values)));
            Assert.Catch<ArgumentException>((TestDelegate)(() => RuleComponents.RemoveTags(values)));
        }

        [Test]
        public void MetadataRetainsDecimalAndDateStringsWithoutCoercionAndSnapshotsSelections()
        {
            var fields = new List<RuleMetadataValue> { new RuleMetadataValue("user.strings.label", ""), new RuleMetadataValue("user.numbers.amount", "1.2300"), new RuleMetadataValue("user.dates.date", "2026-10-12T12:30:00+02:00") };
            var set = RuleComponents.SetExtendedMetadata(fields); fields.Clear();
            var array = (JArray)set.Data["metadata"];
            Assert.That(array.Count, Is.EqualTo(3)); Assert.That(array[1].Value<string>("value"), Is.EqualTo("1.2300"));
            Assert.That(array[1]["value"].Type, Is.EqualTo(JTokenType.String)); Assert.That(array[2].Value<string>("value"), Is.EqualTo("2026-10-12T12:30:00+02:00"));
            var remove = RuleComponents.RemoveExtendedMetadata(new[] { "user.numbers.amount", "user.strings.label" });
            Assert.That(remove.Type, Is.EqualTo("remove-extended-metadata")); Assert.That(remove.Data["metadata"].Values<string>(), Is.EqualTo(new[] { "user.numbers.amount", "user.strings.label" }));
        }

        [TestCase(null), TestCase(""), TestCase("other.strings.name"), TestCase("user.unknown.name"), TestCase("user.strings."), TestCase("user.dates. ")]
        public void MetadataRequiresCurrentWellKnownSectionAndField(string key)
        {
            Assert.Catch<ArgumentException>((TestDelegate)(() => new RuleMetadataValue(key, "value"))); Assert.Catch<ArgumentException>((TestDelegate)(() => RuleComponents.RemoveExtendedMetadata(new[] { key })));
        }

        [Test]
        public void OptionalExplicitEmptyMetadataIsRetainedAndMissingValuesAreRejected()
        {
            Assert.That(RuleComponents.SetExtendedMetadata(new RuleMetadataValue[0]).Data["metadata"], Is.TypeOf<JArray>().And.Empty);
            Assert.That(RuleComponents.RemoveExtendedMetadata(new string[0]).Data["metadata"], Is.TypeOf<JArray>().And.Empty);
            Assert.Catch<ArgumentException>((TestDelegate)(() => new RuleMetadataValue("user.strings.name", null)));
            Assert.Catch<ArgumentException>((TestDelegate)(() => RuleComponents.SetExtendedMetadata(null)));
            Assert.Catch<ArgumentException>((TestDelegate)(() => RuleComponents.SetExtendedMetadata(new RuleMetadataValue[] { null })));
        }

        [TestCase("add-to-collection"), TestCase("add-to-folder"), TestCase("move-to-collection"), TestCase("move-to-folder")]
        public void TargetsRemainExactAndDistinctWithoutHierarchyOrCycleGuessing(string type)
        {
            var action = type == "add-to-collection" ? RuleComponents.AddToCollection("selected") : type == "add-to-folder" ? RuleComponents.AddToFolder("selected") :
                type == "move-to-collection" ? RuleComponents.MoveToCollection("selected") : RuleComponents.MoveToFolder("selected");
            Assert.That(action.Type, Is.EqualTo(type)); Assert.That(action.Data.Value<string>(type.EndsWith("folder") ? "folder" : "collection"), Is.EqualTo("selected"));
        }

        [TestCase(RuleWorkflowCreator.RuleCreator, "rule-creator"), TestCase(RuleWorkflowCreator.RuleTriggerer, "rule-triggerer")]
        public void WorkflowRequesterUsesExplicitSelectionWithoutAuthContextSubstitution(RuleWorkflowCreator creator, string expected)
        {
            var action = RuleComponents.AddWorkflow("template", creator);
            Assert.That(action.Data.Value<string>("workflow-template"), Is.EqualTo("template")); Assert.That(action.Data.Value<string>("workflow-creator"), Is.EqualTo(expected));
        }

        [TestCase("target"), TestCase("creator"), TestCase("scope")]
        public void RequiredIdsAndUnsupportedCreatorEnumsAreRejected(string problem)
        {
            if (problem == "target") Assert.Catch<ArgumentException>((TestDelegate)(() => RuleComponents.MoveToFolder(" ")));
            else if (problem == "scope") Assert.Catch<ArgumentException>((TestDelegate)(() => RuleComponents.CollectionScope(null)));
            else Assert.Catch<ArgumentException>((TestDelegate)(() => RuleComponents.AddWorkflow("template", (RuleWorkflowCreator)99)));
        }

        [TestCase(null), TestCase(false), TestCase(true)]
        public void MarkerActionSnapshotsFormatsAndKeepsOptionalFalseDistinctFromDefault(bool? keepTags)
        {
            var formats = new List<RuleMarkerType> { RuleMarkerType.Code39, RuleMarkerType.Code128, RuleMarkerType.QrCode };
            var patterns = new List<string> { "^[A-Z]{2,}[0-9]+$" };
            var action = RuleComponents.SplitByMarker(formats, false, true, "user.strings.marker", keepTags, patterns); formats.Clear(); patterns.Clear();
            Assert.That(action.Type, Is.EqualTo("split-by-marker"));
            Assert.That(action.Data["marker-types"].Values<string>(), Is.EqualTo(new[] { "CODE_39", "CODE_128", "QR_CODE" }));
            Assert.That(action.Data.Value<bool>("remove-marker-pages"), Is.False); Assert.That(action.Data.Value<bool>("add-marker-to-filename"), Is.True);
            Assert.That(action.Data["code-patterns"].Values<string>(), Is.EqualTo(new[] { "^[A-Z]{2,}[0-9]+$" }));
            if (keepTags.HasValue) Assert.That(action.Data.Value<bool>("keep-tags"), Is.EqualTo(keepTags.Value)); else Assert.That(action.Data["keep-tags"], Is.Null);
        }

        [TestCase("null-types"), TestCase("empty-types"), TestCase("type"), TestCase("path"), TestCase("patterns"), TestCase("pattern-length"), TestCase("null-pattern")]
        public void UnsupportedMarkerSelectionAndPatternBoundsAreRejected(string problem)
        {
            var types = problem == "null-types" ? null : problem == "empty-types" ? new RuleMarkerType[0] : new[] { problem == "type" ? (RuleMarkerType)99 : RuleMarkerType.QrCode };
            var patterns = problem == "patterns" ? Enumerable.Repeat("a", 6).ToArray() : problem == "pattern-length" ? new[] { new string('a', 101) } : problem == "null-pattern" ? new[] { (string)null } : null;
            Assert.Catch<ArgumentException>((TestDelegate)(() => RuleComponents.SplitByMarker(types, false, false, problem == "path" ? " " : "user.strings.marker", codePatterns: patterns)));
        }

        [Test]
        public void EmptyVersusOmittedPatternsAndExactPatternBoundariesRemainExplicit()
        {
            Assert.That(RuleComponents.SplitByMarker(new[] { RuleMarkerType.QrCode }, false, false, "user.strings.marker").Data["code-patterns"], Is.Null);
            Assert.That(RuleComponents.SplitByMarker(new[] { RuleMarkerType.QrCode }, false, false, "user.strings.marker", codePatterns: new string[0]).Data["code-patterns"], Is.TypeOf<JArray>().And.Empty);
            Assert.That(((JArray)RuleComponents.SplitByMarker(new[] { RuleMarkerType.QrCode }, false, false, "user.strings.marker", codePatterns: Enumerable.Repeat(new string('a', 100), 5)).Data["code-patterns"]).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task PublicFactorySerializesAllTypedActionShapesWithoutExecutingRealRuleActions()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                JObject body = null;
                var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync()) using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768)
                        {
                            if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        var header = Encoding.ASCII.GetString(bytes.ToArray());
                        Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("POST /bridge/v2/rules HTTP/1.1"));
                        var length = int.Parse(header.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());
                        var data = new byte[length]; var received = 0;
                        while (received < length) { var count = await stream.ReadAsync(data, received, length - received, cancellation.Token); if (count == 0) throw new EndOfStreamException(); received += count; }
                        using (var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(Encoding.UTF8.GetString(data))) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None }) body = JObject.Load(reader);
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 201 Created\r\nContent-Type: application/json\r\nContent-Length: 16\r\nConnection: close\r\n\r\n{\"id\":\"created\"}");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token);
                    }
                });
                try
                {
                    var actions = new List<RuleComponent> { RuleComponents.AddTags(new[] { "ä" }), RuleComponents.RemoveTags(new[] { "old" }),
                        RuleComponents.SetExtendedMetadata(new[] { new RuleMetadataValue("user.numbers.amount", "1.2300") }), RuleComponents.RemoveExtendedMetadata(new[] { "user.strings.old" }),
                        RuleComponents.AddToCollection("collection"), RuleComponents.AddToFolder("folder"), RuleComponents.MoveToCollection("other-collection"), RuleComponents.MoveToFolder("other-folder"),
                        RuleComponents.AddWorkflow("template", RuleWorkflowCreator.RuleTriggerer), RuleComponents.SplitByMarker(new[] { RuleMarkerType.QrCode }, false, true, "user.strings.marker", codePatterns: new[] { "^[A-Z]{2,}$" }) };
                    var client = new Factory("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/").Rules; client.DisableOfflineModeSimulation();
                    Assert.That(await client.CreateRuleAsync("context", new RuleCreateRequest { Description = "typed", Scope = RuleComponents.CollectionScope("scope"), Trigger = RuleComponents.DocumentAddedToCollectionTrigger(), Actions = actions }, cancellation.Token), Is.EqualTo("created"));
                    await server;
                    Assert.That(((JArray)body["actions"]).Count, Is.EqualTo(10)); Assert.That(body["scope"]["data"]["include-subfolders"], Is.Null);
                    Assert.That(body["actions"][2]["data"]["metadata"][0].Value<string>("value"), Is.EqualTo("1.2300"));
                    Assert.That(body["actions"][8]["data"].Value<string>("workflow-creator"), Is.EqualTo("rule-triggerer"));
                    Assert.That(body["actions"][9]["data"]["keep-tags"], Is.Null); Assert.That(body["actions"][9]["data"]["code-patterns"].Values<string>(), Is.EqualTo(new[] { "^[A-Z]{2,}$" }));
                }
                finally
                {
                    cancellation.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(string address) : base(new Authorization(), new Config(address), null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }
        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address) { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-rule-component-tests";
        }
        private sealed class Authorization : IAsyncOAuthInfoProvider
        {
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization is required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) => Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" });
        }
    }
}
