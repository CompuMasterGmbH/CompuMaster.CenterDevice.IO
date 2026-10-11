using System;
using System.Net;

namespace CenterDevice.Rest.Exceptions
{
    /// <summary>Indicates that a supported document representation is not available yet.</summary>
    /// <remarks>Does not mean an empty successful representation. No automatic retry is performed; the caller decides whether and when to request generation again.</remarks>
    public class DocumentRepresentationNotReadyException : RestClientException
    {
        /// <summary>Initializes a representation-generation result.</summary>
        /// <param name="statusCode">NoContent after a generation wait, or TemporaryRedirect suggesting a wait.</param>
        /// <param name="recommendedWaitSeconds">A validated same-resource generation wait between 1 and 30 seconds, or null when unavailable.</param>
        /// <exception cref="ArgumentOutOfRangeException">The status or recommended wait is outside the supported contract.</exception>
        public DocumentRepresentationNotReadyException(HttpStatusCode statusCode, int? recommendedWaitSeconds = null)
            : base("The document representation is not available yet.")
        {
            if (statusCode != HttpStatusCode.NoContent && statusCode != HttpStatusCode.TemporaryRedirect)
                throw new ArgumentOutOfRangeException(nameof(statusCode));
            if (recommendedWaitSeconds < 1 || recommendedWaitSeconds > 30)
                throw new ArgumentOutOfRangeException(nameof(recommendedWaitSeconds));
            StatusCode = statusCode;
            RecommendedWaitSeconds = recommendedWaitSeconds;
        }

        /// <summary>Gets the generation response status.</summary>
        public HttpStatusCode StatusCode { get; }
        /// <summary>Gets the validated suggested wait in seconds, or null when unavailable.</summary>
        public int? RecommendedWaitSeconds { get; }
    }
}
