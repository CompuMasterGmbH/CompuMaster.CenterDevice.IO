using CenterDevice.Rest.Clients.OAuth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.BrowserDemo
{
    // Internal to the sample: these web-client details are not a supported SDK contract.
    internal static class SessionTokens
    {
        internal const string LoginUrl = "https://auth.centerdevice.de/weblogin";
        internal const string ApiUrl = "https://api.centerdevice.de/";
        internal const int MaximumJsonLength = 64 * 1024;

        internal static bool IsSessionOrigin(string address)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0) return false;
            return new[] { "auth.centerdevice.de", "app.centerdevice.de", "api.centerdevice.de" }
                .Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
        }

        internal static bool IsApi(string address) => IsSessionOrigin(address)
            && new Uri(address).Host.Equals("api.centerdevice.de", StringComparison.OrdinalIgnoreCase);

        internal static bool IsWebApplicationPage(string address) => IsSessionOrigin(address)
            && new Uri(address).Host.Equals("app.centerdevice.de", StringComparison.OrdinalIgnoreCase)
            && new Uri(address).AbsolutePath.StartsWith("/tenant/", StringComparison.Ordinal);

        internal static bool IsSessionCookieName(string name) => string.Equals(name, "JSESSIONID", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "SESSION", StringComparison.OrdinalIgnoreCase);

        internal static bool IsTokenResponse(string address)
        {
            if (!IsSessionOrigin(address)) return false;
            var path = new Uri(address).AbsolutePath.TrimEnd('/');
            return path == "/token" || path == "/oauth/token" || path == "/weblogin" || path == "/login";
        }

        internal static OAuthInfo FromBearer(string address, string header)
        {
            if (!IsApi(address) || header == null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
            var value = header.Substring(7).Trim();
            return IsToken(value) ? new OAuthInfo { access_token = value, token_type = "Bearer" } : null;
        }

        // Only named token fields are considered. Passwords, arbitrary cookies and session IDs are not bearer tokens.
        internal static OAuthInfo FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumJsonLength) return null;
            try
            {
                using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { MaxDepth = 12, DateParseHandling = DateParseHandling.None })
                {
                    var value = JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) return null;
                    return FindTokens(value, 0);
                }
            }
            catch (JsonException) { return null; }
        }

        internal static OAuthInfo FromFields(IEnumerable<KeyValuePair<string, string>> fields)
        {
            var container = new JObject();
            foreach (var field in fields)
            {
                string name = Normalize(field.Key);
                if (name != "accesstoken" && name != "refreshtoken" && name != "tokentype" && name != "expiresin") continue;
                if (container.Property(name) != null) return null;
                container.Add(name, field.Value);
            }
            return ReadTokens(container);
        }

        private static OAuthInfo FindTokens(JToken value, int depth)
        {
            if (depth > 6) return null;
            if (value is JObject obj)
            {
                var tokens = ReadTokens(obj);
                if (tokens != null) return tokens;
                foreach (var property in obj.Properties())
                {
                    tokens = FindTokens(property.Value, depth + 1);
                    if (tokens != null) return tokens;
                }
            }
            else if (value.Type == JTokenType.String)
            {
                // Web storage often contains a JSON-encoded object as a string value.
                string text = (string)value;
                if (text != null && text.TrimStart().StartsWith("{", StringComparison.Ordinal))
                {
                    try { return FindTokens(JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }), depth + 1); }
                    catch (JsonException) { }
                }
            }
            return null;
        }

        private static OAuthInfo ReadTokens(JObject obj)
        {
            var fields = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (var property in obj.Properties())
            {
                string name = Normalize(property.Name);
                if (name != "accesstoken" && name != "refreshtoken" && name != "tokentype" && name != "expiresin") continue;
                if (fields.ContainsKey(name)) return null;
                fields.Add(name, property.Value);
            }
            string access = Text(fields, "accesstoken");
            string refresh = Text(fields, "refreshtoken");
            string type = Text(fields, "tokentype");
            if (!IsToken(access) || (type != null && !type.Equals("Bearer", StringComparison.OrdinalIgnoreCase))) return null;
            long.TryParse(Text(fields, "expiresin"), out long expires);
            return new OAuthInfo { access_token = access, refresh_token = IsToken(refresh) ? refresh : null,
                token_type = "Bearer", expires_in = Math.Max(0, expires) };
        }

        private static string Text(Dictionary<string, JToken> fields, string name)
        {
            if (!fields.TryGetValue(name, out var value)) return null;
            return value.Type == JTokenType.String || value.Type == JTokenType.Integer ? value.ToString() : null;
        }

        private static string Normalize(string value) => value.Replace("_", "").Replace("-", "").ToLowerInvariant();
        private static bool IsToken(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 16384
            && !value.Any(char.IsWhiteSpace) && !value.Any(char.IsControl);
    }

    internal sealed class CapturedSession
    {
        internal OAuthInfo Tokens { get; private set; }
        internal string Source { get; private set; }
        internal int Revision { get; private set; }
        private bool networkObserved;

        internal bool Observe(OAuthInfo value, string source, bool fromStorage = false)
        {
            if (value == null) return false;
            // A stale cookie/local-storage snapshot must not replace a token currently used on the wire.
            if (fromStorage && networkObserved && Tokens != null && Tokens.access_token != value.access_token) return false;
            if (!fromStorage) networkObserved = true;
            if (Tokens != null && Tokens.access_token == value.access_token)
            {
                if (string.IsNullOrEmpty(value.refresh_token) || Tokens.refresh_token == value.refresh_token) return false;
                // A refresh token is accepted only together with the same access token in one snapshot.
            }
            Tokens = new OAuthInfo { access_token = value.access_token, refresh_token = value.refresh_token,
                token_type = "Bearer", expires_in = value.expires_in };
            Source = source;
            Revision++;
            return true;
        }

        internal void Clear() { Tokens = null; Source = null; networkObserved = false; Revision++; }
    }
}
