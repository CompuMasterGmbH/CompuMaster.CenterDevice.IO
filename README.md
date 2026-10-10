# CompuMaster.CenterDevice.IO - the C# library for the CenterDevice file system

## CompuMaster.Dms ecosystem

These separately versioned repositories form the CompuMaster.Dms ecosystem; they are not Git submodules. Repository names, local directory names, and NuGet package IDs can differ.

| Component | Repository | Responsibility |
|---|---|---|
| CompuMaster.Dms | [CompuMaster.Dms](https://github.com/CompuMasterGmbH/CompuMaster.Dms) | Provider-independent DMS workflows, provider adapters, and BrowserUI. |
| CompuMaster.Ocs | [CompuMaster.OpenCollaborationService](https://github.com/CompuMasterGmbH/CompuMaster.OpenCollaborationService) | ownCloud/Nextcloud OCS protocol, sharing, and account/group operations. |
| CompuMaster.Scopevisio.OpenApi | [CompuMaster.Scopevisio.OpenApi](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.OpenApi) | Scopevisio OpenScope REST API and authorization. |
| CompuMaster.Scopevisio.Teamwork | [CompuMaster.Scopevisio.Teamwork](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.Teamwork) | Teamwork integration connecting OpenScope authorization with CenterDevice clients. |
| CompuMaster.CenterDevice | [CompuMaster.CenterDevice.IO](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO) | CenterDevice REST and file-system SDK; the DMS package dependency is CompuMaster.CenterDevice.Rest. |

DMS uses WebDAV for ownCloud/Nextcloud file operations and OCS for supported sharing operations. Teamwork builds on OpenScope and CenterDevice clients. Actual package versions and optional source references are defined by the project files on the branch being tested; an unmerged upstream change is not automatically available in DMS.

For cross-repository work, evaluate the affected libraries before adding a DMS-only workaround. Keep reusable protocol, authentication, transport, and SDK behavior in its owning library, while DMS retains the common workflow/capability model and UI mapping. Preserve standalone library consumers, synchronous APIs, identity semantics, and existing defaults.

Track each upstream change in an issue in its owning repository, link its implementing PR there, and reference that issue as a dependency in the affected DMS issue. Keep reciprocal links, exact commit/package versions, integration order, and verification status current. Distinguish implemented, tested, merged, published, and consumed states; do not close a dependency merely because an upstream branch is green.

Shared remote test systems require coordinated exclusive access across repositories and local sessions, including setup and cleanup. Identically named GitHub Actions concurrency groups in different repositories do not provide a shared lock. See [AGENTS.md](AGENTS.md) for working rules.

## Explicit upload dates

Timestamp-aware overloads of `DirectoryInfo.UploadAndCreateNewFileAsync` and
`FileInfo.UploadNewVersionAsync` accept a source modification date as the remote
`document-date`. Local dates are converted to UTC; dates without a time-zone kind
are interpreted as UTC. Matching synchronous local-file overloads are available.
The REST new-version API also accepts an explicit date. Existing overloads retain
their server-default date behavior, and `null` does not add a date to the metadata.
These dates describe the document; they do not replace server audit timestamps.

## Frameworks supported

* .NET 5.0 or later
* .NET Framework 4.8 or later
* .NET Standard 2.0 or later

## Clients supported

* Scopevisio Teamwork (OpenScope)
* MISSING: CenterDevice
  * the library already works for some 3rd party labels which indirectly involve the CenterDevice API.
  * as per today, the library DOES NOT work as a pure CenterDevice client since the CenterDevice API lacks an adequate authentification process (API allows regular login only as web login)

## Popular projects based on this project

* [CompuMaster.Dms](https://github.com/CompuMasterGmbH/CompuMaster.Dms/)
  *  an abstract DMS provider for multiple cloud systems aka DMS systems (WebDAV, OwnCloud, NextCloud, Scopevisio Teamwork, CenterDevice, or custom providers
  *  and additionally a Windows.Forms UI for accessing and managing files, folders and shares

## Console sample application for Scopevisio Teamwork

For accessing Scopevisio Teamwork, you can use above sample application, but use another IOClient for Teamwork:

```C#
string username = InputLine("username");
string customerno = InputLine("customer no.");
string password = InputLine("password");

//Authorize initially with user credentials -> recieved access token will be saved for following requests
OpenScopeApiClient OpenScopeClient = new OpenScopeApiClient();
OpenScopeClient.AuthorizeWithUserCredentials(username, customerno, password);

//Create teamwork rest client
//CompuMaster.Scopevisio.CenterDeviceApi.TeamworkRestClient TeamworkRestClient = new CompuMaster.Scopevisio.CenterDeviceApi.TeamworkRestClient(OpenScopeClient);

//Create IO client for CenterDevice or Scopevisio Teamwork 
CompuMaster.Scopevisio.CenterDeviceApi.TeamworkIOClient IOClient = new CompuMaster.Scopevisio.CenterDeviceApi.TeamworkIOClient(OpenScopeClient);
```

## Output of sample application (browse directory parts of above code only)
```text
## Initial directory listing - flat
/

## Initial directory listing - recursive - without display of files
/
    [Dirs:?]/

## Full directory listing - after GetDirectories(0)
/
    Test/
        [Dirs:?]/
        [Files:?]
    Neue Sammlung/
        [Dirs:?]/
        [Files:?]
    [Files:?]

## Full directory listing - after GetDirectories(1)
/
    Test/
        BWA/
            [Dirs:?]/
            [Files:?]
        DirLevel 01/
            [Dirs:?]/
            [Files:?]
        Summen- und Saldenliste/
            [Dirs:?]/
            [Files:?]
        test.txt.txt
    Neue Sammlung/
        SubDir/
            [Dirs:?]/
            [Files:?]
        test\/|?*:;,."_'@2.txt

## Full directory listing - after GetDirectories(2)
/
    Test/
        BWA/
            2019/
                [Dirs:?]/
                [Files:?]
            2020/
                [Dirs:?]/
                [Files:?]
            bwa.dir
            test.txt.txt
        DirLevel 01/
            DirLevel 02/
                [Dirs:?]/
                [Files:?]
        Summen- und Saldenliste/
            2019/
                [Dirs:?]/
                [Files:?]
            2020/
                [Dirs:?]/
                [Files:?]
            test.txt.txt
        test.txt.txt
    Neue Sammlung/
        SubDir/
            test.txt
        test\/|?*:;,."_'@2.txt

## Full directory listing - after GetDirectories(10)
/
    Test/
        BWA/
            2019/
                bwa.2019.dir.txt
                test.txt.txt
            2020/
                test.txt.txt
            bwa.dir
            test.txt.txt
        DirLevel 01/
            DirLevel 02/
                DirLevel 03/
                    DirLevel 04/
                        DirLevel 05/
                            DirLevel 06/
                                test\/|?*:;,."_'@2.txt
                            test.txt
        Summen- und Saldenliste/
            2019/
                test.txt.txt
            2020/
                test.txt.txt
            test.txt.txt
        test.txt.txt
    Neue Sammlung/
        SubDir/
            test.txt
        test\/|?*:;,."_'@2.txt

## Open directory path - / [Start: /]
FullName=/

## Open directory path - "" [Start: /]
FullName=/

## Open directory path - . [Start: /]
FullName=/

## Open directory path - Test/Summen- und Saldenliste\2020/ [Start: /]
FullName=/Test/Summen- und Saldenliste/2020

## Open file path - Test/Summen- und Saldenliste\2020/test.txt.txt [Start: /]
FullName=/Test/Summen- und Saldenliste/2020/test.txt.txt

## Open file path - test.txt.txt [Start: /Test/Summen- und Saldenliste/2020]
FullName=/Test/Summen- und Saldenliste/2020/test.txt.txt

## Open directory path - / [Start: /Test/Summen- und Saldenliste/2020]
FullName=/

## Open directory path - "" [Start: /Test/Summen- und Saldenliste/2020]
FullName=/Test/Summen- und Saldenliste/2020

## Open directory path - . [Start: /Test/Summen- und Saldenliste/2020]
FullName=/Test/Summen- und Saldenliste/2020

## Open directory path - .. [Start: /Test/Summen- und Saldenliste/2020]
FullName=/Test/Summen- und Saldenliste

## Open file path - test.txt.txt [Start: /Test/Summen- und Saldenliste/2020]
FullName=/Test/Summen- und Saldenliste/2020/test.txt.txt

## Open file path - ../test.txt.txt [Start: /Test/Summen- und Saldenliste/2020]
FullName=/Test/Summen- und Saldenliste/test.txt.txt

## Open file path - /Test/test.txt.txt [Start: /Test/Summen- und Saldenliste/2020]
FullName=/Test/test.txt.txt

## Open file path - /test.txt.txt [Start: /Test/Summen- und Saldenliste/2020]
Exception: System.IO.FileNotFoundException: File not found
Dateiname: "/test.txt.txt"
   bei CenterDevice.IO.DirectoryInfo.GetFile(String fileName) in D:\GitHub-OpenSource-Externals+Backups\bierdeckel-automation\CenterDevice\CenterDevice.Rest\IO\DirectoryInfo.cs:Zeile 279.
   bei CenterDevice.IO.DirectoryInfo.OpenFilePath(String[] directoryNames, String fileName) in D:\GitHub-OpenSource-Externals+Backups\bierdeckel-automation\CenterDevice\CenterDevice.Rest\IO\DirectoryInfo.cs:Zeile 223.
   bei CenterDevice.IO.DirectoryInfo.OpenFilePath(String[] directoryNamesWithFileName) in D:\GitHub-OpenSource-Externals+Backups\bierdeckel-automation\CenterDevice\CenterDevice.Rest\IO\DirectoryInfo.cs:Zeile 209.
   bei CenterDevice.IO.DirectoryInfo.OpenFilePath(String path) in D:\GitHub-OpenSource-Externals+Backups\bierdeckel-automation\CenterDevice\CenterDevice.Rest\IO\DirectoryInfo.cs:Zeile 194.
   bei CenterDevice.SampleApp.Program.Main() in D:\GitHub-OpenSource-Externals+Backups\bierdeckel-automation\CenterDevice\CenterDevice.SampleApp\Program.cs:Zeile 139.
```

## Console sample application for CenterDevice - NOT WORKING AS PURE CONSOLE APP - REQUIRES WEB LOGIN

```csharp
using CompuMaster.Scopevisio.OpenApi;
using CompuMaster.Scopevisio.OpenApi.Api;
using CompuMaster.Scopevisio.OpenApi.Client;
using CompuMaster.Scopevisio.OpenApi.Model;
using System;

namespace ConsoleSampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            string username = "TODO"
            string password = "TODO"

            try
            {
                //Create IO client for CenterDevice
                CenterDevice.IO.CenterDeviceIOClient IOClient = new CenterDevice.IO.CenterDeviceIOClient(new CenterDevice.Rest.Clients.CenterDeviceClient(oAuthInfoProvider, configuration, errorHandler), userID);  //TODO: provide arguments as usual for CenterDevice REST client

                //Show available directory structure
                if (true)
                {
                    System.Console.WriteLine("\r\n## Initial directory listing - flat");
                    System.Console.WriteLine(IOClient.RootDirectory.ToStringListing(false, true));

                    System.Console.WriteLine("\r\n## Initial directory listing - recursive - without display of files");
                    System.Console.WriteLine(IOClient.RootDirectory.ToStringListing(true, false));

                    System.Console.WriteLine("\r\n## Full directory listing - after GetDirectories(0)");
                    IOClient.RootDirectory.GetDirectories(0, false);
                    System.Console.WriteLine(IOClient.RootDirectory.ToStringListing(true, true));

                    System.Console.WriteLine("\r\n## Full directory listing - after GetDirectories(1)");
                    IOClient.RootDirectory.GetDirectories(1, true);
                    System.Console.WriteLine(IOClient.RootDirectory.ToStringListing(true, true));

                    System.Console.WriteLine("\r\n## Full directory listing - after GetDirectories(2)");
                    IOClient.RootDirectory.GetDirectories(2, true);
                    System.Console.WriteLine(IOClient.RootDirectory.ToStringListing(true, true));

                    System.Console.WriteLine("\r\n## Full directory listing - after GetDirectories(10)");
                    IOClient.RootDirectory.GetDirectories(10, true);
                    System.Console.WriteLine(IOClient.RootDirectory.ToStringListing(true, true));
                }

                //Navigate and open specified paths
                if (true)
                {
                    IO.DirectoryInfo BaseTestPath;
                    string OpenTestPath;
                    IO.DirectoryInfo OpenedTestDir;
                    IO.FileInfo OpenedTestFile;

                    BaseTestPath = IOClient.RootDirectory;
                    OpenTestPath = @"/";
                    System.Console.WriteLine("\r\n## Open directory path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestDir.FullName);

                    OpenTestPath = @"";
                    System.Console.WriteLine("\r\n## Open directory path - \"" + OpenTestPath + "\"" + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestDir.FullName);

                    OpenTestPath = @".";
                    System.Console.WriteLine("\r\n## Open directory path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestDir.FullName);

                    OpenTestPath = @"Test/Summen- und Saldenliste\2020/";
                    System.Console.WriteLine("\r\n## Open directory path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestDir.FullName);

                    OpenTestPath = @"Test/Summen- und Saldenliste\2020/test.txt.txt";
                    System.Console.WriteLine("\r\n## Open file path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestFile.FullName);

                    BaseTestPath = OpenedTestDir;
                    OpenTestPath = @"test.txt.txt";
                    System.Console.WriteLine("\r\n## Open file path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestFile.FullName);

                    OpenTestPath = @"/";
                    System.Console.WriteLine("\r\n## Open directory path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestDir.FullName);

                    OpenTestPath = @"";
                    System.Console.WriteLine("\r\n## Open directory path - \"" + OpenTestPath + "\"" + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestDir.FullName);

                    OpenTestPath = @".";
                    System.Console.WriteLine("\r\n## Open directory path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestDir.FullName);

                    OpenTestPath = @"..";
                    System.Console.WriteLine("\r\n## Open directory path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestDir.FullName);

                    OpenTestPath = @"test.txt.txt";
                    System.Console.WriteLine("\r\n## Open file path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestFile.FullName);

                    OpenTestPath = @"../test.txt.txt";
                    System.Console.WriteLine("\r\n## Open file path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestFile.FullName);

                    OpenTestPath = @"/Test/test.txt.txt";
                    System.Console.WriteLine("\r\n## Open file path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestFile.FullName);

                    OpenTestPath = @"/test.txt.txt";
                    System.Console.WriteLine("\r\n## Open file path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestFile.FullName);
                }

                //Upload and download
                if (true)
                {
                    IO.DirectoryInfo BaseTestPath;
                    string OpenTestPath;
                    IO.DirectoryInfo OpenedTestDir;
                    IO.FileInfo OpenedTestFile;

                    BaseTestPath = IOClient.RootDirectory;
                    OpenTestPath = @"/Test";

                    //Upload 
                    OpenedTestDir.Upload(@"c:\temp\my-file.txt", "my-uploaded-file.txt", IO.DirectoryInfo.UploadMode.CreateNewVersionOrNewFile);

                    //Download
                    OpenedTestFile = OpenedTestDir.OpenFilePath("my-uploaded-file.txt");
                    OpenedTestFile.Download(@"c:\temp\my-downloaded-file.txt");

                    //Delete the temporary file from server
                    OpenedTestFile.Delete();
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
            {
                System.Console.WriteLine("Exception: " + e.ToString());
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }
    }
}
```

