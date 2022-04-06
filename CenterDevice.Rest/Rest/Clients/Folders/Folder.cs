using CenterDevice.Model.Folder;
using CenterDevice.Rest.Clients.Common;
using RestSharp.Deserializers;
using System.Collections.Generic;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Folders
{
    public class Folder : IFolderBaseData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Parent { get; set; }

        public string Collection { get; set; }

        [DeserializeAs(Name = RestApiConstants.PATH)]
        public List<string> Path { get; set; }

        public List<Folder> SubFolders { get; set; }
        [DeserializeAs(Name = RestApiConstants.HAS_SUBFOLDERS)]
        private bool? HasSubFoldersServerInfo { get; set; }
        public bool? HasSubFolders
        {
            get
            {
                if (this.SubFolders != null)
                    return (this.SubFolders.Count != 0);
                else if (this.HasSubFoldersServerInfo.HasValue)
                    return this.HasSubFoldersServerInfo.Value;
                else //if ((!this.HasSubFoldersServerInfo.HasValue) && (this.SubFolders == null))
                    return null;
            }
        }

        public List<CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata> Documents;

        public string Link { get; set; }

        public Sharings Users { get; set; }
        public Sharings Groups { get; set; }

        public bool IsRootFolder
        {
            get
            {
                return Parent == RestApiConstants.NONE;
            }
        }

        public bool IsShared
        {
            get
            {
                return (Users != null && Users.HasSharing) || (Groups != null && Groups.HasSharing) || Link != null;
            }
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element