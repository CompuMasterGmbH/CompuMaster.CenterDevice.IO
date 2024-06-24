using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients
{
    /// <summary>
    /// CenterDeviceClientBase with implementations for low level API access to CenterDevice services
    /// </summary>
    public abstract class CenterDeviceClientBase
    {
        /// <summary>
        /// Version of the CenterDevice client
        /// </summary>
        public static readonly Version Version = new System.Reflection.AssemblyName(typeof(CenterDeviceClientBase).Assembly.FullName).Version;

        internal protected readonly IRestClientConfiguration configuration;
        internal protected readonly IOAuthInfoProvider oAuthInfoProvider;
        internal protected readonly IRestClientErrorHandler errorHandler;
        protected readonly string apiVersionPrefix;

        protected CenterDeviceClientBase(IOAuthInfoProvider oAuthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
        {
            this.errorHandler = errorHandler;
            this.oAuthInfoProvider = oAuthInfoProvider;
            this.configuration = configuration;
            this.apiVersionPrefix = apiVersionPrefix;
        }

        IStreamWrapper defaultStreamWrapper;
        /// <summary>
        /// The stream wrapper in use for document downloads/uploads
        /// </summary>
        /// <returns></returns>
        public virtual IStreamWrapper DefaultStreamWrapper()
        {
            if (defaultStreamWrapper == null)
            {
                defaultStreamWrapper = new CenterDeviceStreamWrapper();
            }
            return defaultStreamWrapper;
        }

        protected abstract string UploadLinkBaseUrl { get; }

        public CenterDevice.Rest.Clients.Folders.FolderRestClient Folder => new CenterDevice.Rest.Clients.Folders.FolderRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Folders.FoldersRestClient Folders => new CenterDevice.Rest.Clients.Folders.FoldersRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Collections.CollectionRestClient Collection => new CenterDevice.Rest.Clients.Collections.CollectionRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Collections.CollectionsRestClient Collections => new CenterDevice.Rest.Clients.Collections.CollectionsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Documents.DocumentsRestClient Documents => new CenterDevice.Rest.Clients.Documents.DocumentsRestClient(oAuthInfoProvider, configuration, errorHandler, this.DefaultStreamWrapper(), apiVersionPrefix);
        public CenterDevice.Rest.Clients.Documents.DocumentRestClient Document => new CenterDevice.Rest.Clients.Documents.DocumentRestClient(oAuthInfoProvider, configuration, errorHandler, this.DefaultStreamWrapper(), apiVersionPrefix);
        public CenterDevice.Rest.Clients.Groups.GroupRestClient Group => new CenterDevice.Rest.Clients.Groups.GroupRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Groups.GroupsRestClient Groups => new CenterDevice.Rest.Clients.Groups.GroupsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Link.LinkRestClient Link => new CenterDevice.Rest.Clients.Link.LinkRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Link.LinksRestClient Links => new CenterDevice.Rest.Clients.Link.LinksRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Link.UploadLinkRestClient UploadLink => new CenterDevice.Rest.Clients.Link.UploadLinkRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix, UploadLinkBaseUrl);
        public CenterDevice.Rest.Clients.Link.UploadLinksRestClient UploadLinks => new CenterDevice.Rest.Clients.Link.UploadLinksRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix, UploadLinkBaseUrl);
        public CenterDevice.Rest.Clients.User.UserRestClient User => new CenterDevice.Rest.Clients.User.UserRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.User.UsersRestClient Users => new CenterDevice.Rest.Clients.User.UsersRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.User.UserSettingsRestClient UserSettings => new CenterDevice.Rest.Clients.User.UserSettingsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Timeline.TimelineRestClient Timeline => new CenterDevice.Rest.Clients.Timeline.TimelineRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Tenant.TenantFeaturesRestClient TenantFeatures => new CenterDevice.Rest.Clients.Tenant.TenantFeaturesRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Tenant.TenantRestClient Tenant => new CenterDevice.Rest.Clients.Tenant.TenantRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Tenant.TenantSettingsRestClient TenantSettings => new CenterDevice.Rest.Clients.Tenant.TenantSettingsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public CenterDevice.Rest.Clients.Tenant.TenantsRestClient Tenants => new CenterDevice.Rest.Clients.Tenant.TenantsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element