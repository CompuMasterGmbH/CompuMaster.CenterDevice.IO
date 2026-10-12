using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.User
{
    internal static class UserSettingsJson
    {
        internal static void Set(JObject root, string name, JToken value)
        { if (value == null) root.Remove(name); else root[name] = value.DeepClone(); }
        internal static IReadOnlyList<string> Strings(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (!(token is JArray values) || values.Any(value => value.Type != JTokenType.String))
                throw new ArgumentException("A favorite selection must be an array of strings.");
            return Array.AsReadOnly(values.Values<string>().ToArray());
        }
        private static JToken Field(JObject root, string name, JTokenType type, bool write, bool required = false)
        {
            var value = root[name];
            if (value == null || value.Type == JTokenType.Null && !write)
            { if (write && required) throw new ArgumentException("The complete " + name + " setting is required."); return null; }
            if (value.Type != type) throw new ArgumentException("The " + name + " setting has an incompatible JSON kind.");
            return value;
        }
        private static JObject Object(JObject root, string name, bool write, bool required = false)
            => (JObject)Field(root, name, JTokenType.Object, write, required);
        private static void Fields(JObject root, string names, JTokenType type, bool write)
        { foreach (var name in names.Split(',')) Field(root, name, type, write, required: write); }

        internal static void Validate(JObject root, bool write)
        {
            if (write && root.Property("tenant-settings") != null) throw new ArgumentException("User tenant-settings are read-only.");
            var locale = Object(root, "locale", write);
            if (locale != null) Fields(locale, "language,country", JTokenType.String, write);
            var notifications = Object(root, "notifications", write);
            if (notifications != null)
            {
                var email = Object(notifications, "email", write); var push = Object(notifications, "push", write);
                if (write && email == null && push == null) throw new ArgumentException("At least one complete notification channel is required.");
                if (email != null) Fields(email, "new-sharing,new-collection-sharing,new-version,new-comment,group-membership,new-workflow,new-workflow-confirmation-response,new-workflow-rejection-response,reminder-triggered,daily-summary", JTokenType.Boolean, write);
                if (push != null) Fields(push, "all", JTokenType.Boolean, write);
            }
            var view = Object(root, "default-view-mode", write);
            if (view != null) Fields(view, "view", JTokenType.String, write);
            var workflow = Object(root, "workflow-config", write);
            if (workflow != null)
            {
                Fields(workflow, "confirmation-mode,visibility", JTokenType.String, write);
                Fields(workflow, "share-document-with-users", JTokenType.Boolean, write);
            }
            Field(root, "default-collection", JTokenType.String, write);
            foreach (var name in new[] { "high-contrast-mode", "search-include-archived-documents", "use-search-history" })
                Field(root, name, JTokenType.Boolean, write);
            var favorites = Object(root, "favorites", write);
            if (favorites != null) foreach (var name in new[] { "users", "groups", "collections", "folders" })
            { var value = Field(favorites, name, JTokenType.Array, write); if (value != null) Strings(value); }
            var sorting = Object(root, "documents-sort-mode", write);
            if (sorting != null) foreach (var name in new[] { "fulltext", "filter" })
            {
                var selection = Object(sorting, name, write, required: write);
                if (selection == null) continue;
                Fields(selection, "field,order", JTokenType.String, write);
                if (write && selection.Value<string>("order") != "asc" && selection.Value<string>("order") != "desc")
                    throw new ArgumentException("The sort order must be asc or desc.");
                if (write && name == "filter" && selection.Value<string>("field") == "score")
                    throw new ArgumentException("Score sorting is not supported for filter-only searches.");
            }
            var tenant = Object(root, "tenant-settings", write);
            if (tenant != null) foreach (var field in tenant.Properties()) Field(tenant, field.Name, JTokenType.Boolean, write);
        }
    }
}
