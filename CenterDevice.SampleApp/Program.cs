using System;
using System.Text;
using System.Threading.Tasks;
using CenterDevice.IO;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.SampleApp
{
    class Program
    {
        static void Main()
        {
            System.Console.WriteLine("PLEASE NOTE: Following user input will be buffered in directory " + System.IO.Path.GetTempPath());
            string username = InputLine("username");
            //string customerno = InputLine("customer no.");
            string password = InputLine("password");

            try
            {
                string ClientCredentials = username + ":" + password;
                string EncodedClientCredentials = System.Convert.ToBase64String(System.Text.Encoding.Default.GetBytes(ClientCredentials));
                System.Console.WriteLine("Authorization header:\nAuthorization: Basic " + EncodedClientCredentials);
                System.Console.WriteLine();

                //Authorize initially with user credentials -> recieved access token will be saved for following requests
                //TODO: somethine like CenterDeviceClient.AuthorizeWithUserCredentials(username, customerno, password);
                CenterDevice.Rest.Clients.CenterDeviceClient CenterDeviceClient = new CenterDevice.Rest.Clients.CenterDeviceClient();
                CenterDeviceClient.AuthorizeWithUserCredentials(username, password);
                //CenterDeviceClient.AuthorizeWithUserCredentials(username, customerno, password);
                System.Console.WriteLine("Authorization successful, future requests can be executed with access token:\n" + CenterDeviceClient.Token.access_token);
                System.Console.WriteLine();

                //Create IO client for CenterDevice 
                CenterDevice.IO.CenterDeviceIOClient IOClient = new CenterDevice.IO.CenterDeviceIOClient(CenterDeviceClient);
                //CenterDevice.IO.CenterDeviceIOClient IOClient = new CenterDevice.IO.CenterDeviceIOClient(
                //    new CenterDevice.Rest.Clients.CenterDeviceClient(
                //        oAuthInfoProvider, 
                //        configuration.OAuthConfiguration(), 
                //        new CenterDevice.Rest.Clients.CenterDeviceErrorHandler(username, oAuthInfoProvider)
                //        ), 
                //    username);  //TODO: provide arguments as usual for CenterDevice REST client

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
                }

                //Navigate and open specified paths
                if (false)
                {
                    CenterDevice.IO.DirectoryInfo OpenedTestDir;
                    CenterDevice.IO.FileInfo OpenedTestFile;
                    CenterDevice.IO.DirectoryInfo BaseTestPath = IOClient.RootDirectory;
                    string OpenTestPath;

                    OpenTestPath = @"Test/123/";
                    System.Console.WriteLine("\r\n## Open directory path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestDir.FullName);

                    OpenTestPath = @"Test/123/test.txt";
                    System.Console.WriteLine("\r\n## Open file path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestFile.FullName);

                    OpenTestPath = @"..";
                    System.Console.WriteLine("\r\n## Open directory path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestDir.FullName);

                    OpenTestPath = @"/Another/Test/test.txt";
                    System.Console.WriteLine("\r\n## Open file path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath);
                    System.Console.WriteLine("FullName=" + OpenedTestFile.FullName);
                }

                //File details
                if (false)
                {
                    CenterDevice.IO.DirectoryInfo BaseTestPath;
                    CenterDevice.IO.DirectoryInfo OpenedTestDir;
                    CenterDevice.IO.FileInfo OpenedTestFile;
                    string OpenTestPath;

                    BaseTestPath = IOClient.RootDirectory;
                    OpenTestPath = @"Test/123/";
                    System.Console.WriteLine("\r\n## Directory listing for path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    OpenedTestDir.GetDirectories(Int32.MaxValue, true);
                    System.Console.WriteLine(OpenedTestDir.ToStringListing(true, true));
                    System.Console.WriteLine("\r\n## Open directory path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    System.Console.WriteLine(OpenedTestDir.ToStringDetails());

                    BaseTestPath = IOClient.RootDirectory;
                    OpenTestPath = @"Test/123/test.txt";
                    System.Console.WriteLine("\r\n## Open file path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath);
                    System.Console.WriteLine(OpenedTestFile.ToStringDetails());
                }
                
                //File upload/download/deletion
                if (false)
                {
                    CenterDevice.IO.DirectoryInfo BaseTestPath;
                    CenterDevice.IO.DirectoryInfo OpenedTestDir;
                    CenterDevice.IO.FileInfo OpenedTestFile;
                    string OpenTestPath;
                    string TransferTestFileName;

                    TransferTestFileName = "uploaded.by.sampleapp.txt";
                    BaseTestPath = IOClient.RootDirectory;
                    OpenTestPath = @"Test/Summen- und Saldenliste\2020/" + TransferTestFileName;
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    OpenedTestDir.GetDirectories(Int32.MaxValue, true);
                    System.Console.WriteLine("\r\n## Open file path - " + OpenTestPath + " [Start: " + BaseTestPath + "]");
                    if (OpenedTestDir.FileExists(TransferTestFileName))
                    {
                        OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath);
                        System.Console.WriteLine(OpenedTestFile.ToStringDetails());
                        System.Console.Write("Deleting . . .");
                        OpenedTestFile.Delete();
                        System.Console.WriteLine(" DONE!");
                    }

                    string LocalSourceFile = @"D:\GitHub-OpenSource-Externals+Backups\bierdeckel-automation\CenterDevice\README.md";
                    BaseTestPath = IOClient.RootDirectory;
                    OpenTestPath = @"Test/Summen- und Saldenliste\2020";
                    OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath);
                    System.Console.WriteLine("\r\n## Uploading new file to directory - " + OpenTestPath + ": " + TransferTestFileName + "");
                    if (OpenedTestDir.FileExists(TransferTestFileName))
                        System.Console.WriteLine(Indent("File exists: " + OpenedTestDir.GetFile(TransferTestFileName).ToStringDetails()));
                    else
                        System.Console.WriteLine(Indent("File doesn't exist: " + TransferTestFileName));
                    System.Console.Write("Upload . . .");
                    OpenedTestDir.Upload(LocalSourceFile, TransferTestFileName, IO.DirectoryInfo.UploadMode.CreateNewVersionOrNewFile);
                    System.Console.WriteLine(" DONE!");
                    if (OpenedTestDir.FileExists(TransferTestFileName))
                        System.Console.WriteLine(Indent("File exists: " + OpenedTestDir.GetFile(TransferTestFileName).ToStringDetails()));
                    else
                        System.Console.WriteLine(Indent("File doesn't exist: " + TransferTestFileName));
                    if (OpenedTestDir.GetFile(TransferTestFileName).Size == (new System.IO.FileInfo(LocalSourceFile).Length))
                        System.Console.WriteLine("File length comparison: equal => SUCCESS!");
                    else
                        System.Console.WriteLine("File length comparison: not equal (" + (new System.IO.FileInfo(LocalSourceFile).Length).ToString() + " vs. " + OpenedTestDir.GetFile(TransferTestFileName).Size.ToString() + ") => FAILURE!");
                    System.Console.Write("Re-Download . . .");
                    OpenedTestFile = OpenedTestDir.OpenFilePath(TransferTestFileName);
                    string tmpFile = System.IO.Path.GetTempFileName();
                    OpenedTestFile.Download(tmpFile);
                    System.Console.WriteLine(" => \"" + tmpFile + "\" DONE!");
                    if (OpenedTestDir.GetFile(TransferTestFileName).Size == (new System.IO.FileInfo(tmpFile).Length))
                        System.Console.WriteLine("File length comparison: equal => SUCCESS!");
                    else
                        System.Console.WriteLine("File length comparison: not equal (" + (new System.IO.FileInfo(tmpFile).Length).ToString() + " vs. " + OpenedTestDir.GetFile(TransferTestFileName).Size.ToString() + ") => FAILURE!");
                    if (System.Linq.Enumerable.SequenceEqual(System.IO.File.ReadAllBytes(LocalSourceFile), System.IO.File.ReadAllBytes(tmpFile)))
                        System.Console.WriteLine("File comparison: equal => SUCCESS!");
                    else
                    {
                        System.Console.WriteLine("File comparison: not equal => FAILURE!");
                        System.Diagnostics.Process.Start(System.IO.Path.GetDirectoryName(tmpFile));
                    }
                    System.Console.Write("Deleting . . .");
                    OpenedTestFile.Delete();
                    System.Console.WriteLine(" DONE!");
                    if (OpenedTestDir.FileExists(TransferTestFileName))
                        System.Console.WriteLine(Indent("File exists: " + OpenedTestDir.GetFile(TransferTestFileName).ToStringDetails()));
                    else
                        System.Console.WriteLine(Indent("File doesn't exist: " + TransferTestFileName));
                }
            }
            catch (CompuMaster.Scopevisio.OpenApi.Client.ApiException e)
            {
                System.Console.WriteLine("Exception when calling API: " + e.Message);
                System.Console.WriteLine("Status Code: " + e.ErrorCode);
                System.Console.WriteLine(e.StackTrace);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
            {
                System.Console.WriteLine("Exception: " + e.ToString());
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }



        /// <summary>
        /// Ask user for field data and buffer them in local files
        /// </summary>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        static string InputLine(string fieldName)
        {
            string BufferFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "~Buffer.Sample." + fieldName.GetHashCode().ToString() + ".tmp");
            string DefaultValue;
            if (System.IO.File.Exists(BufferFile))
                DefaultValue = System.IO.File.ReadAllText(BufferFile);
            else
                DefaultValue = "";

            Console.WriteLine("Please enter " + fieldName + " [" + DefaultValue + "]: ");
            string UserInput = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(UserInput))
                return DefaultValue;
            else
            {
                System.IO.File.WriteAllText(BufferFile, UserInput);
                return UserInput;
            }
        }

        /// <summary>
        /// Indent a string
        /// </summary>
        private static string Indent(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            else
            {
                string result = "    " + value.Replace("\n", "\n    ");
                if (result.EndsWith("\n    "))
                    result = result.Substring(0, result.Length - ("\n    ".Length - 1));
                return result;
            }
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element