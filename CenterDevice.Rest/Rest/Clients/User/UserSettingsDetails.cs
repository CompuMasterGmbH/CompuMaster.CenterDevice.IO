using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CenterDevice.Rest.Clients.User
{
    /// <summary>Contains immutable selected own-user settings with defensively copied configuration objects.</summary>
    public sealed class UserSettingsDetails
    {
        private readonly UserSettingsUpdateRequest settings;
        internal UserSettingsDetails(JObject raw)
        {
            UserSettingsJson.Validate(raw, false); settings = new UserSettingsUpdateRequest(raw);
            if (raw["tenant-settings"] is JObject values)
            {
                var result = new Dictionary<string, bool?>();
                foreach (var field in values.Properties()) result.Add(field.Name, field.Value.Type == JTokenType.Null ? (bool?)null : field.Value.Value<bool>());
                TenantSettings = new ReadOnlyDictionary<string, bool?>(result);
            }
        }
        /// <summary>Gets a defensive copy of the locale, or null when not provided.</summary>
        public UserLocaleConfiguration Locale => settings.Locale;
        /// <summary>Gets a defensive copy of notification channels, or null when not provided.</summary>
        public UserNotificationConfiguration Notifications => settings.Notifications;
        /// <summary>Gets a defensive copy of the default view, or null when not provided.</summary>
        public UserDefaultViewConfiguration DefaultViewMode => settings.DefaultViewMode;
        /// <summary>Gets a defensive copy of the default workflow configuration, or null when not provided.</summary>
        public UserWorkflowConfiguration WorkflowConfig => settings.WorkflowConfig;
        /// <summary>Gets the original default collection selection, or null when not provided.</summary>
        public string DefaultCollection => settings.DefaultCollection;
        /// <summary>Gets the explicit high-contrast choice, or null when not provided.</summary>
        public bool? HighContrastMode => settings.HighContrastMode;
        /// <summary>Gets the explicit archived-search choice, or null when not provided.</summary>
        public bool? SearchIncludeArchivedDocuments => settings.SearchIncludeArchivedDocuments;
        /// <summary>Gets the explicit search-history choice when returned, or null when not provided.</summary>
        public bool? UseSearchHistory => settings.UseSearchHistory;
        /// <summary>Gets a defensive copy of favorite selections, or null when not provided.</summary>
        public UserFavoritesConfiguration Favorites => settings.Favorites;
        /// <summary>Gets a defensive copy of fulltext/filter sorting, or null when not provided.</summary>
        public UserDocumentsSortConfiguration DocumentsSortMode => settings.DocumentsSortMode;
        /// <summary>Gets original nullable tenant-setting indications, or null when not provided.</summary>
        /// <remarks>True grants the respective action at the time of the call. False selects default behavior and is not a denial; role, ownership and other criteria may still permit the action. Missing or null values remain unknown.</remarks>
        public IReadOnlyDictionary<string, bool?> TenantSettings { get; }
        /// <summary>Returns a defensive copy of the complete selected settings.</summary>
        /// <returns>Original settings, including future fields and read-only tenant indications.</returns>
        public JObject GetRawSettings() => (JObject)settings.DeepClone();
    }
}
