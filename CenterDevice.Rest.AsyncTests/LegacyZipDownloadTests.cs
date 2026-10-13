using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS0618 // Explicit manufacturer-deprecated endpoint acceptance.
namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class LegacyZipDownloadTests
    {
        [Test, Combinatorial]
        public async Task BothRepresentationsUseBodylessGetAndLazyOwnedStreams([Values(false,true)] bool pdf,[Values(false,true)] bool sync,[Values(false,true)] bool trailing)
        {
            var state = new State { Address = "https://unused.invalid/bridge" + (trailing ? "/" : "") }; var client = new Client(state);
            using (var stream = sync ? client.DownloadDocumentsAsZipLegacy("context", new[] { "original/ö ?", "other", "other" }, pdf ? DocumentDownloadRepresentation.Pdf : DocumentDownloadRepresentation.Original) : await client.DownloadDocumentsAsZipLegacyAsync("context", new[] { "original/ö ?", "other", "other" }, pdf ? DocumentDownloadRepresentation.Pdf : DocumentDownloadRepresentation.Original))
            {
                Assert.That(state.Method, Is.EqualTo(HttpMethod.Get)); Assert.That(state.BodyPresent, Is.False); Assert.That(state.Accept, Is.EqualTo(pdf ? "application/pdf" : "*/*")); Assert.That(state.Uri.AbsolutePath, Is.EqualTo("/bridge/v2/documents")); Assert.That(Uri.UnescapeDataString(state.Uri.Query), Is.EqualTo("?ids=original/ö ?,other,other")); Assert.That(state.Uri.Fragment, Is.Empty); Assert.That(state.Authorization, Is.EqualTo("Bearer fixture")); Assert.That(state.Context, Is.EqualTo("context"));
                Assert.That(state.Content.Buffered, Is.False); Assert.That(state.Content.Source.Reads, Is.Zero); Assert.That(await stream.ReadAsync(new byte[3],0,3), Is.EqualTo(3)); Assert.That(state.Content.Source.Reads, Is.EqualTo(1));
            }
            Assert.That(state.Content.Disposed, Is.True); Assert.That(state.Content.Source.Disposed, Is.True); Assert.That(state.Calls, Is.EqualTo(1));
        }
        [TestCase(null),TestCase(""),TestCase(" "),TestCase("one,two")]
        public void MissingOrAmbiguousIdentitiesDoNotAuthorize(string id)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).DownloadDocumentsAsZipLegacyAsync("context",new[] {id}))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(false),TestCase(true)]
        public void NullOrEmptySelectionDoesNotAuthorize(bool empty)
        { var state=new State(); Assert.ThrowsAsync(empty ? typeof(ArgumentException) : typeof(ArgumentNullException),(Func<Task>)(async () => await new Client(state).DownloadDocumentsAsZipLegacyAsync("context",empty ? new string[0] : null))); Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public void UnsupportedRepresentationIsNotAnImplicitOriginalDownload()
        { var state=new State(); Assert.ThrowsAsync<ArgumentOutOfRangeException>((Func<Task>)(async () => await new Client(state).DownloadDocumentsAsZipLegacyAsync("context",new[] {"id"},(DocumentDownloadRepresentation)999))); Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public async Task OriginalSelectionIsCopiedExactlyOnceBeforeAuthorization()
        { var ids=new List<string> {"original","original"}; int enumerations=0; IEnumerable<string> Read() {enumerations++;foreach(var id in ids)yield return id;} var state=new State {OnAuthorize=()=>ids.Add("later")}; using(await new Client(state).DownloadDocumentsAsZipLegacyAsync("context",Read())) {} Assert.That(Uri.UnescapeDataString(state.Uri.Query),Is.EqualTo("?ids=original,original")); Assert.That(enumerations,Is.EqualTo(1)); }
        [Test]
        public void PreCanceledCallDoesNotEnumerateOrAuthorize()
        {var state=new State();using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async()=>await new Client(state).DownloadDocumentsAsZipLegacyAsync("context",ThrowIfEnumerated(),cancellationToken:cancellation.Token)));}Assert.That(state.Authorizations,Is.Zero);}
        [Test]
        public async Task ActiveIoReceivesCancellationWithoutReplay()
        {var state=new State {Pending=true};using(var cancellation=new CancellationTokenSource()){var pending=new Client(state).DownloadDocumentsAsZipLegacyAsync("context",new[] {"id"},cancellationToken:cancellation.Token);await state.Entered.Task;cancellation.Cancel();Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async()=>await pending));}Assert.That(state.Calls,Is.EqualTo(1));state.Content.Dispose();}
        [TestCase(403),TestCase(404),TestCase(503)]
        public void HttpFailureDisposesContentWithoutReplay(int status)
        {var state=new State {Status=(HttpStatusCode)status};Assert.CatchAsync<RestClientException>((Func<Task>)(async()=>await new Client(state).DownloadDocumentsAsZipLegacyAsync("context",new[] {"id"})));Assert.That(state.Content.Disposed,Is.True);Assert.That(state.Calls,Is.EqualTo(1));}
        [TestCase(false,200),TestCase(true,200),TestCase(false,503)]
        public async Task ActualPublicFactoryUsesEncodedQueryRepresentationHeadersAndNativeOwnedResponse(bool pdf,int status)
        {
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using(var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string captured=null;var server=Task.Run(async()=>
                {
                    using(var socket=await listener.AcceptTcpClientAsync())using(var stream=socket.GetStream())
                    {
                        var bytes=new List<byte>();var one=new byte[1];while(bytes.Count<32768){if(await stream.ReadAsync(one,0,1,cancellation.Token)==0)throw new EndOfStreamException();bytes.Add(one[0]);if(bytes.Count>=4&&Encoding.ASCII.GetString(bytes.Skip(bytes.Count-4).ToArray())=="\r\n\r\n")break;}captured=Encoding.ASCII.GetString(bytes.ToArray());
                        var payload=Encoding.UTF8.GetBytes(status==200?"ZIP":"{\"code\":300,\"message\":\"isolated failure\"}");var header=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+(status==200?" OK":" Service Unavailable")+"\r\nContent-Type: "+(status==200?"application/zip":"application/json")+"\r\nContent-Length: "+payload.Length+"\r\nConnection: close\r\n\r\n");await stream.WriteAsync(header,0,header.Length,cancellation.Token);await stream.WriteAsync(payload,0,payload.Length,cancellation.Token);
                    }
                });
                var state=new State {Address="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/bridge/"};
                try
                {
                    var client=new Factory(state).Documents;client.DisableOfflineModeSimulation();Func<Task<Stream>> open=()=>client.DownloadDocumentsAsZipLegacyAsync("context",new[] {"one","two"},pdf?DocumentDownloadRepresentation.Pdf:DocumentDownloadRepresentation.Original,cancellation.Token);
                    if(status==200){using(var download=await open()){var buffer=new byte[3];Assert.That(await download.ReadAsync(buffer,0,3),Is.EqualTo(3));Assert.That(Encoding.ASCII.GetString(buffer),Is.EqualTo("ZIP"));}}else Assert.CatchAsync<RestClientException>((Func<Task>)(async()=>await open()));await server;
                    var first=captured.Split(new[] {"\r\n"},StringSplitOptions.None)[0];Assert.That(first,Does.StartWith("GET /bridge/v2/documents?ids="));Assert.That(Uri.UnescapeDataString(first.Split(' ')[1]),Is.EqualTo("/bridge/v2/documents?ids=one,two"));Assert.That(captured,Does.Contain("Accept: "+(pdf?"application/pdf":"*/*")));Assert.That(listener.Pending(),Is.False);
                }
                finally{state.Content.Dispose();cancellation.Cancel();listener.Stop();try{await server;}catch(SocketException){}catch(ObjectDisposedException){}catch(OperationCanceledException){}}
            }
        }
        private sealed class Factory : CenterDeviceClientBase {internal Factory(State state):base(state,new Configuration(state),null,"v2/"){}protected override string UploadLinkBaseUrl=>"https://unused.invalid/";}
        private static IEnumerable<string> ThrowIfEnumerated(){foreach(var value in new string[0])yield return value;throw new AssertionException("Canceled selection enumerated.");}
        private sealed class Configuration : IRestClientConfiguration {private readonly State state;internal Configuration(State state){this.state=state;}public string BaseAddress=>state.Address;public string UserAgent=>"isolated-legacy-zip";}
        private sealed class Client : DocumentsRestClient {private readonly State state;internal Client(State state):base(state,new Configuration(state),null,null,"v2/"){this.state=state;}protected override Task<HttpResponseMessage> SendResourceDownloadRequestAsync(HttpRequestMessage request,CancellationToken token)=>state.Send(request,token);}
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal string Address="https://unused.invalid/bridge/",Accept,Authorization,Context;internal HttpMethod Method;internal Uri Uri;internal bool BodyPresent,Pending;internal int Calls,Authorizations;internal Action OnAuthorize;internal HttpStatusCode Status=HttpStatusCode.OK;internal readonly LazyContent Content=new LazyContent();internal readonly TaskCompletionSource<bool> Entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId)=>throw new AssertionException("Native async auth required.");public Task<OAuthInfo> GetOAuthInfoAsync(string userId,CancellationToken token=default(CancellationToken)){Authorizations++;Context=userId;OnAuthorize?.Invoke();return Task.FromResult(new OAuthInfo {UserId=userId,access_token="fixture"});}
            internal async Task<HttpResponseMessage> Send(HttpRequestMessage request,CancellationToken token){Calls++;Uri=request.RequestUri;Method=request.Method;BodyPresent=request.Content!=null;Accept=request.Headers.Accept.ToString();Authorization=request.Headers.Authorization.ToString();Entered.TrySetResult(true);if(Pending)await Task.Delay(Timeout.Infinite,token);return new HttpResponseMessage(Status){Content=Content};}
        }
        private sealed class LazyContent : HttpContent
        {internal bool Buffered,Disposed;internal readonly TrackingStream Source=new TrackingStream();internal LazyContent(){Headers.ContentType=new MediaTypeHeaderValue("application/zip");}protected override Task SerializeToStreamAsync(Stream stream,TransportContext context){Buffered=true;return Task.FromResult(0);}protected override bool TryComputeLength(out long length){length=3;return true;}protected override Task<Stream> CreateContentReadStreamAsync()=>Task.FromResult<Stream>(Source);protected override void Dispose(bool disposing){if(disposing){Disposed=true;Source.Dispose();}base.Dispose(disposing);}}
        private sealed class TrackingStream : MemoryStream {internal int Reads;internal bool Disposed;internal TrackingStream():base(Encoding.ASCII.GetBytes("ZIP")){}public override int Read(byte[] buffer,int offset,int count){Reads++;return base.Read(buffer,offset,count);}protected override void Dispose(bool disposing){Disposed=true;base.Dispose(disposing);}}
    }
}
#pragma warning restore CS0618
