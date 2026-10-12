using CenterDevice.Rest;
using CenterDevice.Rest.Clients.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    public class CenterDeviceErrorHandler : CenterDevice.Rest.Clients.IRestClientErrorHandler
    {
        /// <summary>
        /// Configuration for accessing CenterDevice API
        /// </summary>
        public CenterDeviceErrorHandler(string loginEMailAddress, CenterDevice.Rest.Clients.OAuth.IOAuthInfoProvider oAuthProvider)
        {
            this.OAuthProvider = oAuthProvider;
            this.LoginEMailAddress = loginEMailAddress;
        }

        public CenterDevice.Rest.Clients.OAuth.IOAuthInfoProvider OAuthProvider { get; set; }
        public string LoginEMailAddress { get; set; }

        public OAuthInfo RefreshToken(OAuthInfo oAuthInfo)
        {
            return new OAuthInfo(this.LoginEMailAddress, this.OAuthProvider.GetOAuthInfo(this.LoginEMailAddress));
        }

        public void ValidateResponse(global::RestSharp.IRestResponse result)
        {
            if (result.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
                throw new System.Net.WebException("Server error", System.Net.WebExceptionStatus.UnknownError);
        }

        ///// <summary>
        ///// Configuration for accessing CenterDevice API
        ///// </summary>
        //public CenterDeviceErrorHandler(string loginEMailAddress, CenterDeviceOAuthInfoProvider oAuthProvider)
        //{
        //    this.OAuthProvider = oAuthProvider;
        //    this.LoginEMailAddress = loginEMailAddress;
        //}

        //public CenterDeviceOAuthInfoProvider OAuthProvider { get; set; }
        //public string LoginEMailAddress { get; set; }

        //public OAuthInfo RefreshToken(OAuthInfo oAuthInfo)
        //{
        //    return new OAuthInfo(this.LoginEMailAddress, this.OAuthProvider.CenterDeviceClient.Token);
        //}

        //public void ValidateResponse(global::RestSharp.IRestResponse result)
        //{
        //    if (result.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
        //        throw new System.Net.WebException("Server error", System.Net.WebExceptionStatus.UnknownError);
        //}
    }
}