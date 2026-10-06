using CenterDevice.Rest.Clients.OAuth;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    /// <summary>Provides optional asynchronous authorization refresh without changing existing error-handler implementations.</summary>
    public interface IAsyncRestClientErrorHandler : IRestClientErrorHandler
    {
        /// <summary>Refreshes authorization after an explicitly rejected expired token.</summary>
        /// <param name="oAuthInfo">The rejected authorization information.</param>
        /// <param name="cancellationToken">Cancels the refresh operation.</param>
        /// <returns>The replacement authorization information, or null if refresh is unavailable.</returns>
        Task<OAuthInfo> RefreshTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken = default(CancellationToken));
    }
}
