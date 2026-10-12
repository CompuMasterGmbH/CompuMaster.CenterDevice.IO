using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Statistics
{
    /// <summary>Retrieves statistics for the authenticated caller.</summary>
    /// <remarks>Every user may retrieve their own statistics. The authentication-context identifier is not treated as another user's statistics owner.</remarks>
    public class StatisticsRestClient : CenterDeviceRestClient
    {
        /// <summary>Identifies the byte count across all document versions created by the caller.</summary>
        public const string DocumentsBytesUsed = "documents-bytes-used";

        /// <summary>Initializes a statistics client using the existing authorization and transport configuration.</summary>
        /// <param name="oauthInfoProvider">The authorization information provider.</param>
        /// <param name="configuration">The REST origin and user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public StatisticsRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>Retrieves the selected statistics for the authenticated caller.</summary>
        /// <inheritdoc cref="GetStatisticsAsync"/>
        public UserStatistics GetStatistics(string userId, IEnumerable<string> includes = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetStatisticsAsync(userId, includes, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves the selected statistics using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="includes">The nonempty selection of documents-bytes-used, snapshotted once before authorization, or null to omit the parameter.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The supplied statistics, preserving unknown values when the selection is omitted.</returns>
        /// <remarks>Omitted includes returns HTTP204 with no invented zero or implicit selection. The byte count includes all versions created through uploads, reverts and renames; it is not a current-document-only storage count. Existing expired-token refresh behavior is preserved.</remarks>
        /// <exception cref="ArgumentException">The explicit selection is empty or contains an unsupported statistic.</exception>
        /// <exception cref="InvalidResponseDataException">A selected statistic is missing or has an invalid count.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<UserStatistics> GetStatisticsAsync(string userId, IEnumerable<string> includes = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = includes?.ToArray();
            if (selected != null && (selected.Length == 0 || selected.Any(name => name != DocumentsBytesUsed)))
                throw new ArgumentException("The selection must contain supported statistic names.", nameof(includes));
            var request = CreateRestRequest(ApiVersionPrefix + "stats", Method.Get);
            if (selected != null) request.AddQueryParameter("includes", string.Join(",", selected));
            cancellationToken.ThrowIfCancellationRequested();
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(selected == null ? HttpStatusCode.NoContent : HttpStatusCode.OK));
            if (selected == null) return new UserStatistics(null);
            try
            {
                JObject data;
                using (var text = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(text))
                { data = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the statistics."); }
                if (data[DocumentsBytesUsed]?.Type != JTokenType.Integer)
                    throw new InvalidDataException("Selected document bytes must be an integer.");
                var bytes = data[DocumentsBytesUsed].ToObject<long>();
                if (bytes < 0) throw new InvalidDataException("Document byte counts cannot be negative.");
                return new UserStatistics(bytes);
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is OverflowException || error is InvalidCastException || error is FormatException)
            { throw new InvalidResponseDataException("The statistics response has an invalid byte count.", error); }
        }
    }

    /// <summary>Contains statistics supplied for the authenticated caller.</summary>
    public sealed class UserStatistics
    {
        internal UserStatistics(long? bytes) { DocumentsBytesUsed = bytes; }
        /// <summary>Gets the total bytes across all document versions created by the caller, or null when not requested.</summary>
        /// <remarks>A known zero remains distinct from an omitted statistic. No owner identifier is inferred.</remarks>
        public long? DocumentsBytesUsed { get; }
    }
}
