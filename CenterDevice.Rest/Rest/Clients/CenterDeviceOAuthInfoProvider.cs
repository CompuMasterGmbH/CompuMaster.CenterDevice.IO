using CenterDevice.Rest;
using CenterDevice.Rest.Clients.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    public class CenterDeviceOAuthInfoProvider : CenterDevice.Rest.Clients.OAuth.IOAuthInfoProvider
    {
        /// <summary>
        /// Configuration for accessing CenterDevice API
        /// </summary>
        public CenterDeviceOAuthInfoProvider()
        {
        }

        /// <summary>
        /// Configuration for accessing CenterDevice API
        /// </summary>
        public CenterDeviceOAuthInfoProvider(CenterDevice.Rest.Clients.CenterDeviceClient centerDeviceClient)
        {
            this.CenterDeviceClient = centerDeviceClient;
        }

        public CenterDevice.Rest.Clients.CenterDeviceClient CenterDeviceClient { get; set; }

        private OAuthInfo getOAuthInfo = null;
        public OAuthInfo GetOAuthInfo(string userId)
        {
            if ((getOAuthInfo == null) || (getOAuthInfo.UserId != userId))
                this.getOAuthInfo = new CenterDevice.Rest.Clients.OAuth.OAuthInfo(CenterDeviceClient.Config.Username, CenterDeviceClient.Token);
            return this.getOAuthInfo;
        }
    }
}