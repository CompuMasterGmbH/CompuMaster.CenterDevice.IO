using CenterDevice.Rest.Clients.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.OAuth
{
    public class OAuthEndpointConfiguration : CenterDevice.Rest.RestClientConfiguration, CenterDevice.Rest.Clients.OAuth.IOAuthClientConfiguration
    {
        /// <summary>
        /// Default configuration for accessing the token endpoint of CenterDevice API
        /// </summary>
        public OAuthEndpointConfiguration(string userAgent) : base(userAgent)
        {
        }

        /// <summary>
        /// Custom configuration for accessing the token endpoint of CenterDevice API
        /// </summary>
        public OAuthEndpointConfiguration(string baseAddress, string userAgent) : base(baseAddress, userAgent)
        {
        }

        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string RedirectUri { get; set; }

        string IOAuthClientConfiguration.ClientId => this.ClientId;

        string IOAuthClientConfiguration.ClientSecret => this.ClientSecret;

        string IOAuthClientConfiguration.RedirectUri => this.RedirectUri;

        string IRestClientConfiguration.BaseAddress => this.BaseAddress;

        string IRestClientConfiguration.UserAgent => this.UserAgent;
    }
}
