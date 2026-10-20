using System;
using System.Net.Http;

namespace CenterDevice.Rest.Clients
{
    /// <summary>Creates HTTP transports that share the CenterDevice request policy with REST and download clients.</summary>
    public static class CenterDeviceHttpTransport
    {
        /// <summary>Configures the optional request rate limit shared by clients targeting the same HTTP origin.</summary>
        /// <param name="serviceUri">An absolute HTTP or HTTPS URI. Only its scheme, host and port identify the shared allowance.</param>
        /// <param name="maxRequestsPerMinute">The maximum starts per rolling minute, or null to disable the fixed rate limit (the default).</param>
        /// <remarks>Configure this process-wide setting before the first request to the origin. Three concurrent requests and server-directed cooldowns remain effective with no fixed rate limit. Separate processes require their own coordination.</remarks>
        /// <exception cref="ArgumentNullException">The service URI is null.</exception>
        /// <exception cref="ArgumentException">The service URI is not an absolute HTTP or HTTPS URI.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The limit is less than one.</exception>
        /// <exception cref="InvalidOperationException">Requests to this origin have already started.</exception>
        public static void ConfigureRequestRateLimit(Uri serviceUri, int? maxRequestsPerMinute)
        {
            if (maxRequestsPerMinute.HasValue && maxRequestsPerMinute.Value < 1) throw new ArgumentOutOfRangeException(nameof(maxRequestsPerMinute));
            CenterDeviceHttpMessageHandler.GetAllowance(serviceUri).ConfigureRateLimit(maxRequestsPerMinute);
        }

        /// <summary>Creates an HTTP client with shared request admission, cooldowns, and bounded read retries.</summary>
        /// <param name="innerHandler">The configured transport handler. The returned client owns and disposes this handler.</param>
        /// <returns>A caller-owned HTTP client sharing up to three active requests per origin in this process, with no fixed minute quota by default.</returns>
        /// <remarks>These are conservative client defaults, not verified server quotas. Response bodies retain admission until consumed or disposed. Writes are never automatically replayed. Configure authorization and account clients with this transport before their first request to share the same allowance.</remarks>
        /// <exception cref="ArgumentNullException">The transport handler is null.</exception>
        public static HttpClient CreateHttpClient(HttpMessageHandler innerHandler)
        {
            if (innerHandler == null) throw new ArgumentNullException(nameof(innerHandler));
            return new HttpClient(new CenterDeviceHttpMessageHandler(innerHandler));
        }
    }
}
