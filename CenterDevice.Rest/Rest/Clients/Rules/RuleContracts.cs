using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Rules
{
    /// <summary>Contains a rule scope, trigger or action and its complete provider data.</summary>
    /// <remarks>Preserves extended metadata, workflow and PDF-marker parameters without truncating their JSON. Advanced payload constraints, visibility, cycles and marker-pattern interpretation remain server validated.</remarks>
    public sealed class RuleComponent
    {
        private readonly JObject data;
        /// <summary>Initializes a component from its type and a defensive data snapshot.</summary>
        /// <param name="type">The nonblank component type.</param>
        /// <param name="data">The nonnull full JSON data object, including an explicit empty object.</param>
        /// <exception cref="ArgumentException">The type is blank.</exception>
        /// <exception cref="ArgumentNullException">The data object is null.</exception>
        public RuleComponent(string type, JObject data)
        {
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("A component type is required.", nameof(type));
            if (data == null) throw new ArgumentNullException(nameof(data));
            Type = type; this.data = (JObject)data.DeepClone();
        }
        /// <summary>Gets the component type, retaining future names in responses.</summary>
        public string Type { get; }
        /// <summary>Gets a defensive copy of the full component data.</summary>
        public JObject Data => (JObject)data.DeepClone();
        internal JObject ToJson() => new JObject { ["type"] = Type, ["data"] = Data };
    }

    /// <summary>Specifies the creation of an own user rule.</summary>
    /// <remarks>Creation defaults to active on the server and can affect shared documents. Scope and trigger types must correspond; actions require at least one definition. The rule type is user.</remarks>
    public sealed class RuleCreateRequest
    {
        /// <summary>Gets or sets the nonnull description, preserving empty text and whitespace.</summary>
        public string Description { get; set; }
        /// <summary>Gets or sets the required collection or folder scope.</summary>
        /// <remarks>Data requires id; omitted include-subfolders retains the true server default.</remarks>
        public RuleComponent Scope { get; set; }
        /// <summary>Gets or sets the required corresponding document-added trigger.</summary>
        public RuleComponent Trigger { get; set; }
        /// <summary>Gets or sets the nonempty ordered action definitions.</summary>
        public List<RuleComponent> Actions { get; set; }
    }

    /// <summary>Specifies selected fields to update on an existing rule.</summary>
    /// <remarks>Null fields remain unchanged. Supplied actions replace the complete action definitions and must not be empty. Rule type is not supported by the update protocol.</remarks>
    public sealed class RuleUpdateRequest
    {
        /// <summary>Gets or sets the selected description, or null to preserve it.</summary>
        public string Description { get; set; }
        /// <summary>Gets or sets the selected scope, or null to preserve it.</summary>
        public RuleComponent Scope { get; set; }
        /// <summary>Gets or sets the selected trigger, or null to preserve it.</summary>
        public RuleComponent Trigger { get; set; }
        /// <summary>Gets or sets the complete replacement actions, or null to preserve them.</summary>
        public List<RuleComponent> Actions { get; set; }
    }

    /// <summary>Contains an immutable own-rule record returned by the server.</summary>
    public sealed class Rule
    {
        internal Rule(string id, string description, string type, string status, string reason, string dateText, DateTimeOffset? date,
            RuleComponent scope, RuleComponent trigger, IReadOnlyList<RuleComponent> actions)
        { Id = id; Description = description; Type = type; Status = status; DeactivationReason = reason; CreationDateText = dateText; CreationDate = date; Scope = scope; Trigger = trigger; Actions = actions; }
        /// <summary>Gets the server-confirmed rule identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original description.</summary>
        public string Description { get; }
        /// <summary>Gets the rule type, retaining unknown future names.</summary>
        public string Type { get; }
        /// <summary>Gets the server status without inferring activation from configuration.</summary>
        public string Status { get; }
        /// <summary>Gets the supplied deactivation reason, or null when omitted.</summary>
        /// <remarks>Retains future reasons without inferring an inactive reason locally.</remarks>
        public string DeactivationReason { get; }
        /// <summary>Gets the exact ISO8601 creation text.</summary>
        public string CreationDateText { get; }
        /// <summary>Gets the creation date when supplied with an explicit timezone offset.</summary>
        /// <remarks>Offset-free text does not acquire the workstation timezone.</remarks>
        public DateTimeOffset? CreationDate { get; }
        /// <summary>Gets the immutable scope and its defensive full data.</summary>
        public RuleComponent Scope { get; }
        /// <summary>Gets the immutable trigger and its defensive full data.</summary>
        public RuleComponent Trigger { get; }
        /// <summary>Gets the read-only ordered actions and their defensive full data.</summary>
        public IReadOnlyList<RuleComponent> Actions { get; }
    }
}
