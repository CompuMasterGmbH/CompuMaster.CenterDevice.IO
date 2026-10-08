using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients
{
    /// <summary>
    /// CenterDeviceClient for low level API access to CenterDevice services
    /// </summary>
    public class CenterDeviceClient : CenterDeviceClientBase
    {
        public CenterDeviceClient() : this(null, CenterDevice.Rest.Clients.CenterDeviceConfiguration.Default)
        {
        }

        public CenterDeviceClient(IOAuthInfoProvider oAuthInfoProvider, CenterDevice.Rest.Clients.CenterDeviceConfiguration configuration) : this(oAuthInfoProvider, configuration.OAuthConfiguration())
        {
            this.Config = configuration;
        }

        private CenterDeviceClient(IOAuthInfoProvider oAuthInfoProvider, IRestClientConfiguration configuration) : this(oAuthInfoProvider, configuration, new CenterDevice.Rest.Clients.CenterDeviceErrorHandler(null, oAuthInfoProvider))
        {
        }

        private CenterDeviceClient(IOAuthInfoProvider oAuthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler) : base(oAuthInfoProvider, configuration, errorHandler, "v2/")
        {
        }

        public CenterDevice.Rest.Clients.HealthCheck.HealthCheckRestClient HealthCheck => new CenterDevice.Rest.Clients.HealthCheck.HealthCheckRestClient(configuration, errorHandler);

        protected override string UploadLinkBaseUrl => this.Config.UploadLinkBaseUrl;

        /// <summary>
        /// Configuration for accessing CenterDevice API
        /// </summary>
        public CenterDevice.Rest.Clients.CenterDeviceConfiguration Config { get; set; }

        /// <summary>
        /// Client token for accessing CenterDevice API
        /// </summary>
        public CenterDevice.Rest.Clients.OAuth.OAuthInfo Token { get; set; }

        /// <summary>
        /// Authorize user with username, customer number, organisation name and password as set up in configuration
        /// </summary>
        public void AuthorizeWithToken(CenterDevice.Rest.Clients.OAuth.OAuthInfo token)
        {
            this.Token = token;
        }

        /// <summary>
        /// Authorize user with username, customer number, organisation name and password as set up in configuration
        /// </summary>
        public void AuthorizeWithUserCredentials()
        {
            //this.Token = this.AuthorizationApi.TokenWithHttpInfo("password", this.Config.ClientNumber, null, null, this.Config.Username, null, this.Config.OrganisationName, this.Config.Password, null, null, null, null).Data;
            //this.Token = this.AuthorizationApi.TokenWithHttpInfo("password", null, null, null, this.Config.Username, null, null, this.Config.Password, null, null, null, null).Data;
        }

        /// <summary>
        /// Update configuration and authorize user
        /// </summary>
        /// <param name="username"></param>
        /// <param name="customerNo"></param>
        /// <param name="password"></param>
        public void AuthorizeWithUserCredentials(string username, string password)
        //public void AuthorizeWithUserCredentials(string username, string customerNo, string password)
        {
            this.Config.Username = username;
            //this.Config.ClientNumber = customerNo;
            this.Config.Password = password;
            this.AuthorizeWithUserCredentials();
        }

        /// <summary>
        /// Update configuration and authorize user
        /// </summary>
        /// <param name="username"></param>
        /// <param name="customerNo"></param>
        /// <param name="organisationName"></param>
        /// <param name="password"></param>
        public void AuthorizeWithUserCredentials(string username, string organisationName, string password)
        //public void AuthorizeWithUserCredentials(string username, string customerNo, string organisationName, string password)
        {
            this.Config.Username = username;
            //this.Config.ClientNumber = customerNo;
            //this.Config.OrganisationName = organisationName;
            this.Config.Password = password;
            this.AuthorizeWithUserCredentials();
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element