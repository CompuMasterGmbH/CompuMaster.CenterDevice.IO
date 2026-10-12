using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients
{
    public sealed class DiagnosticTools
    {
        static void PrintCollections(List<CenterDevice.Rest.Clients.Collections.Collection> Collections)
        {
            System.Console.WriteLine("TeamworkCollections={");
            {
                var sb = new StringBuilder();
                foreach (var Collection in Collections)
                {
                    if (Collections.IndexOf(Collection) > 0)
                        sb.AppendLine();
                    sb.AppendLine("Collection[" + (Collections.IndexOf(Collection) + 1) + "/" + Collections.Count + "]=" + Collection.Name);
                    sb.AppendLine("  Collection.ID=" + Collection.Id);
                    sb.AppendLine("  Collection.Name=" + Collection.Name);
                    sb.AppendLine("  Collection.Owner=" + Collection.Owner);
                    sb.AppendLine("  Collection.HasLink=" + Collection.HasLink);
                    sb.AppendLine("  Collection.Link=" + Collection.Link);
                    sb.AppendLine("  Collection.IsIntelligent=" + Collection.IsIntelligent);
                    sb.AppendLine("  Collection.Archived=" + Collection.Archived);
                    sb.AppendLine("  Collection.ArchivedDate=" + Collection.ArchivedDate);
                    sb.AppendLine("  Collection.Auditing=" + Collection.Auditing);
                    sb.AppendLine("  Collection.NeedToOptIn=" + Collection.NeedToOptIn.ToString("[?]"));
                    sb.AppendLine("  Collection.Public=" + Collection.Public);
                    sb.AppendLine("  Collection.Shared=" + Collection.IsShared);

                    sb.AppendLine("  Collection.HasFolders=" + Collection.HasFolders.ToString("[?]"));
                    StringBuilder SubFolders = PrintSubFolders(Collection.SubFolders);
                    if (SubFolders.Length != 0)
                    {
                        sb.AppendLine("  Collection.SubFolders={");
                        sb.AppendLine(SubFolders.ToString().Indent());
                        sb.AppendLine("  }");
                    }
                    else
                        sb.AppendLine("  Collection.SubFolders={}");

                    sb.AppendLine("  Collection.HasDocuments=" + Collection.HasDocuments.ToString("[?]"));
                    StringBuilder Documents = PrintFiles(Collection.Documents);
                    if (SubFolders.Length != 0)
                    {
                        sb.AppendLine("  Collection.Files={");
                        sb.AppendLine(Documents.ToString().Indent());
                        sb.AppendLine("  }");
                    }
                    else
                        sb.AppendLine("  Collection.Files={}");
                }
                System.Console.Write(sb.ToString().Indent());
            }
            System.Console.WriteLine("}");
            System.Console.WriteLine();
        }

        static StringBuilder PrintFolders(CenterDevice.Rest.Clients.Collections.Collection collection)
        {
            var result = new StringBuilder();
            if ((collection.SubFolders != null) && (collection.SubFolders.Count != 0))
            {
                result.Append("TeamworkFolders[Collection:" + collection.Id + "]=" + collection.Name);
                result.Append(PrintSubFolders(collection.SubFolders).ToString());
            }
            PrintFiles(collection.Documents);
            return result;
        }

        static StringBuilder PrintSubFolders(CenterDevice.Rest.Clients.Folders.Folder parentFolder)
        {
            var result = new StringBuilder();
            if ((parentFolder.SubFolders != null) && (parentFolder.SubFolders.Count != 0))
            {
                result.Append("TeamworkFolders[Folder:" + parentFolder.Id + "]=" + parentFolder.Name);
                result.Append(PrintSubFolders(parentFolder.SubFolders).ToString());
            }
            return result;
        }

        private static StringBuilder PrintSubFolders(List<CenterDevice.Rest.Clients.Folders.Folder> Folders)
        {
            var result = new StringBuilder();
            var sb = new StringBuilder();
            result.AppendLine("{");
            foreach (var Folder in Folders)
            {
                if (Folders.IndexOf(Folder) > 0)
                    sb.AppendLine();
                sb.AppendLine("Folder[" + (Folders.IndexOf(Folder) + 1) + "/" + Folders.Count + "]=" + Folder.Name + "{");
                sb.AppendLine("  Folder.ID=" + Folder.Id);
                sb.AppendLine("  Folder.Name=" + Folder.Name);
                sb.AppendLine("  Folder.Link=" + Folder.Link);
                sb.AppendLine("  Folder.HasSubFolders=" + Folder.HasSubFolders.ToString("[?]"));
                sb.AppendLine("  Folder.IsRootFolder=" + Folder.IsRootFolder);
                sb.AppendLine("  Folder.Parent=" + Folder.Parent);
                if (Folder.Path != null)
                {
                    sb.AppendLine("  Folder.Path=" + string.Join<string>(@"\", Folder.Path));
                }
                else
                    sb.AppendLine("  Folder.Path=[]");
                sb.AppendLine("  Folder.Shared=" + Folder.IsShared);
                if (Folders.IndexOf(Folder) > 0)
                {
                    sb.AppendLine("  Folder.SubFolders=[?]");
                }
                else
                {
                    //Print sub folders
                    StringBuilder SubFolders = PrintSubFolders(Folder);
                    if (SubFolders.Length != 0)
                    {
                        sb.AppendLine("  Folder.SubFolders={");
                        sb.AppendLine(SubFolders.ToString().Indent());
                        sb.AppendLine("  }");
                    }
                    else
                        sb.AppendLine("  Folder.SubFolders={}");

                    //Print files
                    StringBuilder Files = PrintFiles(Folder.Documents);
                    if (SubFolders.Length != 0)
                    {
                        sb.AppendLine("  Folder.Files={");
                        sb.AppendLine(Files.ToString().Indent());
                        sb.AppendLine("  }");
                    }
                    else
                        sb.AppendLine("  Folder.Files={}");
                }
            }
            result.Append(sb.ToString().Indent());
            result.AppendLine("}");
            return result;
        }

        static StringBuilder PrintFiles(List<CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata> documents)
        {
            var result = new StringBuilder();
            if (documents != null)
            {
                var sb = new StringBuilder();
                result.AppendLine("{");
                foreach (var File in documents)
                {
                    sb.AppendLine(File.ToString());
                    if (documents.IndexOf(File) > 0)
                        sb.AppendLine();
                    sb.AppendLine("File[" + (documents.IndexOf(File) + 1) + "/" + documents.Count + "]=" + File.Filename + "{");
                    sb.AppendLine("  File.ID=" + File.Id);
                    sb.AppendLine("  File.Name=" + File.Filename);
                    sb.AppendLine("  File.Link=" + File.Link);
                    sb.AppendLine("  File.Size=" + File.Size);
                    sb.AppendLine("  File.Version=" + File.Version);
                    sb.AppendLine("  File.VersionDate=" + File.VersionDate);
                    sb.AppendLine("  File.Owner=" + File.Owner);
                    if (File.Folders != null)
                    {
                        sb.AppendLine("  File.Folders=" + string.Join<string>(@"\", File.Folders));
                    }
                    else
                        sb.AppendLine("  File.Folders=[]");
                    if (File.Collections != null && File.Collections.HasSharing)
                    {
                        sb.AppendLine("  File.Collections=" + string.Join<string>(@"|", File.Collections.Visible));
                    }
                    else
                        sb.AppendLine("  File.Collections=[]");
                    if (File.Locks != null)
                    {
                        sb.AppendLine("  File.Locks=" + string.Join<string>(@"|", File.Locks));
                    }
                    else
                        sb.AppendLine("  File.Locks=[]");
                    sb.AppendLine("  File.LockedBy=" + File.LockedBy);
                    sb.AppendLine("  File.DocumentDate=" + File.DocumentDate);
                    sb.AppendLine("  File.ArchivedDate=" + File.ArchivedDate);
                    sb.AppendLine("  File.UploadDate=" + File.UploadDate);
                    sb.AppendLine("  File.Uploader=" + File.Uploader);
                }
                result.Append(sb.ToString().Indent());
                result.AppendLine("}");
            }
            else
                result.AppendLine("Files=[]");
            return result;
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element