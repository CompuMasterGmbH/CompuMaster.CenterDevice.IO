using CenterDevice.Rest.Clients;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class SharedAuthorizationTransportTests
    {
        [Test]
        public async Task ExternalAuthorizationClientsShareAdmissionAndCancelWhileQueued()
        {
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstHandler = new PendingHandler(entered);
            var secondHandler = new PendingHandler(new TaskCompletionSource<bool>());
            using (var first = CenterDeviceHttpTransport.CreateHttpClient(firstHandler))
            using (var second = CenterDeviceHttpTransport.CreateHttpClient(secondHandler))
            using (var firstCancel = new CancellationTokenSource())
            using (var secondCancel = new CancellationTokenSource())
            {
                var origin = "https://" + Guid.NewGuid().ToString("N") + ".invalid/";
                var owner = first.GetAsync(origin + "oauth/token", firstCancel.Token);
                await entered.Task;
                var queued = second.GetAsync(origin + "myaccount", secondCancel.Token);
                secondCancel.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await queued; }));
                Assert.That(secondHandler.Calls, Is.Zero);
                firstCancel.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await owner; }));
            }
        }

        private sealed class PendingHandler : HttpMessageHandler
        {
            private readonly TaskCompletionSource<bool> entered;
            internal int Calls;
            internal PendingHandler(TaskCompletionSource<bool> entered) { this.entered = entered; }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                entered.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }
    }
}
