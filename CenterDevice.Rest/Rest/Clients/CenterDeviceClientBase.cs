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

        /// <summary>Gets a client for the authenticated user's own trash.</summary>
        public Rest.Clients.Trash.TrashRestClient Trash => new Rest.Clients.Trash.TrashRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);

        /// <summary>Gets a client for the authenticated caller's statistics.</summary>
        public Rest.Clients.Statistics.StatisticsRestClient Statistics => new Rest.Clients.Statistics.StatisticsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);

        /// <summary>Gets a client for the authenticated caller's explicit search-term history.</summary>
        public Rest.Clients.SearchHistory.SearchHistoryRestClient SearchHistory => new Rest.Clients.SearchHistory.SearchHistoryRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);

        /// <summary>Gets a client for visible collection audit logs.</summary>
        public Rest.Clients.AuditLog.AuditLogRestClient AuditLog => new Rest.Clients.AuditLog.AuditLogRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);

        /// <summary>Gets a client for reminders created by the authenticated caller.</summary>
        public Rest.Clients.Reminders.RemindersRestClient Reminders => new Rest.Clients.Reminders.RemindersRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);

        /// <summary>Gets a client for rules created by the authenticated caller.</summary>
        public Rest.Clients.Rules.RulesRestClient Rules => new Rest.Clients.Rules.RulesRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);

        /// <summary>Gets a client for upload-email endpoints created by the authenticated caller.</summary>
        public Rest.Clients.UploadEmails.UploadEmailsRestClient UploadEmails => new Rest.Clients.UploadEmails.UploadEmailsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);

        /// <summary>Gets a client for SFTP upload endpoints created by the authenticated caller.</summary>
        public Rest.Clients.UploadSftps.UploadSftpsRestClient UploadSftps => new Rest.Clients.UploadSftps.UploadSftpsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);

        /// <summary>Gets a client for visible metadata templates and their sharing contracts.</summary>
        public Rest.Clients.MetadataTemplates.MetadataTemplatesRestClient MetadataTemplates => new Rest.Clients.MetadataTemplates.MetadataTemplatesRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);

        /// <summary>Gets a client for own workflow-template management and explicit template use.</summary>
        public Rest.Clients.WorkflowTemplates.WorkflowTemplatesRestClient WorkflowTemplates => new Rest.Clients.WorkflowTemplates.WorkflowTemplatesRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);

        public Rest.Clients.Folders.FolderRestClient Folder => new Rest.Clients.Folders.FolderRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Folders.FoldersRestClient Folders => new Rest.Clients.Folders.FoldersRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Collections.CollectionRestClient Collection => new Rest.Clients.Collections.CollectionRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Collections.CollectionsRestClient Collections => new Rest.Clients.Collections.CollectionsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Documents.DocumentsRestClient Documents => new Rest.Clients.Documents.DocumentsRestClient(oAuthInfoProvider, configuration, errorHandler, this.DefaultStreamWrapper(), apiVersionPrefix);
        public Rest.Clients.Documents.DocumentRestClient Document => new Rest.Clients.Documents.DocumentRestClient(oAuthInfoProvider, configuration, errorHandler, this.DefaultStreamWrapper(), apiVersionPrefix);
        public Rest.Clients.Groups.GroupRestClient Group => new Rest.Clients.Groups.GroupRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Groups.GroupsRestClient Groups => new Rest.Clients.Groups.GroupsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Link.LinkRestClient Link => new Rest.Clients.Link.LinkRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Link.LinksRestClient Links => new Rest.Clients.Link.LinksRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Link.UploadLinkRestClient UploadLink => new Rest.Clients.Link.UploadLinkRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix, UploadLinkBaseUrl);
        public Rest.Clients.Link.UploadLinksRestClient UploadLinks => new Rest.Clients.Link.UploadLinksRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix, UploadLinkBaseUrl);
        public Rest.Clients.User.UserRestClient User => new Rest.Clients.User.UserRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.User.UsersRestClient Users => new Rest.Clients.User.UsersRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.User.UserSettingsRestClient UserSettings => new Rest.Clients.User.UserSettingsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Timeline.TimelineRestClient Timeline => new Rest.Clients.Timeline.TimelineRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Tenant.TenantFeaturesRestClient TenantFeatures => new Rest.Clients.Tenant.TenantFeaturesRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Tenant.TenantRestClient Tenant => new Rest.Clients.Tenant.TenantRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Tenant.TenantSettingsRestClient TenantSettings => new Rest.Clients.Tenant.TenantSettingsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
        public Rest.Clients.Tenant.TenantsRestClient Tenants => new Rest.Clients.Tenant.TenantsRestClient(oAuthInfoProvider, configuration, errorHandler, apiVersionPrefix);
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
