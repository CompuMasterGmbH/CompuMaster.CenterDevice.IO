using CenterDevice.IO;
using CenterDevice.Rest;
using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.Folders;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class SynchronousListingTests
    {
        [TestCase(false), TestCase(true)]
        public async Task SynchronousWaiterSharesThePendingNativeListingAndPublishedParentReferences(bool files)
        {
            var io = new FakeIo { Block = true };
            var directory = files ? new DirectoryInfo(io, io.RootDirectory, new Collection { Id = "collection", Name = "collection" }) : io.RootDirectory;
            Task owner = files ? (Task)directory.GetFilesAsync() : directory.GetDirectoriesAsync();
            await io.Entered.Task;
            var waiterEntered = Signal();
            var waiter = Task.Run(() => { waiterEntered.TrySetResult(true); return files ? (object)directory.GetFiles() : directory.GetDirectories(); });
            try
            {
                await waiterEntered.Task;
                await Task.Delay(30);
                Assert.That(waiter.IsCompleted, Is.False);
            }
            finally { io.Release.TrySetResult(true); }
            await owner;
            var value = await waiter;
            Assert.That(io.Calls, Is.EqualTo(1));
            if (files)
            {
                Assert.That(value, Is.SameAs(await directory.GetFilesAsync()));
                Assert.That(((FileInfo[])value).Single().ParentDirectory, Is.SameAs(directory));
            }
            else
            {
                Assert.That(value, Is.SameAs(await directory.GetDirectoriesAsync()));
                Assert.That(((DirectoryInfo[])value).Single().ParentDirectory, Is.SameAs(directory));
            }
        }

        [TestCase(false), TestCase(true)]
        public async Task ResetDuringSynchronousListingCannotPublishTheOldCache(bool files)
        {
            var io = new FakeIo { Block = true };
            var directory = files ? new DirectoryInfo(io, io.RootDirectory, new Collection { Id = "collection", Name = "collection" }) : io.RootDirectory;
            var owner = Task.Run(() => files ? (object)directory.GetFiles() : directory.GetDirectories());
            var entered = await Task.WhenAny(io.Entered.Task, owner);
            try
            {
                Assert.That(entered, Is.SameAs(io.Entered.Task));
                if (files) directory.ResetFilesCache(); else directory.ResetDirectoriesCache();
            }
            finally { io.Release.TrySetResult(true); }
            await owner;
            if (files) directory.GetFiles(); else directory.GetDirectories();
            Assert.That(io.Calls, Is.EqualTo(2));
        }

        [TestCase(false), TestCase(true)]
        public async Task ParallelSynchronousListingsReuseOneSuccessfulSnapshot(bool files)
        {
            var io = new FakeIo { Block = true };
            var directory = files ? new DirectoryInfo(io, io.RootDirectory, new Collection { Id = "collection", Name = "collection" }) : io.RootDirectory;
            var jobs = Enumerable.Range(0, 12).Select(_ => Task.Run(() => files ? (object)directory.GetFiles() : directory.GetDirectories())).ToArray();
            await Task.WhenAny(io.Entered.Task, Task.WhenAll(jobs));
            io.Release.TrySetResult(true);
            var results = await Task.WhenAll(jobs);
            Assert.That(results.All(result => ReferenceEquals(result, results[0])), Is.True);
            Assert.That(io.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task FailedSynchronousLookupPreservesItsCauseAndDoesNotPoisonNativeCache()
        {
            var original = new InvalidOperationException("isolated listing failure");
            var io = new FakeIo { Failure = original };
            Assert.That(Assert.Throws<InvalidOperationException>((Action)(() => { io.RootDirectory.GetDirectories(); })), Is.SameAs(original));
            io.Failure = null;
            Assert.That((await io.RootDirectory.GetDirectoriesAsync()).Length, Is.EqualTo(1));
            Assert.That(io.Calls, Is.EqualTo(2));
        }

        [Test]
        public async Task ConcurrentRootAccessPublishesOneRootAndResetProducesOneFreshRoot()
        {
            var io = new FakeIo();
            DirectoryInfo old = null;
            for (var iteration = 0; iteration < 20; iteration++)
            {
                io.ResetDirectoryCache();
                var start = Signal();
                var jobs = Enumerable.Range(0, 64).Select(_ => Task.Run(async () => { await start.Task; return io.RootDirectory; })).ToArray();
                start.TrySetResult(true);
                var roots = await Task.WhenAll(jobs);
                Assert.That(roots.All(root => ReferenceEquals(root, roots[0])), Is.True);
                Assert.That(roots[0], Is.Not.SameAs(old));
                old = roots[0];
            }
            Assert.That(io.Calls, Is.Zero);
            Assert.That(io.RootDirectory.GetFiles(), Is.Empty);
            Assert.That(io.Calls, Is.Zero);
        }

        [Test]
        public async Task ActualLoopbackConcurrentSyncListingsIssueOnlyOneHttpRequest()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var received = Signal();
            var release = Signal();
            var secondStarted = Signal();
            var calls = 0;
            var connections = new List<Task>();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                var server = Task.Run(async () =>
                {
                    while (!cancellation.IsCancellationRequested)
                    {
                        var socket = await listener.AcceptTcpClientAsync();
                        connections.Add(Task.Run(async () =>
                        {
                            using (socket)
                            using (var stream = socket.GetStream())
                            {
                                var header = new List<byte>();
                                var one = new byte[1];
                                while (header.Count < 32768)
                                {
                                    if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new System.IO.EndOfStreamException();
                                    header.Add(one[0]);
                                    if (header.Count >= 4 && Encoding.ASCII.GetString(header.Skip(header.Count - 4).ToArray()) == "\r\n\r\n") break;
                                }
                                Interlocked.Increment(ref calls);
                                received.TrySetResult(true);
                                await release.Task;
                                const string json = "{\"collections\":[{\"id\":\"collection\",\"name\":\"collection\"}]}";
                                var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + json.Length + "\r\nConnection: close\r\n\r\n" + json);
                                await stream.WriteAsync(bytes, 0, bytes.Length, cancellation.Token);
                            }
                        }));
                    }
                });
                try
                {
                    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    var io = new RealIo(new LoopbackApi("http://127.0.0.1:" + port + "/"));
                    var root = io.RootDirectory;
                    var first = Task.Run(() => root.GetDirectories());
                    var started = await Task.WhenAny(received.Task, first, Task.Delay(5000));
                    Assert.That(started, Is.SameAs(received.Task));
                    var second = Task.Run(() => { secondStarted.TrySetResult(true); return root.GetDirectories(); });
                    await secondStarted.Task;
                    await Task.Delay(100);
                    release.TrySetResult(true);
                    var completed = Task.WhenAll(first, second);
                    Assert.That(await Task.WhenAny(completed, Task.Delay(5000)), Is.SameAs(completed));
                    var values = await completed;
                    Assert.That(calls, Is.EqualTo(1));
                    Assert.That(values[0], Is.SameAs(values[1]));
                    Assert.That(values[0][0].ParentDirectory, Is.SameAs(root));
                }
                finally
                {
                    release.TrySetResult(true);
                    cancellation.Cancel();
                    listener.Stop();
                    try { await server; }
                    catch (SocketException) { }
                    catch (ObjectDisposedException) { }
                    await Task.WhenAll(connections);
                }
            }
        }

        private sealed class RealIo : IOClientBase { internal RealIo(CenterDeviceClientBase api) : base(api, "fixture") { } }
        private sealed class LoopbackApi : CenterDeviceClientBase
        {
            internal LoopbackApi(string address) : base(new LoopbackAuth(), new LoopbackConfig(address), null, "") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }
        private sealed class LoopbackConfig : IRestClientConfiguration
        {
            internal LoopbackConfig(string address) { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-cache-test";
        }
        private sealed class LoopbackAuth : IAsyncOAuthInfoProvider
        {
            public OAuthInfo GetOAuthInfo(string userId) => new OAuthInfo { UserId = userId, access_token = "isolated-token" };
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) => Task.FromResult(GetOAuthInfo(userId));
        }

        private static TaskCompletionSource<bool> Signal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private sealed class FakeIo : IOClientBase
        {
            internal FakeIo() : base(null, "fixture") { }
            internal bool Block;
            internal Exception Failure;
            internal int Calls;
            internal readonly TaskCompletionSource<bool> Entered = Signal(), Release = Signal();
            private async Task BeforeLookup()
            {
                Interlocked.Increment(ref Calls);
                Entered.TrySetResult(true);
                if (Block) await Release.Task;
                if (Failure != null) throw Failure;
            }
            protected internal override async Task<List<Collection>> LookupCollectionsAsync(CancellationToken token)
            {
                await BeforeLookup();
                return new List<Collection> { new Collection { Id = "collection", Name = "collection" } };
            }
            protected internal override async Task<List<DocumentFullMetadata>> LookupChildDocumentsAsync(string collection, string parent, CancellationToken token)
            {
                await BeforeLookup();
                return new List<DocumentFullMetadata> { new DocumentFullMetadata { Id = "document", Filename = "fixture.txt" } };
            }
            protected internal override async Task<List<Folder>> LookupChildFoldersAsync(string collection, string parent, CancellationToken token)
            {
                await BeforeLookup();
                return new List<Folder>();
            }
        }
    }
}
