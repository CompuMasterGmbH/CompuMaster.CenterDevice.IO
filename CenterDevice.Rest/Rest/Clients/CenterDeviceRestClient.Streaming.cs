using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    public abstract partial class CenterDeviceRestClient
    {
        private static readonly HttpClient ResourceDownloadTransport = new HttpClient(
            new CenterDeviceHttpMessageHandler(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }, retryReads: false))
        { Timeout = Timeout.InfiniteTimeSpan };

        /// <summary>Sends a resource download without buffering its body or following redirects.</summary>
        /// <param name="request">The authorized resource request.</param>
        /// <param name="cancellationToken">Cancels shared queue admission and active HTTP I/O.</param>
        /// <returns>The response whose body remains owned by the caller.</returns>
        /// <remarks>Uses the shared origin admission policy. Overrides must preserve response ownership and must not automatically replay requests or forward authorization to redirects.</remarks>
        protected virtual Task<HttpResponseMessage> SendResourceDownloadRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => ResourceDownloadTransport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        internal async Task<Stream> OpenResourceDownloadAsync(string userId, Func<HttpRequestMessage> createRequest,
            HttpStatusCode expected, string[] mediaTypes, bool generation, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var authorization = await GetOAuthInfoAsync(userId, token).ConfigureAwait(false);
            for (var attempt = 0; ; attempt++)
            {
                HttpResponseMessage response = null;
                CancellationTokenRegistration registration = default(CancellationTokenRegistration);
                try
                {
                    Uri uri;
                    long? rangeStart;
                    using (var request = createRequest())
                    {
                        uri = request.RequestUri;
                        rangeStart = request.Headers.Range?.Ranges.Single().From;
                        request.Headers.TryAddWithoutValidation(AUTHORIZATION, GetAuthorizationBearer(authorization));
                        request.Headers.TryAddWithoutValidation("User-Agent", CustomOptionUserAgent);
                        response = await SendResourceDownloadRequestAsync(request, token).ConfigureAwait(false);
                    }
                    registration = token.Register(response.Dispose);
                    token.ThrowIfCancellationRequested();
                    if (generation && (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.TemporaryRedirect))
                        throw new DocumentRepresentationNotReadyException(response.StatusCode,
                            response.StatusCode == HttpStatusCode.TemporaryRedirect ? GetRecommendedGenerationWait(uri, response.Headers.Location) : null);
                    if (response.StatusCode != expected)
                    {
                        var content = await ReadResourceErrorAsync(response.Content, token).ConfigureAwait(false);
                        if (attempt == 0 && response.StatusCode == HttpStatusCode.Unauthorized &&
                            (content == "Unknown or expired token" || content == "Tenant has expired"))
                        {
                            // Release the response's origin allowance before refreshing via the same origin.
                            registration.Dispose();
                            response.Dispose();
                            response = null;
                            var replacement = await RefreshAuthorizationAsync(authorization, token).ConfigureAwait(false);
                            if (replacement != null) { authorization = replacement; continue; }
                        }
                        var status = response?.StatusCode ?? HttpStatusCode.Unauthorized;
                        if ((int)status == TOO_MANY_REQUESTS)
                            throw new TooManyRequestsException(ExtractDelay(response.Headers.RetryAfter?.ToString()));
                        if (status == HttpStatusCode.NotAcceptable) throw new NotAcceptableException(content);
                        if (status == HttpStatusCode.RequestedRangeNotSatisfiable)
                            throw new RequestedRangeNotSatisfiableException("The requested range cannot be downloaded.");
                        throw RestClientExceptionUtils.CreateDefaultException(new List<HttpStatusCode> { expected }, status, content, null);
                    }
                    var mediaType = response.Content?.Headers.ContentType?.MediaType;
                    if (mediaType == null || !mediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
                        throw new InvalidResponseDataException("The download returned an unexpected media type.",
                            new InvalidDataException("Expected " + string.Join(", ", mediaTypes) + "; received " + (mediaType ?? "no Content-Type") + "."));
                    if (rangeStart.HasValue && (response.Content.Headers.ContentRange?.HasRange != true ||
                        response.Content.Headers.ContentRange?.Unit != "bytes" || response.Content.Headers.ContentRange?.From != rangeStart))
                        throw new InvalidResponseDataException("The download returned an unexpected byte range.",
                            new InvalidDataException("Content-Range must start at the requested byte offset."));
                    var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    var owned = new ResponseDownloadStream(body, response, registration, token);
                    response = null;
                    registration = default(CancellationTokenRegistration);
                    return owned;
                }
                catch (Exception) when (token.IsCancellationRequested)
                {
                    registration.Dispose();
                    response?.Dispose();
                    throw new OperationCanceledException(token);
                }
                catch
                {
                    registration.Dispose();
                    response?.Dispose();
                    throw;
                }
            }
        }

        private static async Task<string> ReadResourceErrorAsync(HttpContent content, CancellationToken token)
        {
            if (content == null) return null;
            var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
            {
                var buffer = new char[1024];
                var result = new StringBuilder();
                while (result.Length < 16384)
                {
                    token.ThrowIfCancellationRequested();
                    var read = await reader.ReadAsync(buffer, 0, Math.Min(buffer.Length, 16384 - result.Length)).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (read == 0) break;
                    result.Append(buffer, 0, read);
                }
                return result.ToString();
            }
        }

        private static int? GetRecommendedGenerationWait(Uri requestUri, Uri location)
        {
            if (location == null || !Uri.TryCreate(requestUri, location, out var uri) ||
                uri.Scheme != requestUri.Scheme || uri.IdnHost != requestUri.IdnHost || uri.Port != requestUri.Port ||
                uri.AbsolutePath != requestUri.AbsolutePath || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0) return null;
            int? result = null;
            foreach (var part in uri.Query.TrimStart('?').Split('&'))
            {
                var pair = part.Split(new[] { '=' }, 2);
                if (Uri.UnescapeDataString(pair[0]) != "wait-for-generation") continue;
                if (result.HasValue || pair.Length != 2 || !int.TryParse(Uri.UnescapeDataString(pair[1]),
                    NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds < 1 || seconds > 30) return null;
                result = seconds;
            }
            return result;
        }
    }
}
