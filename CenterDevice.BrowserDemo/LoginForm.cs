using CenterDevice.IO;
using CenterDevice.Rest.Clients;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CenterDevice.BrowserDemo
{
    internal sealed class LoginForm : Form
    {
        private readonly WebView2 browser = new WebView2 { Dock = DockStyle.Fill };
        private readonly CapturedSession session = new CapturedSession();
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        private readonly Label details = new Label { Dock = DockStyle.Fill, AutoSize = true };
        private readonly TextBox access = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, UseSystemPasswordChar = true };
        private readonly TextBox refresh = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, UseSystemPasswordChar = true };
        private readonly CheckBox reveal = new CheckBox { Text = "Tokenwerte anzeigen", AutoSize = true };
        private readonly Button verify = new Button { Text = "SDK-Zugriff prüfen (nur lesen)", AutoSize = true, Enabled = false };
        private readonly Button restart = new Button { Text = "Neue Anmeldung / Sitzung löschen", AutoSize = true, Enabled = false };
        private readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer { Interval = 1500 };
        private CancellationTokenSource requestCancellation = new CancellationTokenSource();
        private bool observing;
        private bool pollBusy;
        private bool verifying;
        private bool closing;
        private int browserGeneration;
        private int apiRequests;
        private string browserDiagnostics = "";
        // Created only after the API has confirmed the identity, never from a login name or an unverified token claim.
        private CenterDeviceIOClient ioClient;

        internal LoginForm()
        {
            Text = "CenterDevice – Browser-Anmeldung und Tokens (experimentelle Demo)";
            ClientSize = new Size(1120, 880);
            MinimumSize = new Size(900, 700);
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8),
                Text = "Im CenterDevice-Fenster mit dem eigenen Konto anmelden. Die Demo übernimmt erkannte Tokens dieser Websitzung.\r\nExperimenteller Zugriff ohne eigene API-Clientregistrierung; keine automatische Token-Erneuerung." }, 0, 0);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(5) };
            toolbar.Controls.Add(restart);
            toolbar.Controls.Add(verify);
            layout.Controls.Add(toolbar, 0, 1);
            layout.Controls.Add(status, 0, 2);
            layout.Controls.Add(browser, 0, 3);
            var tokens = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
            tokens.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tokens.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tokens.Controls.Add(new Label { Text = "Access-Token", AutoSize = true }, 0, 0);
            tokens.Controls.Add(access, 1, 0);
            tokens.Controls.Add(new Label { Text = "Refresh-Token", AutoSize = true }, 0, 1);
            tokens.Controls.Add(refresh, 1, 1);
            tokens.Controls.Add(reveal, 1, 2);
            tokens.Controls.Add(details, 0, 3);
            tokens.SetColumnSpan(details, 2);
            layout.Controls.Add(tokens, 0, 4);
            Controls.Add(layout);
            reveal.CheckedChanged += (_, __) => access.UseSystemPasswordChar = refresh.UseSystemPasswordChar = !reveal.Checked;
            restart.Click += async (_, __) => await RestartAsync();
            verify.Click += async (_, __) => await VerifyAsync();
            poll.Tick += async (_, __) => await InspectStorageAsync();
            Shown += async (_, __) => await InitializeBrowserAsync();
            FormClosing += (_, __) => { closing = true; observing = false; poll.Stop(); requestCancellation.Cancel(); };
            FormClosed += (_, __) => { poll.Dispose(); requestCancellation.Dispose(); };
            RefreshDisplay();
        }

        private async Task InitializeBrowserAsync()
        {
            status.Text = "Browser wird gestartet …";
            try
            {
                // Dedicated profile, never the user's Edge/Chrome profile. InPrivate avoids persisting login data.
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CompuMaster", "CenterDevice.BrowserDemo", "WebView2");
                var environment = await CoreWebView2Environment.CreateAsync(null, folder);
                if (closing) return;
                var options = environment.CreateCoreWebView2ControllerOptions();
                options.ProfileName = "Session-" + Guid.NewGuid().ToString("N");
                options.IsInPrivateModeEnabled = true;
                await browser.EnsureCoreWebView2Async(environment, options);
                if (closing) return;
                var core = browser.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsPasswordAutosaveEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += OnWebResourceRequested;
                core.WebResourceResponseReceived += OnWebResourceResponseReceived;
                // Keep user-initiated SSO popups in the same observed control and profile.
                core.NewWindowRequested += (_, e) =>
                {
                    e.Handled = true;
                    if (e.IsUserInitiated && Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && uri.Scheme == "https") core.Navigate(e.Uri);
                };
                core.NavigationCompleted += (_, e) =>
                {
                    if (!closing && observing && !e.IsSuccess) status.Text = "Die Browserseite konnte nicht geladen werden (" + e.WebErrorStatus + "). Neue Anmeldung versuchen.";
                };
                observing = true;
                restart.Enabled = true;
                poll.Start();
                core.Navigate(SessionTokens.LoginUrl);
                status.Text = "Bitte im eingebetteten Browser anmelden. Noch kein verwendbares Access-Token erkannt.";
            }
            catch (Exception)
            {
                if (!closing) status.Text = "Browser konnte nicht gestartet werden. Microsoft Edge WebView2 Runtime installieren und die Demo neu starten.";
            }
        }

        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (!observing || closing || !SessionTokens.IsApi(e.Request.Uri)) return;
            apiRequests++;
            var header = e.Request.Headers.Contains("Authorization") ? e.Request.Headers.GetHeader("Authorization") : null;
            Observe(SessionTokens.FromBearer(e.Request.Uri, header), "Bearer-Header an api.centerdevice.de");
        }

        private async void OnWebResourceResponseReceived(object sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            if (!observing || closing || !SessionTokens.IsTokenResponse(e.Request.Uri)
                || e.Response.StatusCode < 200 || e.Response.StatusCode >= 300
                || !e.Response.Headers.Contains("Content-Type")
                || !e.Response.Headers.GetHeader("Content-Type").StartsWith("application/json", StringComparison.OrdinalIgnoreCase)) return;
            int generation = browserGeneration;
            try
            {
                using (var content = await e.Response.GetContentAsync())
                {
                    if (content == null) return;
                    var buffer = new byte[SessionTokens.MaximumJsonLength + 1];
                    int count = 0;
                    while (count < buffer.Length)
                    {
                        int read = await content.ReadAsync(buffer, count, buffer.Length - count);
                        if (read == 0) break;
                        count += read;
                    }
                    if (count > SessionTokens.MaximumJsonLength || !observing || closing || generation != browserGeneration) return;
                    Observe(SessionTokens.FromJson(Encoding.UTF8.GetString(buffer, 0, count)), "JSON-Antwort des CenterDevice-Anmeldeendpunkts");
                }
            }
            catch (Exception) { /* Navigation/disposal may invalidate a response. Never log its content. */ }
        }

        private async Task InspectStorageAsync()
        {
            if (pollBusy || !observing || closing) return;
            pollBusy = true;
            int generation = browserGeneration;
            try
            {
                var core = browser.CoreWebView2;
                if (SessionTokens.IsSessionOrigin(core.Source))
                {
                    // Do not inspect the DOM or login fields. Keep local/session storage separate.
                    var json = await core.ExecuteScriptAsync(@"(() => {
                        if (!['https://auth.centerdevice.de', 'https://app.centerdevice.de', 'https://api.centerdevice.de'].includes(location.origin)) return null;
                        const copy = store => { const result = {}; let size = 0;
                            for (let i = 0; i < store.length; i++) { const k = store.key(i), v = store.getItem(k);
                                if (k && v && /token|oauth|auth|session/i.test(k) && (size += k.length + v.length) <= 24000) result[k] = v; }
                            return result; };
                        return {local: copy(localStorage), session: copy(sessionStorage)};
                    })()");
                    if (!CanObserve(generation)) return;
                    Observe(SessionTokens.FromJson(json), "Web-Speicher der CenterDevice-Seite", true);
                }
                var cookieNames = new HashSet<string>(StringComparer.Ordinal);
                bool hasSessionCookie = false;
                bool hasTokenNamedCookie = false;
                foreach (var origin in new[] { "https://auth.centerdevice.de/", "https://app.centerdevice.de/", SessionTokens.ApiUrl })
                {
                    var cookies = await core.CookieManager.GetCookiesAsync(origin);
                    if (!CanObserve(generation)) return;
                    foreach (var cookie in cookies)
                    {
                        // Only counts/classification reach the UI. Names, values and session IDs are not printed or logged.
                        cookieNames.Add(cookie.Domain + "\n" + cookie.Path + "\n" + cookie.Name);
                        hasSessionCookie |= SessionTokens.IsSessionCookieName(cookie.Name);
                        hasTokenNamedCookie |= cookie.Name.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0
                            || cookie.Name.IndexOf("oauth", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    // A pair must belong to the same cookie domain and path; never pair across hosts/scopes.
                    foreach (var scope in cookies.GroupBy(c => new { c.Domain, c.Path }))
                        Observe(SessionTokens.FromFields(scope.Select(c => new KeyValuePair<string, string>(c.Name, c.Value))), "Token-Cookies der CenterDevice-Sitzung", true);
                }
                browserDiagnostics = "Browser-Diagnose: " + cookieNames.Count + " Cookies; Session-Cookie: "
                    + (hasSessionCookie ? "vorhanden" : "nicht erkannt") + "; Cookie mit Token-Namen: "
                    + (hasTokenNamedCookie ? "vorhanden" : "nicht erkannt") + "; Anfragen an die öffentliche API: " + apiRequests + ".";
                if (session.Tokens == null)
                {
                    if (SessionTokens.IsWebApplicationPage(core.Source))
                        status.Text = "Webanmeldung erfolgt. Bisher kein API-Access-Token erkannt. Die Websitzung kann serverseitig verwaltet sein.";
                    RefreshDisplay();
                }
            }
            catch (Exception) { /* Storage may be unavailable while a page is navigating. */ }
            finally { pollBusy = false; }
        }

        private bool CanObserve(int generation) => !closing && observing && generation == browserGeneration;

        private void Observe(CenterDevice.Rest.Clients.OAuth.OAuthInfo tokens, string source, bool fromStorage = false)
        {
            if (!session.Observe(tokens, source, fromStorage)) return;
            requestCancellation.Cancel();
            ioClient = null;
            reveal.Checked = false;
            RefreshDisplay();
            status.Text = "Access-Token erkannt. Mit „SDK-Zugriff prüfen“ kann die API-Verwendbarkeit geprüft werden.";
        }

        private void RefreshDisplay()
        {
            access.Text = session.Tokens?.access_token ?? "";
            refresh.Text = session.Tokens?.refresh_token ?? "";
            verify.Enabled = session.Tokens != null && !verifying;
            details.Text = session.Tokens == null ? "Tokenwerte bleiben bis zum Erkennen leer. Die Demo speichert keine Tokens in Dateien.\r\n" + browserDiagnostics
                : "Quelle: " + session.Source + "\r\nRefresh-Token: " + (session.Tokens.refresh_token == null ? "nicht verfügbar" : "vorhanden")
                    + ". Automatische Erneuerung ist nicht implementiert.\r\nSchließen beendet das private Browserprofil. „Neue Anmeldung“ löscht zusätzlich dessen Browserdaten.";
        }

        private async Task VerifyAsync()
        {
            var tokens = session.Tokens;
            if (tokens == null || verifying) return;
            int revision = session.Revision;
            requestCancellation.Dispose();
            requestCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            verifying = true;
            RefreshDisplay();
            status.Text = "SDK liest den aktuellen Benutzer …";
            try
            {
                var provider = new CapturedTokenProvider(tokens);
                var rest = new CenterDeviceClient(provider, provider, provider);
                var identity = await rest.User.GetLoggedInUserDataAsync(tokens, requestCancellation.Token);
                if (closing || revision != session.Revision) return;
                if (string.IsNullOrWhiteSpace(identity?.Id) || string.IsNullOrWhiteSpace(identity.TenantId))
                    throw new InvalidOperationException();
                tokens.UserId = identity.Id;
                tokens.Email = identity.Email;
                tokens.TenantId = identity.TenantId;
                ioClient = new CenterDeviceIOClient(rest, identity.Id);
                status.Text = "SDK-Zugriff erfolgreich. Benutzer: " + identity.Email + " | Benutzer-ID: " + identity.Id
                    + " | Mandant: " + identity.TenantId + ". REST- und IO-Client sind initialisiert.";
            }
            catch (OperationCanceledException)
            {
                if (!closing && revision == session.Revision) status.Text = "SDK-Prüfung abgebrochen oder Zeitlimit erreicht. Erneut prüfen oder neu anmelden.";
            }
            catch (Exception)
            {
                if (!closing && revision == session.Revision) status.Text = "SDK-Zugriff nicht bestätigt. Das Token kann abgelaufen oder für diese API ungeeignet sein. Erneut anmelden. Es wurden keine Dokumente verändert.";
            }
            finally
            {
                verifying = false;
                if (!closing) RefreshDisplay();
            }
        }

        private async Task RestartAsync()
        {
            observing = false;
            browserGeneration++;
            requestCancellation.Cancel();
            browser.CoreWebView2.Stop();
            session.Clear();
            apiRequests = 0;
            browserDiagnostics = "";
            ioClient = null;
            reveal.Checked = false;
            RefreshDisplay();
            restart.Enabled = false;
            try
            {
                await browser.CoreWebView2.Profile.ClearBrowsingDataAsync();
                if (closing) return;
                observing = true;
                browser.CoreWebView2.Navigate(SessionTokens.LoginUrl);
                status.Text = "Lokale Sitzung gelöscht. Bitte erneut anmelden.";
            }
            catch (Exception)
            {
                if (!closing) status.Text = "Browserdaten konnten nicht vollständig gelöscht werden. Demo schließen und neu starten.";
            }
            finally { if (!closing) restart.Enabled = true; }
        }
    }
}
