using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;

namespace CenterDevice.Rest.Clients.Documents
{
    internal static class DocumentSearchContractValidation
    {
        internal static JObject Snapshot(DocumentSearchParameters request)
        {
            var data = (JObject)request.DeepClone();
            OptionalInteger(data, "offset", false); OptionalInteger(data, "rows", false); OptionalBoolean(data, "deep-folder-facets");
            var query = OptionalObject(data, "query");
            if (query != null)
            {
                if (query["text"] != null) Text(Unnegated(query["text"]));
                if (query["scope"] != null) { var scope = query["scope"]; if (scope is JArray) StringArray(scope); else Text(scope); }
            }
            var filter = OptionalObject(data, "filter");
            if (filter != null)
            {
                foreach (var name in new[] { "ids", "filenames", "extensions", "mimetypes", "titles", "authors", "owners", "tags", "groups", "collections", "hashes", "locked-by" })
                    if (filter[name] != null) StringArray(Unnegated(filter[name]));
                if (filter["locked"] != null) Boolean(Unnegated(filter["locked"]));
                if (filter["folders"] != null)
                { var folders = Object(Unnegated(filter["folders"])); if (folders["ids"] != null) StringArray(folders["ids"]); OptionalBoolean(folders, "deep"); }
                if (filter["size"] != null)
                {
                    var size = Object(Unnegated(filter["size"]));
                    foreach (var name in new[] { "from", "to" }) if (size[name] != null)
                    {
                        long parsed; if (size[name].Type != JTokenType.Integer || !long.TryParse(size[name].ToString(Formatting.None), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) throw new ArgumentException("Size bounds must be Int64 JSON integers.");
                    }
                }
                foreach (var name in new[] { "upload-date", "last-change", "document-date", "archived-date" })
                    if (filter[name] != null) DateRange(Object(Unnegated(filter[name])));
                if (filter["extended-metadata"] != null) MetadataQuery(Object(Unnegated(filter["extended-metadata"])));
            }
            var named = OptionalArray(data, "named");
            if (named != null) foreach (var entry in named)
            {
                var item = Object(entry); var name = RequiredText(item, "name"); var parameters = OptionalObject(item, "params");
                if (name == "public-collections" || name == "archived") { if (parameters == null || parameters["include"] == null) throw new ArgumentException("The named query requires an explicit include choice."); Boolean(parameters["include"]); }
                if (name == "workflow") { if (parameters == null) throw new ArgumentException("The workflow named query requires parameters."); RequiredText(parameters, "status"); RequiredText(parameters, "role"); }
            }
            var fields = OptionalObject(data, "fields"); if (fields != null) Fields(fields);
            var sort = OptionalObject(data, "sort");
            if (sort != null)
            {
                if (sort["field"] != null) Text(sort["field"]); if (sort["order"] != null) Text(sort["order"]);
                var parameters = OptionalObject(sort, "params");
                if (sort.Value<string>("field") == "extended-metadata" && (parameters == null || parameters["key"] == null)) throw new ArgumentException("Extended metadata sorting requires an explicit key.");
                if (parameters?["key"] != null) Text(parameters["key"]);
            }
            var facets = OptionalArray(data, "facet");
            if (facets != null) foreach (var entry in facets)
            {
                var facet = Object(entry); RequiredText(facet, "field"); OptionalInteger(facet, "size", true);
                if (facet["qualifier"] != null) Text(facet["qualifier"]); var parameters = OptionalObject(facet, "params");
                if (parameters != null) foreach (var name in new[] { "key", "section" }) if (parameters[name] != null) Text(parameters[name]);
            }
            return data;
        }
        internal static void Fields(JObject fields)
        {
            if (fields["includes"] != null && fields["excludes"] != null) throw new ArgumentException("Included and excluded fields cannot be supplied together.");
            foreach (var name in new[] { "includes", "excludes" }) if (fields[name] != null) { StringArray(fields[name]); if (((JArray)fields[name]).Any(value => string.IsNullOrWhiteSpace(value.Value<string>()))) throw new ArgumentException("Metadata field names must be nonblank."); }
            if (fields["excludes"] is JArray excluded && excluded.Values<string>().Contains("base")) throw new ArgumentException("The base shortcut cannot be excluded.");
        }
        private static JToken Unnegated(JToken value)
        {
            if (value is JObject wrapper && wrapper.Property("not") != null)
            { if (wrapper.Properties().Count() != 1) throw new ArgumentException("A negation wrapper must contain only its not selection."); return wrapper["not"]; }
            return value;
        }
        private static void MetadataQuery(JObject query)
        {
            if (query.Property("or") != null)
            {
                if (query.Properties().Count() != 1 || !(query["or"] is JArray units)) throw new ArgumentException("An OR metadata query must contain only an array of base units.");
                foreach (var unit in units) MetadataBase(Object(unit));
            }
            else MetadataBase(query);
        }
        private static void MetadataBase(JObject query)
        {
            foreach (var field in query.Properties())
            {
                if (field.Value.Type == JTokenType.Null) continue;
                if (field.Value is JObject range) DateRange(range);
                else if (field.Value is JArray) StringArray(field.Value);
                else Text(field.Value);
            }
        }
        private static void DateRange(JObject range) { foreach (var name in new[] { "from", "to" }) if (range[name] != null) Text(range[name]); }
        private static string RequiredText(JObject data, string name) { var text = Text(data[name]); if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("The " + name + " selection is required."); return text; }
        private static string Text(JToken value) { if (value == null || value.Type != JTokenType.String) throw new ArgumentException("A known query value must be an original string."); return value.Value<string>(); }
        private static void Boolean(JToken value) { if (value == null || value.Type != JTokenType.Boolean) throw new ArgumentException("A known query choice must be a boolean."); }
        private static JObject Object(JToken value) { if (!(value is JObject data)) throw new ArgumentException("A known query section must be an object."); return data; }
        private static void StringArray(JToken value) { if (!(value is JArray values) || values.Any(item => item.Type != JTokenType.String)) throw new ArgumentException("A known selection must be an array of strings."); }
        private static JObject OptionalObject(JObject data, string name) => data[name] == null ? null : Object(data[name]);
        private static JArray OptionalArray(JObject data, string name) { if (data[name] == null) return null; if (!(data[name] is JArray values)) throw new ArgumentException("The " + name + " selection must be an array."); return values; }
        private static void OptionalBoolean(JObject data, string name) { if (data[name] != null) Boolean(data[name]); }
        private static void OptionalInteger(JObject data, string name, bool positive)
        { var value = data[name]; if (value != null && (value.Type != JTokenType.Integer || !System.Text.RegularExpressions.Regex.IsMatch(value.ToString(Formatting.None), positive ? @"^[1-9][0-9]*$" : @"^(0|[1-9][0-9]*)$"))) throw new ArgumentException("The " + name + " selection must be an integer in its documented range."); }
    }
}
