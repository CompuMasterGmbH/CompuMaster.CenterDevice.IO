using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.Rules
{
    /// <summary>Selects the requester for a workflow created by a rule.</summary>
    public enum RuleWorkflowCreator
    {
        /// <summary>Uses the user who created the rule.</summary>
        RuleCreator,
        /// <summary>Uses the user whose document action triggered the rule.</summary>
        RuleTriggerer
    }

    /// <summary>Selects a marker format for automated PDF splitting.</summary>
    public enum RuleMarkerType
    {
        /// <summary>Selects the CODE_39 industrial barcode format.</summary>
        Code39,
        /// <summary>Selects the CODE_128 industrial barcode format.</summary>
        Code128,
        /// <summary>Selects the QR_CODE two-dimensional format.</summary>
        QrCode
    }

    /// <summary>Contains an original extended metadata field/value selection for a rule action.</summary>
    public sealed class RuleMetadataValue
    {
        /// <summary>Initializes a metadata field/value selection without numeric or date coercion.</summary>
        /// <param name="key">The usable full user.strings, user.numbers or user.dates field path.</param>
        /// <param name="value">The nonnull original string, decimal text or ISO8601 date text for server validation.</param>
        public RuleMetadataValue(string key, string value)
        {
            RuleComponents.ValidateMetadataKey(key);
            if (value == null) throw new ArgumentNullException(nameof(value));
            Key = key; Value = value;
        }
        /// <summary>Gets the complete original field path.</summary>
        public string Key { get; }
        /// <summary>Gets the original string representation of the value.</summary>
        public string Value { get; }
    }

    /// <summary>Constructs immutable typed components for the current rule contracts.</summary>
    /// <remarks>These factories preserve omitted server defaults and original selections. Advanced raw RuleComponent data remains available. The server enforces actual metadata values, roles, target visibility/hierarchy, workflow availability, cycles and marker-pattern validity.</remarks>
    public static class RuleComponents
    {
        /// <summary>Constructs a selected collection scope.</summary>
        /// <param name="collectionId">The usable selected collection identifier.</param>
        /// <param name="includeSubfolders">The optional descendant choice; null preserves the true server default.</param>
        /// <returns>A defensive immutable scope component.</returns>
        public static RuleComponent CollectionScope(string collectionId, bool? includeSubfolders = null)
            => Scope("collection", collectionId, includeSubfolders);

        /// <summary>Constructs a selected folder scope.</summary>
        /// <param name="folderId">The usable selected folder identifier.</param>
        /// <param name="includeSubfolders">The optional descendant choice; null preserves the true server default.</param>
        /// <returns>A defensive immutable scope component.</returns>
        public static RuleComponent FolderScope(string folderId, bool? includeSubfolders = null)
            => Scope("folder", folderId, includeSubfolders);

        /// <summary>Constructs the document-added trigger for a collection scope.</summary>
        /// <returns>A trigger with an explicit empty data object.</returns>
        public static RuleComponent DocumentAddedToCollectionTrigger() => new RuleComponent("document-added-to-collection", new JObject());

        /// <summary>Constructs the document-added trigger for a folder scope.</summary>
        /// <returns>A trigger with an explicit empty data object.</returns>
        public static RuleComponent DocumentAddedToFolderTrigger() => new RuleComponent("document-added-to-folder", new JObject());

        /// <summary>Constructs a selected add-tags action.</summary>
        /// <param name="tags">A nonempty sequence of nonnull original tag strings, snapshotted once.</param>
        /// <returns>An immutable action retaining original tag text/order.</returns>
        public static RuleComponent AddTags(IEnumerable<string> tags) => Tags("add-tags", tags);

        /// <summary>Constructs a selected remove-tags action.</summary>
        /// <inheritdoc cref="AddTags"/>
        public static RuleComponent RemoveTags(IEnumerable<string> tags) => Tags("remove-tags", tags);

        /// <summary>Constructs an action setting selected extended metadata values.</summary>
        /// <param name="metadata">The nonnull sequence of field/value selections, snapshotted once; an explicit empty sequence remains empty.</param>
        /// <returns>An immutable action with exact key/value strings in order.</returns>
        /// <remarks>Decimal and ISO8601 values remain strings for the server to validate; no local rounding, timezone or field-schema inference occurs.</remarks>
        public static RuleComponent SetExtendedMetadata(IEnumerable<RuleMetadataValue> metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            var values = metadata.ToArray();
            if (values.Any(value => value == null)) throw new ArgumentException("Metadata selections cannot contain null entries.", nameof(metadata));
            return new RuleComponent("set-extended-metadata", new JObject { ["metadata"] = new JArray(values.Select(value => new JObject { ["key"] = value.Key, ["value"] = value.Value })) });
        }

        /// <summary>Constructs an action removing selected extended metadata fields.</summary>
        /// <param name="keys">The nonnull original full field paths, snapshotted once; an explicit empty sequence remains empty.</param>
        /// <returns>An immutable action with a metadata string array.</returns>
        public static RuleComponent RemoveExtendedMetadata(IEnumerable<string> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            var values = keys.ToArray(); foreach (var key in values) ValidateMetadataKey(key);
            return new RuleComponent("remove-extended-metadata", new JObject { ["metadata"] = new JArray(values) });
        }

        /// <summary>Constructs an action adding the document to a collection.</summary>
        /// <param name="collectionId">The usable target collection identifier.</param>
        /// <returns>An immutable action retaining the exact target.</returns>
        public static RuleComponent AddToCollection(string collectionId) => Target("add-to-collection", "collection", collectionId);

        /// <summary>Constructs an action adding the document to a folder.</summary>
        /// <param name="folderId">The usable target folder identifier.</param>
        /// <returns>An immutable action retaining the exact target.</returns>
        /// <remarks>The target folder's collection must differ from the collection where the rule is defined; the server validates this relation.</remarks>
        public static RuleComponent AddToFolder(string folderId) => Target("add-to-folder", "folder", folderId);

        /// <summary>Constructs an action moving the document to a collection.</summary>
        /// <inheritdoc cref="AddToCollection"/>
        /// <remarks>The action adds the target reference and removes the location reference that triggered the rule; the server checks visibility and cycles.</remarks>
        public static RuleComponent MoveToCollection(string collectionId) => Target("move-to-collection", "collection", collectionId);

        /// <summary>Constructs an action moving the document to a folder.</summary>
        /// <inheritdoc cref="AddToFolder"/>
        /// <remarks>With descendant scope enabled, the target cannot be a subfolder of the rule scope. The server validates hierarchy/cycles and removes the triggering location reference.</remarks>
        public static RuleComponent MoveToFolder(string folderId) => Target("move-to-folder", "folder", folderId);

        /// <summary>Constructs an action creating a workflow from a template.</summary>
        /// <param name="workflowTemplateId">The usable selected workflow-template identifier.</param>
        /// <param name="creator">The explicit requester selection, without substituting the authentication context.</param>
        /// <returns>An immutable action retaining template and requester choice.</returns>
        public static RuleComponent AddWorkflow(string workflowTemplateId, RuleWorkflowCreator creator)
        {
            RequireId(workflowTemplateId);
            if (creator != RuleWorkflowCreator.RuleCreator && creator != RuleWorkflowCreator.RuleTriggerer) throw new ArgumentOutOfRangeException(nameof(creator));
            return new RuleComponent("add-workflow", new JObject { ["workflow-template"] = workflowTemplateId, ["workflow-creator"] = creator == RuleWorkflowCreator.RuleCreator ? "rule-creator" : "rule-triggerer" });
        }

        /// <summary>Constructs an action splitting a PDF at selected markers.</summary>
        /// <param name="markerTypes">A nonempty sequence of supported marker formats, snapshotted once.</param>
        /// <param name="removeMarkerPages">Whether marker pages are omitted from the resulting PDFs.</param>
        /// <param name="addMarkerToFilename">Whether recognized values are added to resulting filenames.</param>
        /// <param name="markerIdPath">The usable extended metadata path for recognized marker values; the server validates its schema.</param>
        /// <param name="keepTags">The optional tag retention choice; null preserves the false server default.</param>
        /// <param name="codePatterns">Optional original pattern strings, at most five with .NET length at most 100 each; null omits them and an empty sequence remains explicit.</param>
        /// <returns>An immutable action with the documented JSON marker array and exact options.</returns>
        /// <remarks>Successful splitting erases the original source document. The manufacturer's restricted pattern syntax and quantifier interpretation remain server validated; the client neither compiles nor rewrites these patterns. No action is executed by constructing this component.</remarks>
        public static RuleComponent SplitByMarker(IEnumerable<RuleMarkerType> markerTypes, bool removeMarkerPages, bool addMarkerToFilename,
            string markerIdPath, bool? keepTags = null, IEnumerable<string> codePatterns = null)
        {
            if (markerTypes == null) throw new ArgumentNullException(nameof(markerTypes));
            var types = markerTypes.ToArray();
            if (types.Length == 0) throw new ArgumentException("At least one marker type is required.", nameof(markerTypes));
            var names = types.Select(type => type == RuleMarkerType.Code39 ? "CODE_39" : type == RuleMarkerType.Code128 ? "CODE_128" : type == RuleMarkerType.QrCode ? "QR_CODE" : throw new ArgumentOutOfRangeException(nameof(markerTypes))).ToArray();
            RequireId(markerIdPath);
            var patterns = codePatterns?.ToArray();
            if (patterns != null && (patterns.Length > 5 || patterns.Any(pattern => pattern == null || pattern.Length > 100))) throw new ArgumentException("At most five nonnull patterns of length at most 100 are allowed.", nameof(codePatterns));
            var data = new JObject { ["remove-marker-pages"] = removeMarkerPages, ["add-marker-to-filename"] = addMarkerToFilename, ["marker-types"] = new JArray(names), ["marker-id-path"] = markerIdPath };
            if (keepTags.HasValue) data["keep-tags"] = keepTags.Value;
            if (patterns != null) data["code-patterns"] = new JArray(patterns);
            return new RuleComponent("split-by-marker", data);
        }

        private static RuleComponent Scope(string type, string id, bool? descendants)
        { RequireId(id); var data = new JObject { ["id"] = id }; if (descendants.HasValue) data["include-subfolders"] = descendants.Value; return new RuleComponent(type, data); }
        private static RuleComponent Tags(string type, IEnumerable<string> tags)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags)); var values = tags.ToArray();
            if (values.Length == 0 || values.Any(value => value == null)) throw new ArgumentException("A nonempty sequence of nonnull tags is required.", nameof(tags));
            return new RuleComponent(type, new JObject { ["tags"] = new JArray(values) });
        }
        private static RuleComponent Target(string type, string key, string id) { RequireId(id); return new RuleComponent(type, new JObject { [key] = id }); }
        private static void RequireId(string id) { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A usable selected identifier/path is required.", nameof(id)); }
        internal static void ValidateMetadataKey(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var prefixes = new[] { "user.strings.", "user.numbers.", "user.dates." };
            if (!prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(key.Substring(prefix.Length))))
                throw new ArgumentException("The field must select user.strings, user.numbers or user.dates and a usable field name.", nameof(key));
        }
    }
}
