using CenterDevice.BrowserDemo;
using CenterDevice.Rest.Clients.OAuth;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class BrowserSessionTokenTests
    {
        [TestCase("https://auth.centerdevice.de/weblogin", true)]
        [TestCase("https://app.centerdevice.de/", true)]
        [TestCase("https://api.centerdevice.de/v2/users/current", true)]
        [TestCase("https://api.centerdevice.de.evil.invalid/", false)]
        [TestCase("https://evilcenterdevice.de/", false)]
        [TestCase("https://www.centerdevice.de/", false)]
        [TestCase("http://auth.centerdevice.de/", false)]
        [TestCase("https://auth.centerdevice.de:444/", false)]
        [TestCase("https://user@auth.centerdevice.de/", false)]
        [TestCase("not a URL", false)]
        public void CaptureRequiresExactHttpsSessionOrigin(string address, bool accepted)
            => Assert.That(SessionTokens.IsSessionOrigin(address), Is.EqualTo(accepted));

        [TestCase("https://app.centerdevice.de/tenant/test/view/search", true)]
        [TestCase("https://auth.centerdevice.de/weblogin", false)]
        [TestCase("https://app.centerdevice.de/login", false)]
        [TestCase("http://app.centerdevice.de/tenant/test", false)]
        [TestCase("https://app.centerdevice.de.evil.invalid/tenant/test", false)]
        [TestCase("https://user@app.centerdevice.de/tenant/test", false)]
        public void WebApplicationNavigationIsSeparateFromApiTokenAcquisition(string address, bool accepted)
            => Assert.That(SessionTokens.IsWebApplicationPage(address), Is.EqualTo(accepted));

        [Test]
        public void ServerSessionCookiesNeverBecomeOAuthTokens()
        {
            var fields = new Dictionary<string, string> { ["JSESSIONID"] = "synthetic-session", ["SESSION"] = "synthetic-other-session" };
            Assert.That(SessionTokens.IsSessionCookieName("JSESSIONID"), Is.True);
            Assert.That(SessionTokens.IsSessionCookieName("SESSION"), Is.True);
            Assert.That(SessionTokens.IsSessionCookieName("access_token"), Is.False);
            Assert.That(SessionTokens.FromFields(fields), Is.Null);
        }

        [Test]
        public void CookieInspectionIncludesTenantScopedCookiesWithoutCopyingQueryOrFragment()
        {
            var addresses = SessionTokens.CookieInspectionAddresses("https://app.centerdevice.de/tenant/test/view/search?private=not-forwarded#fragment");
            Assert.That(addresses, Does.Contain("https://app.centerdevice.de/tenant/test/view/search"));
            Assert.That(addresses, Does.Contain(SessionTokens.LoginUrl));
            Assert.That(addresses, Does.Contain(SessionTokens.ApiUrl + "v2/user/current"));
            Assert.That(string.Join("\n", addresses), Does.Not.Contain("private=").And.Not.Contain("#fragment"));
        }

        [TestCase("https://identity-provider.invalid/tenant/test")]
        [TestCase("http://app.centerdevice.de/tenant/test")]
        [TestCase("https://user@app.centerdevice.de/tenant/test")]
        public void CookieInspectionExcludesUntrustedCurrentPages(string currentAddress)
        {
            Assert.That(SessionTokens.CookieInspectionAddresses(currentAddress), Does.Not.Contain(currentAddress));
        }

        [TestCase("https://api.centerdevice.de/v2/users/current", "Bearer synthetic-access", true)]
        [TestCase("https://api.centerdevice.de/v2/users/current", "bearer synthetic-access", true)]
        [TestCase("https://auth.centerdevice.de/token", "Bearer synthetic-access", false)]
        [TestCase("https://api.centerdevice.de.evil.invalid/v2/users", "Bearer synthetic-access", false)]
        [TestCase("https://api.centerdevice.de/v2/users", "Basic ignored", false)]
        [TestCase("https://api.centerdevice.de/v2/users", "Bearer ", false)]
        [TestCase("https://api.centerdevice.de/v2/users", "Bearer invalid token", false)]
        [TestCase("https://api.centerdevice.de/v2/users", "Bearer invalid\r\nheader", false)]
        public void OnlyApiBearerHeadersProduceAccessTokens(string address, string header, bool accepted)
        {
            var tokens = SessionTokens.FromBearer(address, header);
            Assert.That(tokens != null, Is.EqualTo(accepted));
            if (tokens != null)
            {
                Assert.That(tokens.refresh_token, Is.Null);
                Assert.That(tokens.UserId, Is.Null);
                Assert.That(tokens.TenantId, Is.Null);
            }
        }

        [TestCase("https://auth.centerdevice.de/token", true)]
        [TestCase("https://auth.centerdevice.de/weblogin", true)]
        [TestCase("https://api.centerdevice.de/v2/documents", false)]
        [TestCase("https://untrusted.invalid/token", false)]
        public void ResponseInspectionIsLimitedToLoginAndTokenPaths(string address, bool accepted)
            => Assert.That(SessionTokens.IsTokenResponse(address), Is.EqualTo(accepted));

        [TestCase("access_token", "refresh_token")]
        [TestCase("accessToken", "refreshToken")]
        [TestCase("access-token", "refresh-token")]
        public void ParsesNamedTokenFieldsWithoutInferringIdentity(string accessName, string refreshName)
        {
            var tokens = SessionTokens.FromJson("{\"" + accessName + "\":\"synthetic-access\",\"" + refreshName
                + "\":\"synthetic-refresh\",\"expires_in\":123,\"userId\":\"untrusted\",\"tenant-id\":\"untrusted\"}");
            Assert.That(tokens.access_token, Is.EqualTo("synthetic-access"));
            Assert.That(tokens.refresh_token, Is.EqualTo("synthetic-refresh"));
            Assert.That(tokens.expires_in, Is.EqualTo(123));
            Assert.That(tokens.UserId, Is.Null);
            Assert.That(tokens.TenantId, Is.Null);
        }

        [TestCase("{}")]
        [TestCase("{\"password\":\"not-a-token\",\"session_id\":\"not-a-token\"}")]
        [TestCase("{\"refresh_token\":\"refresh-only\"}")]
        [TestCase("{\"access_token\":null}")]
        [TestCase("{\"access_token\":{\"value\":\"wrong-type\"}}")]
        [TestCase("{\"access_token\":\"a\",\"token_type\":\"Basic\"}")]
        [TestCase("{\"access_token\":\"a\",\"access_token\":\"b\"}")]
        [TestCase("{\"access_token\":\"a\",\"accessToken\":\"b\"}")]
        [TestCase("{\"access_token\":\"invalid token\"}")]
        [TestCase("{\"access_token\":\"a\"} {\"access_token\":\"b\"}")]
        [TestCase("<html>login</html>")]
        public void RejectsMalformedAmbiguousAndNonTokenData(string json)
            => Assert.That(SessionTokens.FromJson(json), Is.Null);

        [Test]
        public void NestedStorageRetainsPairsFromOneObjectOnly()
        {
            var tokens = SessionTokens.FromJson("{\"local\":{\"oauth\":\"{\\\"accessToken\\\":\\\"a\\\"}\"},\"session\":{\"refresh_token\":\"unrelated\"}}");
            Assert.That(tokens.access_token, Is.EqualTo("a"));
            Assert.That(tokens.refresh_token, Is.Null);
            Assert.That(SessionTokens.FromJson(new string('x', SessionTokens.MaximumJsonLength + 1)), Is.Null);
        }

        [Test]
        public void CookieFieldsIgnoreSessionIdsAndRejectAmbiguousAliases()
        {
            var fields = new Dictionary<string, string> { ["JSESSIONID"] = "session", ["access_token"] = "a", ["refreshToken"] = "r" };
            var tokens = SessionTokens.FromFields(fields);
            Assert.That(tokens.access_token, Is.EqualTo("a"));
            Assert.That(tokens.refresh_token, Is.EqualTo("r"));
            fields.Add("accessToken", "different");
            Assert.That(SessionTokens.FromFields(fields), Is.Null);
        }

        [Test]
        public void RotationDropsOldRefreshAndIgnoresStaleStorage()
        {
            var session = new CapturedSession();
            session.Observe(new OAuthInfo { access_token = "old", refresh_token = "old-refresh" }, "storage", true);
            session.Observe(new OAuthInfo { access_token = "new" }, "API bearer");
            Assert.That(session.Tokens.refresh_token, Is.Null);
            Assert.That(session.Observe(new OAuthInfo { access_token = "old", refresh_token = "old-refresh" }, "stale storage", true), Is.False);
            Assert.That(session.Tokens.access_token, Is.EqualTo("new"));
            Assert.That(session.Observe(new OAuthInfo { access_token = "new", refresh_token = "new-refresh" }, "paired storage", true), Is.True);
            Assert.That(session.Observe(new OAuthInfo { access_token = "new" }, "API bearer"), Is.False);
            Assert.That(session.Tokens.refresh_token, Is.EqualTo("new-refresh"));
            int revision = session.Revision;
            session.Clear();
            Assert.That(session.Revision, Is.GreaterThan(revision));
            Assert.That(session.Tokens, Is.Null);
            Assert.That(session.Observe(new OAuthInfo { access_token = "fresh" }, "fresh storage", true), Is.True);
        }

        [Test]
        public void TokenProviderUsesFixedApiAndRequiresReauthenticationOnExpiry()
        {
            var tokens = new OAuthInfo { access_token = "synthetic-access", refresh_token = "synthetic-refresh" };
            var provider = new CapturedTokenProvider(tokens);
            Assert.That(provider.BaseAddress, Is.EqualTo("https://api.centerdevice.de/"));
            Assert.That(provider.GetOAuthInfo(null), Is.SameAs(tokens));
            Assert.Throws<InvalidOperationException>((Action)(() => provider.RefreshToken(tokens)));
        }

        [Test]
        public async Task CapturedBearerPerformsReadOnlySdkIdentityRequest()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var provider = new CapturedTokenProvider(new OAuthInfo { access_token = "synthetic-access" });
                var server = Task.Run(async () =>
                {
                    using (var connection = await listener.AcceptTcpClientAsync())
                    using (var stream = connection.GetStream())
                    {
                        var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                        var request = new StringBuilder();
                        string line;
                        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync())) request.AppendLine(line);
                        const string json = "{\"id\":\"confirmed-user\",\"email\":\"user@example.invalid\",\"tenant-id\":\"confirmed-tenant\"}";
                        var response = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "
                            + Encoding.UTF8.GetByteCount(json) + "\r\nConnection: close\r\n\r\n" + json);
                        await stream.WriteAsync(response, 0, response.Length);
                        return request.ToString();
                    }
                });
                var config = new LoopbackConfig { BaseAddress = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/" };
                var userClient = new Clients.User.UserRestClient(provider, config, provider, "v2/");
                Clients.Tenant.ExtendedUserData identity;
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    identity = await userClient.GetLoggedInUserDataAsync(provider.GetOAuthInfo(null), timeout.Token);
                string requestHeaders = await server;
                Assert.That(requestHeaders, Does.StartWith("GET /v2/user/current HTTP/1.1"));
                Assert.That(requestHeaders, Does.Contain("Authorization: Bearer synthetic-access"));
                Assert.That(identity.Id, Is.EqualTo("confirmed-user"));
                Assert.That(identity.TenantId, Is.EqualTo("confirmed-tenant"));
                Assert.That(provider.GetOAuthInfo(null).UserId, Is.Null, "Identity must be assigned explicitly after successful verification.");
            }
            finally { listener.Stop(); }
        }

        private sealed class LoopbackConfig : IRestClientConfiguration
        {
            public string BaseAddress { get; set; }
            public string UserAgent => "isolated-browser-demo-test";
        }
    }
}
