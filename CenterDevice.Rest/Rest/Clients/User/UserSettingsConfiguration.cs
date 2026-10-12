using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.User
{
    /// <summary>Specifies explicit own-user settings without inserting defaults.</summary>
    public sealed class UserSettingsUpdateRequest : JObject
    {
        /// <summary>Initializes an empty configuration without defaults.</summary>
        public UserSettingsUpdateRequest() { }
        /// <summary>Initializes a configuration by copying complete JSON.</summary>
        /// <param name="other">The original JSON, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source JSON is null.</exception>
        public UserSettingsUpdateRequest(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original locale setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public UserLocaleConfiguration Locale { get { return this["locale"] == null || this["locale"].Type == JTokenType.Null ? null : new UserLocaleConfiguration((JObject)this["locale"]); } set { UserSettingsJson.Set(this, "locale", value); } }
        /// <summary>Gets or sets the original notifications setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public UserNotificationConfiguration Notifications { get { return this["notifications"] == null || this["notifications"].Type == JTokenType.Null ? null : new UserNotificationConfiguration((JObject)this["notifications"]); } set { UserSettingsJson.Set(this, "notifications", value); } }
        /// <summary>Gets or sets the original default-view-mode setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public UserDefaultViewConfiguration DefaultViewMode { get { return this["default-view-mode"] == null || this["default-view-mode"].Type == JTokenType.Null ? null : new UserDefaultViewConfiguration((JObject)this["default-view-mode"]); } set { UserSettingsJson.Set(this, "default-view-mode", value); } }
        /// <summary>Gets or sets the original workflow-config setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public UserWorkflowConfiguration WorkflowConfig { get { return this["workflow-config"] == null || this["workflow-config"].Type == JTokenType.Null ? null : new UserWorkflowConfiguration((JObject)this["workflow-config"]); } set { UserSettingsJson.Set(this, "workflow-config", value); } }
        /// <summary>Gets or sets the original default-collection setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public string DefaultCollection { get { return Value<string>("default-collection"); } set { UserSettingsJson.Set(this, "default-collection", value == null ? null : new JValue(value)); } }
        /// <summary>Gets or sets the original high-contrast-mode setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? HighContrastMode { get { return Value<bool?>("high-contrast-mode"); } set { UserSettingsJson.Set(this, "high-contrast-mode", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original search-include-archived-documents setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? SearchIncludeArchivedDocuments { get { return Value<bool?>("search-include-archived-documents"); } set { UserSettingsJson.Set(this, "search-include-archived-documents", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original use-search-history setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        /// <remarks>Setting false also clears the stored search history on the server.</remarks>
        public bool? UseSearchHistory { get { return Value<bool?>("use-search-history"); } set { UserSettingsJson.Set(this, "use-search-history", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original favorites setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public UserFavoritesConfiguration Favorites { get { return this["favorites"] == null || this["favorites"].Type == JTokenType.Null ? null : new UserFavoritesConfiguration((JObject)this["favorites"]); } set { UserSettingsJson.Set(this, "favorites", value); } }
        /// <summary>Gets or sets the original documents-sort-mode setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public UserDocumentsSortConfiguration DocumentsSortMode { get { return this["documents-sort-mode"] == null || this["documents-sort-mode"].Type == JTokenType.Null ? null : new UserDocumentsSortConfiguration((JObject)this["documents-sort-mode"]); } set { UserSettingsJson.Set(this, "documents-sort-mode", value); } }
        internal string SnapshotForSend() { var snapshot = (JObject)DeepClone(); UserSettingsJson.Validate(snapshot, true); return snapshot.ToString(Newtonsoft.Json.Formatting.None); }
    }

    /// <summary>Specifies the original language and country settings.</summary>
    public sealed class UserLocaleConfiguration : JObject
    {
        /// <summary>Initializes an empty configuration without defaults.</summary>
        public UserLocaleConfiguration() { }
        /// <summary>Initializes a configuration by copying complete JSON.</summary>
        /// <param name="other">The original JSON, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source JSON is null.</exception>
        public UserLocaleConfiguration(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original language setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public string Language { get { return Value<string>("language"); } set { UserSettingsJson.Set(this, "language", value == null ? null : new JValue(value)); } }
        /// <summary>Gets or sets the original country setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public string Country { get { return Value<string>("country"); } set { UserSettingsJson.Set(this, "country", value == null ? null : new JValue(value)); } }
    }

    /// <summary>Specifies complete selected notification channels.</summary>
    public sealed class UserNotificationConfiguration : JObject
    {
        /// <summary>Initializes an empty configuration without defaults.</summary>
        public UserNotificationConfiguration() { }
        /// <summary>Initializes a configuration by copying complete JSON.</summary>
        /// <param name="other">The original JSON, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source JSON is null.</exception>
        public UserNotificationConfiguration(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original email setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public UserEmailNotificationConfiguration Email { get { return this["email"] == null || this["email"].Type == JTokenType.Null ? null : new UserEmailNotificationConfiguration((JObject)this["email"]); } set { UserSettingsJson.Set(this, "email", value); } }
        /// <summary>Gets or sets the original push setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public UserPushNotificationConfiguration Push { get { return this["push"] == null || this["push"].Type == JTokenType.Null ? null : new UserPushNotificationConfiguration((JObject)this["push"]); } set { UserSettingsJson.Set(this, "push", value); } }
    }

    /// <summary>Specifies the complete email notification channel.</summary>
    public sealed class UserEmailNotificationConfiguration : JObject
    {
        /// <summary>Initializes an empty configuration without defaults.</summary>
        public UserEmailNotificationConfiguration() { }
        /// <summary>Initializes a configuration by copying complete JSON.</summary>
        /// <param name="other">The original JSON, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source JSON is null.</exception>
        public UserEmailNotificationConfiguration(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original new-sharing setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? NewSharing { get { return Value<bool?>("new-sharing"); } set { UserSettingsJson.Set(this, "new-sharing", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original new-collection-sharing setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? NewCollectionSharing { get { return Value<bool?>("new-collection-sharing"); } set { UserSettingsJson.Set(this, "new-collection-sharing", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original new-version setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? NewVersion { get { return Value<bool?>("new-version"); } set { UserSettingsJson.Set(this, "new-version", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original new-comment setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? NewComment { get { return Value<bool?>("new-comment"); } set { UserSettingsJson.Set(this, "new-comment", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original group-membership setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? GroupMembership { get { return Value<bool?>("group-membership"); } set { UserSettingsJson.Set(this, "group-membership", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original new-workflow setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? NewWorkflow { get { return Value<bool?>("new-workflow"); } set { UserSettingsJson.Set(this, "new-workflow", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original new-workflow-confirmation-response setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? NewWorkflowConfirmationResponse { get { return Value<bool?>("new-workflow-confirmation-response"); } set { UserSettingsJson.Set(this, "new-workflow-confirmation-response", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original new-workflow-rejection-response setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? NewWorkflowRejectionResponse { get { return Value<bool?>("new-workflow-rejection-response"); } set { UserSettingsJson.Set(this, "new-workflow-rejection-response", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original reminder-triggered setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? ReminderTriggered { get { return Value<bool?>("reminder-triggered"); } set { UserSettingsJson.Set(this, "reminder-triggered", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original daily-summary setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? DailySummary { get { return Value<bool?>("daily-summary"); } set { UserSettingsJson.Set(this, "daily-summary", value.HasValue ? new JValue(value.Value) : null); } }
    }

    /// <summary>Specifies the complete push notification channel.</summary>
    public sealed class UserPushNotificationConfiguration : JObject
    {
        /// <summary>Initializes an empty configuration without defaults.</summary>
        public UserPushNotificationConfiguration() { }
        /// <summary>Initializes a configuration by copying complete JSON.</summary>
        /// <param name="other">The original JSON, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source JSON is null.</exception>
        public UserPushNotificationConfiguration(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original all setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? All { get { return Value<bool?>("all"); } set { UserSettingsJson.Set(this, "all", value.HasValue ? new JValue(value.Value) : null); } }
    }

    /// <summary>Specifies the original default document view.</summary>
    public sealed class UserDefaultViewConfiguration : JObject
    {
        /// <summary>Initializes an empty configuration without defaults.</summary>
        public UserDefaultViewConfiguration() { }
        /// <summary>Initializes a configuration by copying complete JSON.</summary>
        /// <param name="other">The original JSON, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source JSON is null.</exception>
        public UserDefaultViewConfiguration(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original view setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public string View { get { return Value<string>("view"); } set { UserSettingsJson.Set(this, "view", value == null ? null : new JValue(value)); } }
    }

    /// <summary>Specifies the complete default workflow configuration.</summary>
    public sealed class UserWorkflowConfiguration : JObject
    {
        /// <summary>Initializes an empty configuration without defaults.</summary>
        public UserWorkflowConfiguration() { }
        /// <summary>Initializes a configuration by copying complete JSON.</summary>
        /// <param name="other">The original JSON, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source JSON is null.</exception>
        public UserWorkflowConfiguration(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original confirmation-mode setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public string ConfirmationMode { get { return Value<string>("confirmation-mode"); } set { UserSettingsJson.Set(this, "confirmation-mode", value == null ? null : new JValue(value)); } }
        /// <summary>Gets or sets the original share-document-with-users setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public bool? ShareDocumentWithUsers { get { return Value<bool?>("share-document-with-users"); } set { UserSettingsJson.Set(this, "share-document-with-users", value.HasValue ? new JValue(value.Value) : null); } }
        /// <summary>Gets or sets the original visibility setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public string Visibility { get { return Value<string>("visibility"); } set { UserSettingsJson.Set(this, "visibility", value == null ? null : new JValue(value)); } }
    }

    /// <summary>Specifies individually selected favorite resource lists.</summary>
    public sealed class UserFavoritesConfiguration : JObject
    {
        /// <summary>Initializes an empty configuration without defaults.</summary>
        public UserFavoritesConfiguration() { }
        /// <summary>Initializes a configuration by copying complete JSON.</summary>
        /// <param name="other">The original JSON, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source JSON is null.</exception>
        public UserFavoritesConfiguration(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original users setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public IReadOnlyList<string> Users { get { return UserSettingsJson.Strings(this["users"]); } set { UserSettingsJson.Set(this, "users", value == null ? null : new JArray(value)); } }
        /// <summary>Gets or sets the original groups setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public IReadOnlyList<string> Groups { get { return UserSettingsJson.Strings(this["groups"]); } set { UserSettingsJson.Set(this, "groups", value == null ? null : new JArray(value)); } }
        /// <summary>Gets or sets the original collections setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public IReadOnlyList<string> Collections { get { return UserSettingsJson.Strings(this["collections"]); } set { UserSettingsJson.Set(this, "collections", value == null ? null : new JArray(value)); } }
        /// <summary>Gets or sets the original folders setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public IReadOnlyList<string> Folders { get { return UserSettingsJson.Strings(this["folders"]); } set { UserSettingsJson.Set(this, "folders", value == null ? null : new JArray(value)); } }
    }

    /// <summary>Specifies complete fulltext and filter sorting settings.</summary>
    public sealed class UserDocumentsSortConfiguration : JObject
    {
        /// <summary>Initializes an empty configuration without defaults.</summary>
        public UserDocumentsSortConfiguration() { }
        /// <summary>Initializes a configuration by copying complete JSON.</summary>
        /// <param name="other">The original JSON, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source JSON is null.</exception>
        public UserDocumentsSortConfiguration(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original fulltext setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public UserDocumentSortConfiguration Fulltext { get { return this["fulltext"] == null || this["fulltext"].Type == JTokenType.Null ? null : new UserDocumentSortConfiguration((JObject)this["fulltext"]); } set { UserSettingsJson.Set(this, "fulltext", value); } }
        /// <summary>Gets or sets the original filter setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public UserDocumentSortConfiguration Filter { get { return this["filter"] == null || this["filter"].Type == JTokenType.Null ? null : new UserDocumentSortConfiguration((JObject)this["filter"]); } set { UserSettingsJson.Set(this, "filter", value); } }
    }

    /// <summary>Specifies the original sorting field and order.</summary>
    public sealed class UserDocumentSortConfiguration : JObject
    {
        /// <summary>Initializes an empty configuration without defaults.</summary>
        public UserDocumentSortConfiguration() { }
        /// <summary>Initializes a configuration by copying complete JSON.</summary>
        /// <param name="other">The original JSON, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source JSON is null.</exception>
        public UserDocumentSortConfiguration(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original field setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public string Field { get { return Value<string>("field"); } set { UserSettingsJson.Set(this, "field", value == null ? null : new JValue(value)); } }
        /// <summary>Gets or sets the original order setting.</summary>
        /// <value>The explicit setting, or null to omit it. Nested objects and lists are defensively copied.</value>
        public string Order { get { return Value<string>("order"); } set { UserSettingsJson.Set(this, "order", value == null ? null : new JValue(value)); } }
    }

}
