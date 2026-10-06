using System;
using System.Net.Http;

namespace CenterDevice.Rest.Clients
{
    /// <summary>Creates HTTP transports that share the CenterDevice request policy with REST and download clients.</summary>
    public static class CenterDeviceHttpTransport
    {
        /// <summary>Creates an HTTP client with shared request admission, cooldowns, and bounded read retries.</summary>
        /// <param name="innerHandler">The configured transport handler. The returned client owns and disposes this handler.</param>
        /// <returns>A caller-owned HTTP client sharing one active request and thirty starts per rolling minute per origin in this process.</returns>
        /// <remarks>These are conservative client defaults, not verified server quotas. Response bodies retain admission until consumed or disposed. Writes are never automatically replayed. Configure authorization and account clients with this transport before their first request to share the same allowance.</remarks>
        /// <exception cref="ArgumentNullException">The transport handler is null.</exception>
        public static HttpClient CreateHttpClient(HttpMessageHandler innerHandler)
        {
            if (innerHandler == null) throw new ArgumentNullException(nameof(innerHandler));
            return new HttpClient(new CenterDeviceHttpMessageHandler(innerHandler));
        }
    }
}
