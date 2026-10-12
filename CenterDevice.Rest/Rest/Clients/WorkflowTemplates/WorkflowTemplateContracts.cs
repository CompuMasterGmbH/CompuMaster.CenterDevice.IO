using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.WorkflowTemplates
{
    /// <summary>Contains an immutable workflow finalizing action and its complete data.</summary>
    /// <remarks>Workflow location keys differ from rule-action keys. Visibility, membership and actual execution remain server-enforced; construction performs no document operation.</remarks>
    public sealed class WorkflowFinalizingAction
    {
        private readonly JObject data;
        /// <summary>Initializes a current finalizing action from a defensive data snapshot.</summary>
        /// <param name="type">The current add/remove-tags, add/move-to-collection/folder or remove-from-location name.</param>
        /// <param name="data">The nonnull complete data with exact tags/location keys; additional data remains original.</param>
        /// <exception cref="ArgumentException">The type or required current data shape is invalid.</exception>
        public WorkflowFinalizingAction(string type, JObject data) : this(type, data, true) { }
        internal WorkflowFinalizingAction(string type, JObject data, bool validate)
        {
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("A finalizing action type is required.", nameof(type));
            if (data == null) throw new ArgumentNullException(nameof(data)); Type = type; this.data = (JObject)data.DeepClone(); if (validate) Validate();
        }
        /// <summary>Gets the original action type, retaining future response names.</summary>
        public string Type { get; }
        /// <summary>Gets a defensive copy of complete original action data.</summary>
        public JObject Data => (JObject)data.DeepClone();
        internal JObject ToJson() => new JObject { ["type"] = Type, ["data"] = Data };
        internal void Validate()
        {
            if (Type == "add-tags" || Type == "remove-tags")
            {
                if (!(data["tags"] is JArray tags) || tags.Count == 0 || tags.Any(tag => tag.Type != JTokenType.String)) throw new ArgumentException("A nonempty textual tag array is required.");
                return;
            }
            if (Type != "add-to-collection" && Type != "add-to-folder" && Type != "move-to-collection" && Type != "move-to-folder" && Type != "remove-from-location") throw new ArgumentException("An unsupported finalizing action was selected.");
            if (Type != "remove-from-location") RequiredId(data[Type.EndsWith("collection", StringComparison.Ordinal) ? "collection" : "folder"]);
            if (Type.StartsWith("move-", StringComparison.Ordinal) || Type == "remove-from-location")
            {
                var collection = data["source-collection"]; var folder = data["source-folder"];
                if ((collection == null) == (folder == null)) throw new ArgumentException("Exactly one source location is required."); RequiredId(collection ?? folder);
            }
        }
        private static void RequiredId(JToken token) { if (token?.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.Value<string>())) throw new ArgumentException("A usable location identifier is required."); }
        /// <summary>Constructs an action that adds original tag strings to the document and attachments.</summary>
        /// <param name="tags">The nonempty exact strings, snapshotted once.</param>
        /// <returns>The immutable action without executing it.</returns>
        public static WorkflowFinalizingAction AddTags(IEnumerable<string> tags) => TagAction("add-tags", tags);
        /// <summary>Constructs an action that removes original tag strings from the document and attachments.</summary>
        /// <inheritdoc cref="AddTags"/>
        public static WorkflowFinalizingAction RemoveTags(IEnumerable<string> tags) => TagAction("remove-tags", tags);
        private static WorkflowFinalizingAction TagAction(string type, IEnumerable<string> tags) { if (tags == null) throw new ArgumentNullException(nameof(tags)); return new WorkflowFinalizingAction(type, new JObject { ["tags"] = new JArray(tags.ToArray()) }); }
        /// <summary>Constructs an action that adds the document and attachments to a collection.</summary>
        /// <param name="collectionId">The exact nonblank destination collection.</param>
        /// <returns>The immutable action without executing it.</returns>
        public static WorkflowFinalizingAction AddToCollection(string collectionId) => new WorkflowFinalizingAction("add-to-collection", new JObject { ["collection"] = collectionId });
        /// <summary>Constructs an action that adds the document and attachments to a folder.</summary>
        /// <param name="folderId">The exact nonblank destination folder.</param>
        /// <returns>The immutable action without executing it.</returns>
        public static WorkflowFinalizingAction AddToFolder(string folderId) => new WorkflowFinalizingAction("add-to-folder", new JObject { ["folder"] = folderId });
        /// <summary>Constructs a document move between selected visible locations.</summary>
        /// <param name="collectionId">The exact nonblank destination collection.</param>
        /// <param name="sourceCollectionId">The selected source collection, or null when selecting a source folder.</param>
        /// <param name="sourceFolderId">The selected source folder, or null when selecting a source collection.</param>
        /// <returns>The immutable action without executing it.</returns>
        /// <remarks>Exactly one source is required. The server checks whether the document is in the source or a subfolder; this is not inferred locally.</remarks>
        public static WorkflowFinalizingAction MoveToCollection(string collectionId, string sourceCollectionId = null, string sourceFolderId = null) => Move("move-to-collection", "collection", collectionId, sourceCollectionId, sourceFolderId);
        /// <summary>Constructs a document move to a selected visible folder.</summary>
        /// <inheritdoc cref="MoveToCollection"/>
        /// <param name="folderId">The exact nonblank destination folder.</param>
        /// <param name="sourceCollectionId">The selected source collection, or null when selecting a source folder.</param>
        /// <param name="sourceFolderId">The selected source folder, or null when selecting a source collection.</param>
        public static WorkflowFinalizingAction MoveToFolder(string folderId, string sourceCollectionId = null, string sourceFolderId = null) => Move("move-to-folder", "folder", folderId, sourceCollectionId, sourceFolderId);
        /// <summary>Constructs removal of the document from a selected source location.</summary>
        /// <param name="sourceCollectionId">The source collection, or null when selecting a source folder.</param>
        /// <param name="sourceFolderId">The source folder, or null when selecting a source collection.</param>
        /// <returns>The immutable action without executing it.</returns>
        /// <remarks>Exactly one source is required; this removes a location reference rather than permanently erasing the document.</remarks>
        public static WorkflowFinalizingAction RemoveFromLocation(string sourceCollectionId = null, string sourceFolderId = null) => Move("remove-from-location", null, null, sourceCollectionId, sourceFolderId);
        private static WorkflowFinalizingAction Move(string type, string key, string id, string collection, string folder)
        { var data = new JObject(); if (key != null) data[key] = id; if (collection != null) data["source-collection"] = collection; if (folder != null) data["source-folder"] = folder; return new WorkflowFinalizingAction(type, data); }
    }

    /// <summary>Selects workflow-template creation or update fields without imposing server defaults.</summary>
    /// <remarks>Null omits fields; explicit false, empty strings/arrays/objects remain explicit. Creation requires name/type; update cannot select ShareDocumentWithUsers. Complete nested data is snapshotted before authorization.</remarks>
    public sealed class WorkflowTemplateOptions
    {
        /// <summary>Gets or sets the exact name, or null to omit it on update.</summary>
        public string Name { get; set; }
        /// <summary>Gets or sets read, approval or metadata-form, or null to omit it on update.</summary>
        public string Type { get; set; }
        /// <summary>Gets or sets original participant user IDs, an explicit empty sequence, or null to omit them.</summary>
        public IEnumerable<string> Users { get; set; }
        /// <summary>Gets or sets original group IDs, an explicit empty sequence, or null to omit them.</summary>
        /// <remarks>Groups resolve at workflow creation; the server enforces the combined 200-participant limit without a guessed client count.</remarks>
        public IEnumerable<string> Groups { get; set; }
        /// <summary>Gets or sets one/all, or null to omit it.</summary>
        /// <remarks>Metadata-form requires omission or one; other omitted defaults are not invented.</remarks>
        public string ConfirmationMode { get; set; }
        /// <summary>Gets or sets complete metadata-form completion data.</summary>
        /// <value>The original metadata/metadata-templates object, an explicit empty object, or null to omit it.</value>
        /// <remarks>Required for metadata-form creation. Exact default strings and template IDs remain original; effective fields resolve at workflow creation and require at least one field then.</remarks>
        public JObject CompletionCondition { get; set; }
        /// <summary>Gets or sets confirmed-result finalizing actions, an explicit empty sequence, or null to omit them.</summary>
        /// <remarks>Currently at most one action is allowed for each result.</remarks>
        public IEnumerable<WorkflowFinalizingAction> ConfirmedActions { get; set; }
        /// <summary>Gets or sets rejected-result finalizing actions, an explicit empty sequence, or null to omit them.</summary>
        /// <remarks>A rejected result is available only for approval workflows; unknown existing update type remains server-validated.</remarks>
        public IEnumerable<WorkflowFinalizingAction> RejectedActions { get; set; }
        /// <summary>Gets or sets automatic document sharing for workflows created from the template.</summary>
        /// <value>The explicit creation choice, or null to omit it and retain true; this field cannot be updated.</value>
        public bool? ShareDocumentWithUsers { get; set; }
        /// <summary>Gets or sets the notification comment, including empty text, or null to omit it.</summary>
        public string Comment { get; set; }
        /// <summary>Gets or sets document/participant visibility, or null to omit it and retain the participant creation default.</summary>
        public string Visibility { get; set; }
        /// <summary>Gets or sets no/on-completion sharing removal, or null to omit it and retain the no creation default.</summary>
        public string RemoveCreatedSharings { get; set; }
    }

    /// <summary>Contains immutable own-template detail without inventing creator or effective participants.</summary>
    public sealed class WorkflowTemplate
    {
        private readonly JObject data;
        internal WorkflowTemplate(string id, string name, string type, JObject data) { Id = id; Name = name; Type = type; this.data = (JObject)data.DeepClone(); }
        /// <summary>Gets the original template identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original name.</summary>
        public string Name { get; }
        /// <summary>Gets the original type, retaining future values.</summary>
        public string Type { get; }
        /// <summary>Gets the original confirmation mode, or null when absent or JSON null.</summary>
        public string ConfirmationMode => data.Value<string>("confirmation-mode");
        /// <summary>Gets the original comment, or null when absent or JSON null.</summary>
        public string Comment => data.Value<string>("comment");
        /// <summary>Gets original visibility without inferring an omitted participant default.</summary>
        public string Visibility => data.Value<string>("visibility");
        /// <summary>Gets original sharing-removal mode without inferring an omitted no default.</summary>
        public string RemoveCreatedSharings => data.Value<string>("remove-created-sharings");
        /// <summary>Gets original automatic-sharing choice without inferring an omitted true default.</summary>
        public bool? ShareDocumentWithUsers => data.Value<bool?>("share-document-with-users");
        /// <summary>Gets copied read-only original user IDs, or null when absent or JSON null.</summary>
        public IReadOnlyList<string> Users => ReadIds("users");
        /// <summary>Gets copied read-only original group IDs, or null when absent or JSON null.</summary>
        /// <remarks>These are template selections rather than resolved workflow participants.</remarks>
        public IReadOnlyList<string> Groups => ReadIds("groups");
        /// <summary>Gets a defensive copy of original completion data, or null when absent or JSON null.</summary>
        public JObject CompletionCondition => (JObject)(data["completion-condition"] as JObject)?.DeepClone();
        /// <summary>Gets a defensive copy of complete original finalizing data, or null when absent or JSON null.</summary>
        public JObject FinalizingActions => (JObject)(data["finalizing-actions"] as JObject)?.DeepClone();
        /// <summary>Gets a defensive copy of the complete original template envelope, including future fields.</summary>
        public JObject Data => (JObject)data.DeepClone();
        private IReadOnlyList<string> ReadIds(string key) => data[key] is JArray entries ? Array.AsReadOnly(entries.Values<string>().ToArray()) : null;
    }
}
