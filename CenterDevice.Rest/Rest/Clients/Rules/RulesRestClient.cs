using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Rules
{
    /// <summary>Manages rules created by the authenticated caller.</summary>
    /// <remarks>Admin, internal and external users can manage own rules; guests cannot. Rules are private but their actions can affect shared documents. Actual scope/target/workflow visibility, payload validity and execution cycles remain server enforced.</remarks>
    public class RulesRestClient : CenterDeviceRestClient
    {
        private static readonly string[] ActionTypes = { "add-tags", "remove-tags", "set-extended-metadata", "remove-extended-metadata", "add-to-collection", "add-to-folder", "move-to-collection", "move-to-folder", "add-workflow", "split-by-marker" };
        /// <summary>Initializes a rule client with the existing authorization and transport configuration.</summary>
        /// <param name="oauthInfoProvider">The authorization information provider.</param>
        /// <param name="configuration">The REST origin and user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public RulesRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>Creates an active own rule and returns its confirmed identity.</summary>
        /// <inheritdoc cref="CreateRuleAsync"/>
        public string CreateRule(string userId, RuleCreateRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => CreateRuleAsync(userId, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates an own rule using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="request">The nonnull rule definition, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed new rule ID.</returns>
        /// <remarks>Creation is active by default, non-idempotent and may affect shared documents. Retain its ID for reconciliation/cleanup. Advanced action data remains server validated and is not stripped; uncertain outcomes are not replayed.</remarks>
        public async Task<string> CreateRuleAsync(string userId, RuleCreateRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Description == null || request.Scope == null || request.Trigger == null || request.Actions == null) throw new ArgumentException("A complete rule definition is required.", nameof(request));
            var snapshot = Definition(request.Description, request.Scope, request.Trigger, request.Actions, true);
            var response = await SendAsync(userId, ApiVersionPrefix + "rules", Method.Post, snapshot, new[] { HttpStatusCode.Created }, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, data => Text(data["id"], true));
        }

        /// <summary>Retrieves a selected own rule, or null when the server reports absence.</summary>
        /// <inheritdoc cref="GetRuleAsync"/>
        public Rule GetRule(string userId, string ruleId, CancellationToken cancellationToken = default(CancellationToken))
            => GetRuleAsync(userId, ruleId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves a selected own rule using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="ruleId">The nonblank selected rule identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The original rule record, or null for the documented HTTP 204 absent result.</returns>
        /// <remarks>Creator/guest errors remain errors; absence does not infer a role or ownership identity.</remarks>
        public async Task<Rule> GetRuleAsync(string userId, string ruleId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await SendAsync(userId, Resource(ruleId), Method.Get, null, new[] { HttpStatusCode.OK, HttpStatusCode.NoContent }, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            return Parse(response.Content, data => { var rule = ParseRule(data); if (Identity(rule.Id) != Identity(ruleId)) throw new InvalidDataException("The rule differs from the selected identity."); return rule; });
        }

        /// <summary>Retrieves the caller's own visible rules.</summary>
        /// <inheritdoc cref="GetRulesAsync"/>
        public IReadOnlyList<Rule> GetRules(string userId, CancellationToken cancellationToken = default(CancellationToken))
            => GetRulesAsync(userId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves own rules using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only page in server order, including an empty page for HTTP 204.</returns>
        /// <remarks>No unlisted paging, total, role or ownership metadata is inferred.</remarks>
        public async Task<IReadOnlyList<Rule>> GetRulesAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await SendAsync(userId, ApiVersionPrefix + "rules", Method.Get, null, new[] { HttpStatusCode.OK, HttpStatusCode.NoContent }, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NoContent) return Array.AsReadOnly(new Rule[0]);
            return Parse<IReadOnlyList<Rule>>(response.Content, data =>
            {
                var array = data["rules"] as JArray; if (array == null) throw new InvalidDataException("A rule array is required.");
                var rules = new List<Rule>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var token in array)
                {
                    if (!(token is JObject item)) throw new InvalidDataException("Rule records must be objects.");
                    var rule = ParseRule(item); if (!ids.Add(Identity(rule.Id))) throw new InvalidDataException("The rule page repeats an identity."); rules.Add(rule);
                }
                return rules.AsReadOnly();
            });
        }

        /// <summary>Updates only the selected own-rule definition fields.</summary>
        /// <inheritdoc cref="UpdateRuleAsync"/>
        public void UpdateRule(string userId, string ruleId, RuleUpdateRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateRuleAsync(userId, ruleId, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Updates selected rule fields using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="ruleId">The nonblank selected own-rule identifier.</param>
        /// <param name="request">The nonnull selected fields; null fields preserve existing values.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed update.</returns>
        /// <remarks>Actions replace the complete definitions; rule type is not sent. The server validates a singly changed scope/trigger against the existing counterpart and enforces actual visibility/configuration. Uncertain outcomes are not replayed.</remarks>
        public Task UpdateRuleAsync(string userId, string ruleId, RuleUpdateRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var resource = Resource(ruleId);
            if (request == null) throw new ArgumentNullException(nameof(request));
            var snapshot = Definition(request.Description, request.Scope, request.Trigger, request.Actions, false);
            if (!snapshot.HasValues) throw new ArgumentException("At least one rule field must be selected.", nameof(request));
            return SendAsync(userId, resource, Method.Put, snapshot, new[] { HttpStatusCode.NoContent }, cancellationToken);
        }

        /// <summary>Activates a selected own rule when its server configuration is valid.</summary>
        /// <inheritdoc cref="ActivateRuleAsync"/>
        public void ActivateRule(string userId, string ruleId, CancellationToken cancellationToken = default(CancellationToken))
            => ActivateRuleAsync(userId, ruleId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Activates an own rule using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="ruleId">The nonblank selected own-rule identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed activation.</returns>
        /// <remarks>The server validates configuration and creator privileges. Activation is idempotent under unchanged objects, but uncertain mutations are not replayed automatically.</remarks>
        public Task ActivateRuleAsync(string userId, string ruleId, CancellationToken cancellationToken = default(CancellationToken))
            => SetActivationAsync(userId, ruleId, "activate", cancellationToken);

        /// <summary>Deactivates a selected own rule.</summary>
        /// <inheritdoc cref="ActivateRuleAsync"/>
        public void DeactivateRule(string userId, string ruleId, CancellationToken cancellationToken = default(CancellationToken))
            => DeactivateRuleAsync(userId, ruleId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Deactivates an own rule using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="ActivateRuleAsync"/>
        /// <remarks>Inactive rules do not trigger until activated again. The server enforces ownership and guest restrictions; no local status is guessed and uncertain outcomes are not replayed.</remarks>
        public Task DeactivateRuleAsync(string userId, string ruleId, CancellationToken cancellationToken = default(CancellationToken))
            => SetActivationAsync(userId, ruleId, "deactivate", cancellationToken);

        /// <summary>Removes a selected own rule without removing its documents.</summary>
        /// <inheritdoc cref="RemoveRuleAsync"/>
        public void RemoveRule(string userId, string ruleId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveRuleAsync(userId, ruleId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Removes an own rule using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="ruleId">The nonblank selected own-rule identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed removal.</returns>
        /// <remarks>Guest/creator restrictions and original failures remain server enforced; no whole-account cleanup or uncertain replay occurs.</remarks>
        public Task RemoveRuleAsync(string userId, string ruleId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SendAsync(userId, Resource(ruleId), Method.Delete, null, new[] { HttpStatusCode.NoContent }, cancellationToken);
        }

        private Task SetActivationAsync(string userId, string ruleId, string action, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return SendAsync(userId, Resource(ruleId), Method.Post, new JObject { ["action"] = action }, new[] { HttpStatusCode.NoContent }, token); }
        private string Resource(string id) { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A selected rule ID is required.", "ruleId"); return ApiVersionPrefix + "rule/" + Uri.EscapeDataString(id); }
        private async Task<RestResponse> SendAsync(string userId, string resource, Method method, JObject body, HttpStatusCode[] expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); var request = CreateRestRequest(resource, method);
            if (body != null) request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(expected)); return response;
        }
        private static JObject Definition(string description, RuleComponent scope, RuleComponent trigger, List<RuleComponent> actions, bool create)
        {
            var data = new JObject(); if (description != null) data["description"] = description;
            if (create) data["type"] = "user";
            if (scope != null)
            {
                var value = scope.ToJson(); var payload = (JObject)value["data"];
                if ((scope.Type != "collection" && scope.Type != "folder") || payload["id"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(payload.Value<string>("id")) ||
                    (payload["include-subfolders"] != null && payload["include-subfolders"].Type != JTokenType.Boolean)) throw new ArgumentException("A scope requires a collection/folder ID and optional boolean include-subfolders.", "request");
                data["scope"] = value;
            }
            if (trigger != null)
            {
                if ((trigger.Type != "document-added-to-collection" && trigger.Type != "document-added-to-folder") || (scope != null && trigger.Type != "document-added-to-" + scope.Type))
                    throw new ArgumentException("The document-added trigger must correspond to its scope.", "request");
                data["trigger"] = trigger.ToJson();
            }
            if (actions != null)
            {
                var selected = actions.ToArray();
                if (selected.Length == 0 || selected.Any(action => action == null || !ActionTypes.Contains(action.Type))) throw new ArgumentException("At least one supported rule action is required.", "request");
                data["actions"] = new JArray(selected.Select(action => action.ToJson()));
            }
            return data;
        }
        private static T Parse<T>(string content, Func<JObject, T> convert)
        {
            try
            {
                using (var text = new StringReader(content ?? "")) using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                { var root = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the rule."); return convert(root); }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException || error is FormatException)
            { throw new InvalidResponseDataException("The rule response has invalid identity, components or metadata.", error); }
        }
        private static Rule ParseRule(JObject data)
        {
            var dateText = Text(data["creation-date"], true);
            if (!Regex.IsMatch(dateText, @"^\d{4}-\d{2}-\d{2}(?:T|$)") || !DateTimeOffset.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) throw new InvalidDataException("Creation date must be usable ISO8601 data.");
            DateTimeOffset? date = Regex.IsMatch(dateText, @"(?:[Zz]|[+-]\d{2}:?\d{2})$") ? (DateTimeOffset?)parsed : null;
            var reasonToken = data["deactivation-reason"]; string reason = null;
            if (reasonToken != null && reasonToken.Type != JTokenType.Null) reason = Text(reasonToken, false);
            var entries = data["actions"] as JArray; if (entries == null || entries.Count == 0) throw new InvalidDataException("A rule requires action definitions.");
            return new Rule(Text(data["id"], true), Text(data["description"], false), Text(data["type"], true), Text(data["status"], true), reason, dateText, date,
                Component(data["scope"]), Component(data["trigger"]), Array.AsReadOnly(entries.Select(Component).ToArray()));
        }
        private static RuleComponent Component(JToken token)
        { if (!(token is JObject item) || !(item["data"] is JObject payload)) throw new InvalidDataException("Rule component data must be an object."); return new RuleComponent(Text(item["type"], true), payload); }
        private static string Text(JToken token, bool nonblank)
        { if (token?.Type != JTokenType.String || (nonblank && string.IsNullOrWhiteSpace(token.Value<string>()))) throw new InvalidDataException("Rule metadata must be usable text."); return token.Value<string>(); }
        private static string Identity(string id) => Guid.TryParse(id, out var value) ? value.ToString("D") : id;
    }
}
