using CenterDevice.Rest.Clients.Trash;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    public partial class TrashClientTests
    {
        [TestCase(false), TestCase(true)]
        public async Task TrashSearchSnapshotsAllNestedOptionsBeforeAuthorization(bool sync)
        {
            var when = DateTimeOffset.Parse("2026-10-12T08:09:10+02:00");
            var request = new TrashSearchRequest
            {
                Query = new TrashSearchQuery { Text = "ä \"quoted\"" }, Offset = 3, Rows = 1000,
                Filter = new TrashSearchFilter { Ids = new List<string> { "owned" }, Filenames = new List<string> { "ä.txt" },
                    Extensions = new List<string> { "txt" }, MimeTypes = new List<string> { "text/plain" }, Tags = new List<string>(),
                    Size = new TrashSearchSizeRange { From = 5L * 1024 * 1024 * 1024 },
                    UploadDate = new TrashSearchDateRange { To = when }, LastChange = new TrashSearchDateRange { From = when },
                    DeletionDate = new TrashSearchDateRange { From = when, To = when.AddDays(1) } },
                Fields = new TrashSearchFields { Includes = new List<string> { "id", "filename", "score" } },
                Sort = new TrashSearchSort { Field = "deletion-date", Order = "desc" },
                Facets = new List<TrashSearchFacet> { new TrashSearchFacet { Field = "tags", Size = 4, Qualifier = "top" } }
            };
            var state = new State { Json = "{\"hits\":4,\"documents\":[{\"id\":\"owned\",\"filename\":\"ä.txt\",\"score\":1.5}]}" };
            state.BeforeAuthorize = () => { request.Query.Text = "changed"; request.Filter.Ids.Clear(); request.Filter.DeletionDate.From = null;
                request.Fields.Includes.Clear(); request.Sort.Order = "asc"; request.Facets[0].Size = 1; request.Offset = 50; };
            var result = sync ? new Client(state).Search("context", request) : await new Client(state).SearchAsync("context", request);
            var body = JObject.Parse(state.Body);
            Assert.That(body.Properties().Select(property => property.Name), Is.EquivalentTo(new[] { "action", "params" }));
            Assert.That(body["action"].Value<string>(), Is.EqualTo("search"));
            var options = body["params"];
            Assert.That(options["query"]["text"].Value<string>(), Is.EqualTo("ä \"quoted\""));
            Assert.That(options["offset"].Value<int>(), Is.EqualTo(3));
            Assert.That(options["rows"].Value<int>(), Is.EqualTo(1000), "Do not invent a search-specific 500-row cap from the GET listing contract.");
            Assert.That(options["filter"]["ids"].Values<string>(), Is.EqualTo(new[] { "owned" }));
            Assert.That(options["filter"]["extensions"].Values<string>(), Is.EqualTo(new[] { "txt" }));
            Assert.That(options["filter"]["tags"], Is.Empty);
            Assert.That(options["filter"]["size"]["from"].Value<long>(), Is.EqualTo(5L * 1024 * 1024 * 1024));
            Assert.That(options["filter"]["size"]["to"], Is.Null);
            Assert.That(state.Body, Does.Contain("2026-10-12T08:09:10+02:00"));
            Assert.That(options["fields"]["includes"].Values<string>(), Is.EqualTo(new[] { "id", "filename", "score" }));
            Assert.That(options["sort"]["order"].Value<string>(), Is.EqualTo("desc"));
            Assert.That(options["facet"][0]["size"].Value<int>(), Is.EqualTo(4));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/trash"));
            Assert.That(result.Hits, Is.EqualTo(4));
            Assert.That(result.Documents.Single().Score, Is.EqualTo(1.5));
            Assert.That(result.Documents.Single().UploadDate, Is.Null);
        }

        [Test]
        public async Task EmptyOptionsPreserveServerDefaultsAndNoFacetMetadata()
        {
            var state = new State { Json = "{\"hits\":0}" };
            var result = await new Client(state).SearchAsync("context", new TrashSearchRequest());
            Assert.That(JObject.Parse(state.Body)["params"].Children(), Is.Empty);
            Assert.That(result.Documents, Is.Empty);
            Assert.That(result.Facets, Is.Null);
            Assert.That(typeof(TrashSearchDocument).GetProperty("Owner"), Is.Null);
            Assert.That(typeof(TrashSearchDocument).GetProperty("Version"), Is.Null);
        }

        [TestCase(0, null, 0), TestCase(1, null, 1), TestCase(0, 0, 9), TestCase(10, null, 2)]
        public async Task OmittedDocumentsAreKnownEmptyOnlyFromHitsOffsetOrFacetOnlyRows(int offset, int? rows, long hits)
        {
            var state = new State { Json = "{\"hits\":" + hits + "}" };
            var result = await new Client(state).SearchAsync("context", new TrashSearchRequest { Offset = offset, Rows = rows });
            Assert.That(result.Hits, Is.EqualTo(hits));
            Assert.That(result.Documents, Is.Empty);
        }

        [Test]
        public async Task LargeHitsOptionalMetadataAndCompleteFacetJsonRemainDistinct()
        {
            var state = new State { Json = "{\"hits\":5000000000,\"documents\":[{\"id\":\"a\",\"size\":5368709120,\"tags\":[],\"deletion-date\":\"2026-10-12T08:09:10+02:00\"},{\"id\":\"b\"}],\"facets\":{\"tags.top\":{\"total\":9,\"other\":2,\"not-visible\":1,\"terms\":[{\"term\":\"ä\",\"count\":9}]},\"size\":{\"ranges\":[{\"name\":\"large\",\"from\":5368709120,\"to\":null,\"count\":0}]},\"deletion-date\":{\"ranges\":[{\"from\":\"2026-10-12T08:09:10+02:00\",\"to\":null,\"count\":2}]}}}" };
            var result = await new Client(state).SearchAsync("context", new TrashSearchRequest());
            Assert.That(result.Hits, Is.EqualTo(5000000000L));
            Assert.That(result.Documents.Select(document => document.Id), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(result.Documents[0].Size, Is.EqualTo(5368709120L));
            Assert.That(result.Documents[0].Tags, Is.Empty);
            Assert.That(result.Documents[1].Tags, Is.Null);
            Assert.That(result.Documents[1].Size, Is.Null);
            Assert.That(result.Documents[0].DeletionDate.Value.Offset, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(result.Facets["size"]["ranges"][0]["from"].Value<long>(), Is.EqualTo(5368709120L));
            Assert.That(result.Facets["size"]["ranges"][0]["count"].Value<int>(), Is.Zero);
            Assert.That(result.Facets["deletion-date"]["ranges"][0]["from"].Type, Is.EqualTo(JTokenType.String));
            var copy = result.Facets; copy.RemoveAll();
            Assert.That(result.Facets.Count, Is.EqualTo(3), "Facet snapshots must not mutate the stored response.");
            Assert.Throws<NotSupportedException>((Action)(() => { ((IList<TrashSearchDocument>)result.Documents).Clear(); }));
        }

        [Test]
        public async Task ExplicitlyExcludedIdentityAndEmptyQueryListsRemainExplicit()
        {
            var state = new State { Json = "{\"hits\":1,\"documents\":[{\"filename\":\"a.txt\"}],\"facets\":{}}" };
            var request = new TrashSearchRequest { Query = new TrashSearchQuery { Text = "" }, Filter = new TrashSearchFilter { Tags = new List<string>() },
                Fields = new TrashSearchFields { Excludes = new List<string> { "id" } }, Facets = new List<TrashSearchFacet>() };
            var result = await new Client(state).SearchAsync("context", request);
            Assert.That(result.Documents.Single().Id, Is.Null);
            Assert.That(result.Facets, Is.Empty);
            var options = JObject.Parse(state.Body)["params"];
            Assert.That(options["query"]["text"].Value<string>(), Is.Empty);
            Assert.That(options["filter"]["tags"], Is.Empty);
            Assert.That(options["facet"], Is.Empty);
        }

        [Test]
        public async Task AllSupportedProjectionsSortsFacetsAndOneSidedBoundsUseExactWireNames()
        {
            var fields = new[] { "id", "upload-date", "filename", "size", "mimetype", "version-date", "tags", "score", "deletion-date" };
            var facets = new[] { "extension", "filename", "mimetype", "size", "tags", "upload-date", "version-date", "deletion-date" };
            foreach (var sort in new[] { "score", "filename", "upload-date", "version-date", "size", "deletion-date" })
            {
                var state = new State { Json = "{\"hits\":0}" };
                await new Client(state).SearchAsync("context", new TrashSearchRequest { Fields = new TrashSearchFields { Includes = fields.ToList() },
                    Sort = new TrashSearchSort { Field = sort }, Facets = facets.Select(field => new TrashSearchFacet { Field = field }).ToList(),
                    Filter = new TrashSearchFilter { Size = new TrashSearchSizeRange { To = long.MaxValue },
                        UploadDate = new TrashSearchDateRange { From = DateTimeOffset.Parse("2026-10-12T00:00:00Z") },
                        LastChange = new TrashSearchDateRange { To = DateTimeOffset.Parse("2026-10-12T01:00:00Z") } } });
                var options = JObject.Parse(state.Body)["params"];
                Assert.That(options["fields"]["includes"].Values<string>(), Is.EqualTo(fields));
                Assert.That(options["facet"].Select(facet => facet["field"].Value<string>()), Is.EqualTo(facets));
                Assert.That(options["sort"]["field"].Value<string>(), Is.EqualTo(sort));
                Assert.That(options["sort"]["order"], Is.Null, "Omitted ordering must remain omitted.");
                Assert.That(options["filter"]["size"]["to"].Value<long>(), Is.EqualTo(long.MaxValue));
                Assert.That(options["filter"]["size"]["from"], Is.Null);
                Assert.That(options["filter"]["upload-date"]["to"], Is.Null);
                Assert.That(options["filter"]["last-change"]["from"], Is.Null);
            }
        }

        [Test]
        public async Task QualifiedFacetsOfSameFieldKeepDistinctResponseNames()
        {
            var state = new State { Json = "{\"hits\":0}" };
            await new Client(state).SearchAsync("context", new TrashSearchRequest { Facets = new List<TrashSearchFacet> {
                new TrashSearchFacet { Field = "tags", Qualifier = "first", Size = 1 },
                new TrashSearchFacet { Field = "tags", Qualifier = "second", Size = 2 } } });
            Assert.That(JObject.Parse(state.Body)["params"]["facet"].Select(facet => facet["qualifier"].Value<string>()), Is.EqualTo(new[] { "first", "second" }));
        }

        private static IEnumerable<TrashSearchRequest> InvalidTrashSearchRequests()
        {
            yield return null;
            yield return new TrashSearchRequest { Offset = -1 };
            yield return new TrashSearchRequest { Rows = -1 };
            yield return new TrashSearchRequest { Fields = new TrashSearchFields { Includes = new List<string>(), Excludes = new List<string>() } };
            foreach (var name in new[] { "owner", "version", "folders", "collections", "unknown", null })
                yield return new TrashSearchRequest { Fields = new TrashSearchFields { Includes = new List<string> { name } } };
            foreach (var size in new[] { new TrashSearchSizeRange { From = -1 }, new TrashSearchSizeRange { To = -1 }, new TrashSearchSizeRange { From = 2, To = 1 } })
                yield return new TrashSearchRequest { Filter = new TrashSearchFilter { Size = size } };
            yield return new TrashSearchRequest { Filter = new TrashSearchFilter { Ids = new List<string> { " " } } };
            yield return new TrashSearchRequest { Filter = new TrashSearchFilter { Tags = new List<string> { null } } };
            yield return new TrashSearchRequest { Filter = new TrashSearchFilter { DeletionDate = new TrashSearchDateRange { From = DateTimeOffset.UtcNow, To = DateTimeOffset.UtcNow.AddDays(-1) } } };
            yield return new TrashSearchRequest { Sort = new TrashSearchSort { Field = "owner" } };
            yield return new TrashSearchRequest { Sort = new TrashSearchSort { Field = "filename", Order = "up" } };
            yield return new TrashSearchRequest { Facets = new List<TrashSearchFacet> { null } };
            yield return new TrashSearchRequest { Facets = new List<TrashSearchFacet> { new TrashSearchFacet { Field = "owner" } } };
            yield return new TrashSearchRequest { Facets = new List<TrashSearchFacet> { new TrashSearchFacet { Field = "tags", Size = 0 } } };
            yield return new TrashSearchRequest { Facets = new List<TrashSearchFacet> { new TrashSearchFacet { Field = "tags" }, new TrashSearchFacet { Field = "tags" } } };
            yield return new TrashSearchRequest { Facets = new List<TrashSearchFacet> { new TrashSearchFacet { Field = "tags" }, new TrashSearchFacet { Field = "tags", Qualifier = "qualified" } } };
            yield return new TrashSearchRequest { Facets = new List<TrashSearchFacet> { new TrashSearchFacet { Field = "tags", Qualifier = " " } } };
        }

        [TestCaseSource(nameof(InvalidTrashSearchRequests))]
        public void InvalidTrashSearchRejectsBeforeAuthorization(TrashSearchRequest request)
        {
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new Client(state).SearchAsync("context", request); }));
            Assert.That(state.AuthorizationCalls, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("{}"), TestCase("null"), TestCase("{\"hits\":-1}"), TestCase("{\"hits\":1.5}"), TestCase("{\"hits\":\"1\"}")]
        [TestCase("{\"hits\":1}"), TestCase("{\"hits\":1,\"documents\":[]}"), TestCase("{\"hits\":1,\"documents\":{}}")]
        [TestCase("{\"hits\":1,\"documents\":[null]}"), TestCase("{\"hits\":1,\"documents\":[{}]}")]
        [TestCase("{\"hits\":1,\"documents\":[{\"id\":4}]}"), TestCase("{\"hits\":1,\"documents\":[{\"id\":\"a\",\"size\":1.5}]}")]
        [TestCase("{\"hits\":1,\"documents\":[{\"id\":\"a\",\"tags\":[null]}]}")]
        [TestCase("{\"hits\":1,\"documents\":[{\"id\":\"a\",\"size\":-1}]}")]
        [TestCase("{\"hits\":1,\"documents\":[{\"id\":\"a\",\"score\":-1}]}")]
        [TestCase("{\"hits\":1,\"documents\":[{\"id\":\"a\",\"upload-date\":\"invalid\"}]}")]
        [TestCase("{\"hits\":0,\"facets\":[]}"), TestCase("{\"hits\":0} {}")]
        [TestCase("{\"hits\":9223372036854775808}"), TestCase("{\"hits\":1,\"documents\":[{\"id\":\"a\",\"size\":9223372036854775808}]}")]
        [TestCase("{\"hits\":1,\"documents\":[{\"id\":\" \"}]}"), TestCase("{\"hits\":1,\"documents\":[{\"id\":null}]}")]
        [TestCase("{\"hits\":1,\"documents\":[{\"id\":\"a\",\"score\":\"2.5\"}]}"), TestCase("{\"hits\":1,\"documents\":[{\"id\":\"a\",\"score\":NaN}]}")]
        [TestCase("{\"hits\":1,\"documents\":[{\"id\":\"a\",\"score\":Infinity}]}"), TestCase("{\"hits\":1,\"documents\":[{\"id\":\"a\",\"tags\":{}}]}")]
        [TestCase("{\"hits\":2,\"documents\":[{\"id\":\"a\"},{\"id\":\"a\"}]}")]
        [TestCase("{\"hits\":2,\"documents\":[{\"id\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"},{\"id\":\"AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE\"}]}")]
        public void MalformedTrashSearchDoesNotBecomeSuccessfulEmptyResults(string json)
        {
            var state = new State { Json = json };
            var failure = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).SearchAsync("context", new TrashSearchRequest()); }));
            Assert.That(failure.InnerException, Is.Not.Null);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(0, 1), TestCase(1, 0), TestCase(1, 2)]
        public void TrashSearchRejectsPagesContradictingRowsOrTotal(int rows, long hits)
        {
            var state = new State { Json = "{\"hits\":" + hits + ",\"documents\":[{\"id\":\"a\"},{\"id\":\"b\"}]}" };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).SearchAsync("context", new TrashSearchRequest { Rows = rows }); }));
        }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void TrashSearchRetainsOriginalServerFailureWithoutReplay(int status)
        {
            var original = new InvalidOperationException("isolated failure");
            var state = new State { Status = (HttpStatusCode)status, Error = original };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await new Client(state).SearchAsync("context", new TrashSearchRequest()); }));
            Assert.That(error.InnerException, Is.SameAs(original));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task TrashSearchCancellationReachesNativeAuthorizationAndIo()
        {
            var state = new State { Pending = true };
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await new Client(state).SearchAsync("context", new TrashSearchRequest(), cancellation.Token); }));
                Assert.That(state.AuthorizationCalls, Is.Zero);
            }
            using (var cancellation = new CancellationTokenSource())
            {
                var task = new Client(state).SearchAsync("context", new TrashSearchRequest(), cancellation.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(cancellation.Token));
                Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await task; }));
            }
        }
    }
}
