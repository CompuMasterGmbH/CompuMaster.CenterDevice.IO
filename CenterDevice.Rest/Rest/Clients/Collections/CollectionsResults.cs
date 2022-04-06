using CenterDevice.Model.Collection;
using CenterDevice.Rest.Clients.Common;
using RestSharp.Deserializers;
using System;
using System.Collections.Generic;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Collections
{
    public class CollectionsResults
    {
        public List<Collection> Collections { get; set; }
    }

    public class Collection : CollectionBaseData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Link { get; set; }
        public string UploadLink { get; set; }
        public bool Public { get; set; }
        [DeserializeAs(Name = RestApiConstants.FILTER_PARAMS)]
        public object FilterParameters { get; set; }
        public Sharings Users { get; set; }
        public Sharings Groups { get; set; }
        public bool Auditing { get; set; }
        [DeserializeAs(Name = RestApiConstants.NEED_TO_OPT_IN)]
        public bool? NeedToOptIn { get; set; }

        public List<Folders.Folder> SubFolders { get; set; }
        [DeserializeAs(Name = RestApiConstants.HAS_FOLDERS)]
        public bool? HasFoldersServerInfo { get; set; }
        [DeserializeAs(Name = RestApiConstants.HAS_SUBFOLDERS)]
        public bool? HasFolders
        {
            get
            {
                if (this.SubFolders != null)
                    return (this.SubFolders.Count != 0);
                else if (this.HasFoldersServerInfo.HasValue)
                    return this.HasFoldersServerInfo.Value;
                else //if ((!this.HasFoldersServerInfo.HasValue) && (this.SubFolders == null))
                    return null;
            }
        }

        public bool? HasDocuments
        {
            get
            {
                if (this.Documents != null)
                    return (this.Documents.Count != 0);
                else //if (this.Documents == null)
                    return null;
            }
        }
        public List<CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata> Documents;

        [DeserializeAs(Name = RestApiConstants.ARCHIVED_DATE)]
        public DateTime? ArchivedDate { get; set; }
        [DeserializeAs(Name = RestApiConstants.OWNER)]
        public string Owner { get; set; }

        public bool IsShared
        {
            get
            {
                return Public || HasLink || HasUploadLink || (Users != null && Users.HasSharing) || (Groups != null && Groups.HasSharing);
            }
        }

        public bool IsIntelligent
        {
            get
            {
                return FilterParameters != null;
            }
        }

        public bool HasLink
        {
            get
            {
                return Link != null;
            }
        }

        public bool HasUploadLink
        {
            get
            {
                return UploadLink != null;
            }
        }

        public bool Archived => ArchivedDate != null;
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element