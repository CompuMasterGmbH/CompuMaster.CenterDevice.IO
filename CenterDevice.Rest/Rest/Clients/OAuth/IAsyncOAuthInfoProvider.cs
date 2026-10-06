using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.OAuth
{
    /// <summary>Provides optional asynchronous authorization lookup without changing existing provider implementations.</summary>
    public interface IAsyncOAuthInfoProvider : IOAuthInfoProvider
    {
        /// <summary>Retrieves authorization for an existing authentication context.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels the authorization lookup.</param>
        /// <returns>The authorization information for the requested context.</returns>
        Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken));
    }
}
