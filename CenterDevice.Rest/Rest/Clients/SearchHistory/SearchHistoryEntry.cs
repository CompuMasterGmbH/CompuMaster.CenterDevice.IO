using System;

namespace CenterDevice.Rest.Clients.SearchHistory
{
    /// <summary>Selects the server sorting for search-history retrieval.</summary>
    public enum SearchHistorySort
    {
        /// <summary>Orders entries by descending last-use timestamp.</summary>
        MostRecentFirst,
        /// <summary>Orders entries by descending recorded usage count.</summary>
        MostUsedFirst
    }

    /// <summary>Contains the server-confirmed explicit usage of a search term.</summary>
    /// <remarks>Search requests do not automatically update this record. No creator identity is inferred from the authentication context.</remarks>
    public sealed class SearchHistoryEntry
    {
        internal SearchHistoryEntry(string id, string term, long count, string timestampText, DateTimeOffset? timestamp)
        { Id = id; Term = term; Count = count; TimestampText = timestampText; Timestamp = timestamp; }
        /// <summary>Gets the entry's confirmed identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original stored term text without changing its case or whitespace.</summary>
        public string Term { get; }
        /// <summary>Gets the recorded number of explicit term usages.</summary>
        public long Count { get; }
        /// <summary>Gets the exact ISO8601 last-use timestamp text.</summary>
        public string TimestampText { get; }
        /// <summary>Gets the last-use timestamp when its text contains an explicit timezone offset.</summary>
        /// <remarks>Offset-free text does not acquire the workstation timezone.</remarks>
        public DateTimeOffset? Timestamp { get; }
    }
}
