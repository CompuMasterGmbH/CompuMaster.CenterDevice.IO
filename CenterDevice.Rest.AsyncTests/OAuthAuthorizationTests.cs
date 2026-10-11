using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using NUnit.Framework;
using RestSharp;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class OAuthAuthorizationTests
    {
        [TestCase(null), TestCase("tenant+&= id")]
        public void AuthorizationUriEncodesClientRedirectStateAndOptionalTenant(string tenant)
        {
            var config = new Configuration { ClientId = "client+&=ü", BaseAddress = "https://auth.invalid/distributor/", RedirectUri = "https://app.invalid/callback/?fixed=a%2Bb" };
            using (var client = new TestClient(config, new Handler()))
            {
                var uri = client.CreateAuthorizationUri("state+&= ü", tenant);
                Assert.That(uri.GetLeftPart(UriPartial.Path), Is.EqualTo("https://auth.invalid/distributor/authorize"));
                Assert.That(uri.Query, Does.Contain("client_id=" + WebUtility.UrlEncode(config.ClientId)));
                Assert.That(uri.Query, Does.Contain("redirect_uri=" + WebUtility.UrlEncode(config.RedirectUri)));
                Assert.That(uri.Query, Does.Contain("state=" + WebUtility.UrlEncode("state+&= ü")));
                Assert.That(uri.Query.Contains("tenant="), Is.EqualTo(tenant != null));
                if (tenant != null) Assert.That(uri.Query, Does.Contain("tenant=" + WebUtility.UrlEncode(tenant)));
                Assert.That(uri.Query, Does.Contain("response_type=code"));
                Assert.That(uri.AbsoluteUri, Does.Not.Contain(config.ClientSecret));
            }
        }

        [TestCase(false), TestCase(true)]
        public async Task CallbackExchangeUsesExactRedirectUtf8BasicAndFormEncodingWithoutInferringIdentity(bool synchronous)
        {
            var config = new Configuration { ClientId = "client-ü", ClientSecret = "secret-ä", RedirectUri = "https://app.invalid/callback/?fixed=a%2Bb" };
            var handler = new Handler();
            using (var client = new TestClient(config, handler))
            {
                var callback = new Uri(config.RedirectUri + "&state=state%2Bvalue&code=code%2B%26%3D");
                var result = synchronous ? client.ExchangeAuthorizationCode(callback, "state+value")
                    : await client.ExchangeAuthorizationCodeAsync(callback, "state+value");
                Assert.That(handler.Calls, Is.EqualTo(1));
                Assert.That(handler.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(handler.Uri.AbsoluteUri, Is.EqualTo("https://auth.invalid/token"));
                Assert.That(handler.Authorization, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(config.ClientId + ":" + config.ClientSecret))));
                Assert.That(handler.ContentType, Does.StartWith("application/x-www-form-urlencoded"));
                Assert.That(handler.Body, Is.EqualTo("grant_type=authorization_code&redirect_uri=" + WebUtility.UrlEncode(config.RedirectUri) + "&code=code%2B%26%3D"));
                Assert.That(result.Data.access_token, Is.EqualTo("returned-access"));
                Assert.That(result.Data.refresh_token, Is.EqualTo("returned-refresh"));
                Assert.That(result.Data.expires_in, Is.EqualTo(1234));
                Assert.That(result.Data.UserId, Is.Null);
                Assert.That(result.Data.TenantId, Is.Null);
                Assert.That(result.Data.Email, Is.Null);
            }
        }

        [TestCase("https://auth.invalid/distributor"), TestCase("https://auth.invalid/distributor/")]
        public async Task AuthorizationAndTokenEndpointsUseTheSameConfiguredDirectory(string address)
        {
            var handler = new Handler();
            using (var client = new TestClient(new Configuration { BaseAddress = address }, handler))
            {
                Assert.That(client.CreateAuthorizationUri("state").AbsolutePath, Is.EqualTo("/distributor/authorize"));
                await client.ExchangeAuthorizationCodeAsync(new Uri("https://app.invalid/callback?state=state&code=code"), "state");
                Assert.That(handler.Uri.AbsolutePath, Is.EqualTo("/distributor/token"));
            }
        }

        [TestCase("https://other.invalid/callback?state=state&code=code")]
        [TestCase("http://app.invalid/callback?state=state&code=code")]
        [TestCase("https://app.invalid:444/callback?state=state&code=code")]
        [TestCase("https://app.invalid/callback/?state=state&code=code")]
        [TestCase("https://app.invalid/callback?state=other&code=code")]
        [TestCase("https://app.invalid/callback?code=code")]
        [TestCase("https://app.invalid/callback?state=state&code=")]
        [TestCase("https://app.invalid/callback?state=state")]
        [TestCase("https://app.invalid/callback?state=state&state=state&code=code")]
        [TestCase("https://app.invalid/callback?state=state&code=one&code=two")]
        [TestCase("https://app.invalid/callback?state=state&code=one&%63ode=two")]
        [TestCase("https://app.invalid/callback?state=state&code=code#error")]
        [TestCase("https://app.invalid/callback?state=state&error=access_denied")]
        [TestCase("https://app.invalid/callback?state=state&code=code&error=access_denied")]
        public void InvalidOrDeniedCallbacksNeverSendTokens(string callback)
        {
            var handler = new Handler();
            using (var client = new TestClient(new Configuration(), handler))
                Assert.Throws<AuthorizationException>((Action)(() => client.ExchangeAuthorizationCode(new Uri(callback), "state")));
            Assert.That(handler.Calls, Is.Zero);
        }

        [Test]
        public void CallbackMustRetainRegisteredQueryParameters()
        {
            var handler = new Handler();
            using (var client = new TestClient(new Configuration { RedirectUri = "https://app.invalid/callback?fixed=expected" }, handler))
                Assert.Throws<AuthorizationException>((Action)(() => client.ExchangeAuthorizationCode(new Uri("https://app.invalid/callback?fixed=other&state=state&code=code"), "state")));
            Assert.That(handler.Calls, Is.Zero);
        }

        [TestCase(null), TestCase(""), TestCase(" ")]
        public void StateCannotBeEmpty(string state)
        {
            var handler = new Handler();
            using (var client = new TestClient(new Configuration(), handler))
            {
                Assert.Throws<ArgumentException>((Action)(() => client.CreateAuthorizationUri(state)));
                Assert.Throws<ArgumentException>((Action)(() => client.ExchangeAuthorizationCode(new Uri("https://app.invalid/callback?state=state&code=code"), state)));
            }
            Assert.That(handler.Calls, Is.Zero);
        }

        [TestCase("http://auth.invalid/", "https://app.invalid/callback")]
        [TestCase("https://auth.invalid/?query=bad", "https://app.invalid/callback")]
        [TestCase("https://auth.invalid/", "https://app.invalid/callback#fragment")]
        [TestCase("https://auth.invalid/", "https://user@app.invalid/callback")]
        [TestCase("https://auth.invalid/", "https://app.invalid/callback?state=fixed")]
        public void InvalidConfigurationFailsBeforeAuthorization(string address, string redirect)
        {
            using (var client = new TestClient(new Configuration { BaseAddress = address, RedirectUri = redirect }, new Handler()))
                Assert.Throws<ArgumentException>((Action)(() => client.CreateAuthorizationUri("state")));
        }

        [TestCase(false, false), TestCase(true, false), TestCase(false, true)]
        public async Task RefreshOptionsAreExplicitAndLeaveOriginalIdentityUntouched(bool force, bool prolong)
        {
            var info = new OAuthInfo { refresh_token = "refresh+&=", UserId = "selected-user", TenantId = "selected-tenant", access_token = "existing" };
            var handler = new Handler();
            using (var client = new TestClient(new Configuration(), handler))
                await client.RefreshTokenAsync(info, force, prolong);
            Assert.That(handler.Body, Is.EqualTo("grant_type=refresh_token&refresh_token=refresh%2B%26%3D" + (force ? "&force=true" : "") + (prolong ? "&prolong=true" : "")));
            Assert.That(info.UserId, Is.EqualTo("selected-user"));
            Assert.That(info.TenantId, Is.EqualTo("selected-tenant"));
            Assert.That(info.access_token, Is.EqualTo("existing"));
        }

        [Test]
        public async Task ExistingInterfaceAndAsyncRefreshRetainTheirAbsentOptionBody()
        {
            var handler = new Handler();
            var info = new OAuthInfo { refresh_token = "legacy+" };
            using (var client = new TestClient(new Configuration(), handler))
            {
                IOAuthRestClient legacy = client;
                legacy.RefreshToken(info);
                Assert.That(handler.Body, Is.EqualTo("grant_type=refresh_token&refresh_token=legacy%2B"));
                await client.RefreshTokenAsync(info);
                Assert.That(handler.Body, Is.EqualTo("grant_type=refresh_token&refresh_token=legacy%2B"));
                client.RefreshToken(info, true, false);
                Assert.That(handler.Body, Does.EndWith("&force=true"));
            }
        }

        [Test]
        public void MutuallyExclusiveRefreshOptionsFailBeforeHttp()
        {
            var handler = new Handler();
            using (var client = new TestClient(new Configuration(), handler))
                Assert.Throws<ArgumentException>((Action)(() => client.RefreshToken(new OAuthInfo { refresh_token = "refresh" }, true, true)));
            Assert.That(handler.Calls, Is.Zero);
        }

        [Test]
        public void PreCancellationNeverStartsAnExchangeOrRefresh()
        {
            var handler = new Handler();
            using (var client = new TestClient(new Configuration(), handler))
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Assert.Throws<OperationCanceledException>((Action)(() => client.ExchangeAuthorizationCodeAsync(new Uri("https://app.invalid/callback?state=state&code=code"), "state", cancel.Token)));
                Assert.Throws<OperationCanceledException>((Action)(() => client.RefreshTokenAsync(new OAuthInfo { refresh_token = "refresh" }, true, false, cancel.Token)));
            }
            Assert.That(handler.Calls, Is.Zero);
        }

        [Test]
        public async Task ActiveCancellationAbortsTokenExchangeWithoutReplay()
        {
            var handler = new Handler { Pending = true };
            using (var client = new TestClient(new Configuration(), handler))
            using (var cancel = new CancellationTokenSource())
            {
                var task = client.ExchangeAuthorizationCodeAsync(new Uri("https://app.invalid/callback?state=state&code=code"), "state", cancel.Token);
                await handler.Entered.Task;
                cancel.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await task; }));
            }
            Assert.That(handler.Calls, Is.EqualTo(1));
        }

        [TestCase(400), TestCase(429), TestCase(503)]
        public async Task TokenFailuresRetainResponseDiagnosticsAndAreNeverReplayed(int status)
        {
            var handler = new Handler { Status = (HttpStatusCode)status };
            using (var client = new TestClient(new Configuration { BaseAddress = "https://" + Guid.NewGuid().ToString("N") + ".invalid/" }, handler, true))
            {
                var result = await client.ExchangeAuthorizationCodeAsync(new Uri("https://app.invalid/callback?state=state&code=code"), "state");
                Assert.That(result.StatusCode, Is.EqualTo((HttpStatusCode)status));
                Assert.That(result.Content, Does.Contain("invalid_grant"));
            }
            Assert.That(handler.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task AuthorizationExchangesShareAdmissionAndCancelWhileQueued()
        {
            var config = new Configuration { BaseAddress = "https://" + Guid.NewGuid().ToString("N") + ".invalid/" };
            var firstHandler = new Handler { Pending = true };
            var secondHandler = new Handler();
            var callback = new Uri("https://app.invalid/callback?state=state&code=code");
            using (var first = new TestClient(config, firstHandler, true))
            using (var second = new TestClient(config, secondHandler, true))
            using (var firstCancel = new CancellationTokenSource())
            using (var secondCancel = new CancellationTokenSource())
            {
                var owner = first.ExchangeAuthorizationCodeAsync(callback, "state", firstCancel.Token);
                await firstHandler.Entered.Task;
                var queued = second.ExchangeAuthorizationCodeAsync(callback, "state", secondCancel.Token);
                secondCancel.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await queued; }));
                Assert.That(secondHandler.Calls, Is.Zero);
                firstCancel.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await owner; }));
                Assert.That(firstHandler.Calls, Is.EqualTo(1));
            }
        }

        private sealed class Configuration : IOAuthClientConfiguration
        {
            public string BaseAddress { get; set; } = "https://auth.invalid/";
            public string UserAgent => "isolated-oauth-tests";
            public string ClientId { get; set; } = "test-client";
            public string ClientSecret { get; set; } = "test-secret";
            public string RedirectUri { get; set; } = "https://app.invalid/callback";
        }

        private sealed class TestClient : OAuthRestClient, IDisposable
        {
            private readonly HttpClient transport;
            internal TestClient(Configuration config, Handler handler, bool sharedTransport = false) : base(config)
            {
                Client.Dispose();
                if (sharedTransport)
                {
                    transport = CenterDeviceHttpTransport.CreateHttpClient(handler);
                    Client = new RestClient(transport, new RestClientOptions(config.BaseAddress));
                }
                else Client = new RestClient(new RestClientOptions(config.BaseAddress) { ConfigureMessageHandler = _ => handler });
            }
            public void Dispose() { Client.Dispose(); transport?.Dispose(); }
        }

        private sealed class Handler : HttpMessageHandler
        {
            internal int Calls;
            internal string Body, Authorization, ContentType;
            internal Uri Uri;
            internal HttpMethod Method;
            internal bool Pending;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                Uri = request.RequestUri;
                Method = request.Method;
                Authorization = request.Headers.Authorization.ToString();
                ContentType = request.Content.Headers.ContentType.ToString();
                Body = await request.Content.ReadAsStringAsync();
                Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(Status) { Content = new StringContent(Status == HttpStatusCode.OK
                    ? "{\"token_type\":\"bearer\",\"access_token\":\"returned-access\",\"refresh_token\":\"returned-refresh\",\"expires_in\":1234}"
                    : "{\"error\":\"invalid_grant\"}", Encoding.UTF8, "application/json") };
            }
        }
    }
}
