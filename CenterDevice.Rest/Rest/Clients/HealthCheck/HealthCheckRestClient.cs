using RestSharp;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.HealthCheck
{
    public class HealthCheckRestClient : CenterDeviceRestClient, IHealthCheckRestClient
    {
        private const string URI_RESOURCE = "healthcheck";

        public HealthCheckRestClient(IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler) : base(null, configuration, errorHandler, null) { }

        public bool IsConnectionWorking(bool useDefaultProxy, string userName, string password)
        {
            return IsConnectionWorkingAsync(useDefaultProxy, userName, password).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>Checks connectivity using asynchronous HTTP I/O and the selected proxy settings.</summary>
        /// <param name="useDefaultProxy">Selects the system proxy when true.</param>
        /// <param name="userName">The proxy user name, or null for default credentials.</param>
        /// <param name="password">The proxy password, or null for default credentials.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>True when the health endpoint returns HTTP 200.</returns>
        public async Task<bool> IsConnectionWorkingAsync(bool useDefaultProxy, string userName, string password,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = new RestClientOptions(CustomOptionBaseAddress)
            {
                UserAgent = this.CustomOptionUserAgent,
                ConfigureMessageHandler = handler => new CenterDeviceHttpMessageHandler(handler)
            };

            if (useDefaultProxy)
            {
                options.Proxy = WebRequest.GetSystemWebProxy();
            }
            else
            {
                options.Proxy = null;
            }

            if (options.Proxy != null)
            {
                if (userName != null && password != null)
                {
                    options.Proxy.Credentials = new NetworkCredential(userName, password);
                }
                else
                {
                    options.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                }
            }

            using (var testClient = new RestClient(options))
            {
                var response = await testClient.ExecuteAsync(CreateRestRequest(URI_RESOURCE, Method.Get), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return response.StatusCode == HttpStatusCode.OK;
            }
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
