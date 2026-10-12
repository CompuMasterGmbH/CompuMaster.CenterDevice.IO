Option Explicit On
Option Strict On

Imports NUnit.Framework
Imports CenterDevice.Test.Tools

<TestFixture, NonParallelizable> Public Class DirectoriesExtendedTest
    Inherits TestBase

    Private Const RemoteTestDirPath As String = "ZZZ_UnitTests_CenterDevice"

    <OneTimeSetUp> Public Sub InitTests()
        CleanupTestData()
        'Create required test directories
        Dim RequiredDirPath As String
        RequiredDirPath = RemoteTestDirPath & "/Test/Summen- und Saldenliste/2020"
        If Me.IOClient.RootDirectory.DirectoryExists(RequiredDirPath) <> True Then
            Me.IOClient.RootDirectory.CreateDirectoryStructure(RequiredDirPath)
        End If
        'Create required test file
        Dim Dir As CenterDevice.IO.DirectoryInfo
        Dim RequiredFileName As String
        RequiredDirPath = RemoteTestDirPath & "/Test/Summen- und Saldenliste/2020"
        RequiredFileName = "test.txt.txt"
        Dir = Me.IOClient.RootDirectory.OpenDirectoryPath(RequiredDirPath)
        If Dir.FileExists(RequiredFileName) = False Then
            Dir.UploadAndCreateNewFile(TestFileForUploadTests, RequiredFileName)
        End If
    End Sub

    <OneTimeTearDown> Public Sub CleanupTestData()
        Dim BaseTestPath As CenterDevice.IO.DirectoryInfo
        Dim OpenedCleanupDir As CenterDevice.IO.DirectoryInfo
        BaseTestPath = Me.IOClient.RootDirectory
        If BaseTestPath.DirectoryExists(RemoteTestDirPath) Then
            OpenedCleanupDir = BaseTestPath.OpenDirectoryPath(RemoteTestDirPath)
            OpenedCleanupDir.Delete()
        End If
    End Sub

    Protected Function TestFileForUploadTests() As String
        Dim LocalFilePath As String = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Me.TestAssembly.Location), "TestFile.txt")
        If System.IO.File.Exists(LocalFilePath) = False Then Throw New System.IO.FileNotFoundException("File not found: " & LocalFilePath)
        Return LocalFilePath
    End Function

    <Test> Public Sub UploadNewFileByStream()
        Dim RequiredDirPath As String
        RequiredDirPath = RemoteTestDirPath & "/Test/Summen- und Saldenliste/2020"
        Dim RequiredFileName As String
        RequiredFileName = "test.by.streamupload.txt"
        Dim Dir As CenterDevice.IO.DirectoryInfo
        Dir = Me.IOClient.RootDirectory.OpenDirectoryPath(RequiredDirPath)

        If Dir.FileExists(RequiredFileName) = False Then
            Dir.Upload(Function() As System.IO.Stream
                           Return GetDummyBinaryStream(1)
                       End Function, RequiredFileName, IO.DirectoryInfo.UploadMode.DropExistingFileAndCreateNew)
        End If
        Assert.AreEqual(GetDummyBinaryData(1), ReadAllBytesFromStream(Dir.GetFile(RequiredFileName).Download()))

        Dir.Upload(Function() As System.IO.Stream
                       Return GetDummyBinaryStream(2)
                   End Function, RequiredFileName, IO.DirectoryInfo.UploadMode.CreateNewVersion)
        Assert.AreEqual(GetDummyBinaryData(2), ReadAllBytesFromStream(Dir.GetFile(RequiredFileName).Download()))

        Assert.AreEqual(GetDummyBinaryData(2), ReadAllBytesFromStream(Dir.GetFile(RequiredFileName).Download(0))) ' 0 = latest version
        Assert.AreEqual(GetDummyBinaryData(1), ReadAllBytesFromStream(Dir.GetFile(RequiredFileName).Download(1)))
        Assert.AreEqual(GetDummyBinaryData(2), ReadAllBytesFromStream(Dir.GetFile(RequiredFileName).Download(2)))
    End Sub

    Private Function ReadAllBytesFromStream(stream As System.IO.Stream) As Byte()
        Dim ms As New System.IO.MemoryStream()
        stream.CopyTo(ms)
        Return ms.ToArray
    End Function

    <Test> Public Sub RootDirWithDuplicateDirectories()
        Assert.IsFalse(Me.IOClient.RootDirectory.ContainsCollidingDuplicateFiles)
        Assert.IsFalse(Me.IOClient.RootDirectory.ContainsCollidingDuplicateDirectories)
    End Sub

    <Test> Public Sub RootDirBrowsing()
        Dim StartTime, EndTime As DateTime

        Me.IOClient.RootDirectory.ResetDirectoriesCache()
        Me.IOClient.RootDirectory.ResetFilesCache()

        StartTime = Now
        System.Console.WriteLine(System.Environment.NewLine & "## Initial directory listing - flat")
        System.Console.WriteLine(">>> Start: " & Now.ToString("yyyy-MM-dd HH:mm:ss"))
        System.Console.WriteLine(Me.IOClient.RootDirectory.ToStringListing(False, True))
        Assert.AreEqual("/", Me.IOClient.RootDirectory.ToStringListing(False, True))
        EndTime = Now
        System.Console.WriteLine("<<< End after " & EndTime.Subtract(StartTime).TotalSeconds.ToString("#,##0") & "s " & EndTime.Subtract(StartTime).Milliseconds.ToString("#,##0") & "ms: " & EndTime.ToString("yyyy-MM-dd HH:mm:ss"))

        StartTime = Now
        System.Console.WriteLine(System.Environment.NewLine & "## Initial directory listing - recursive - without display of files")
        System.Console.WriteLine(">>> Start: " & Now.ToString("yyyy-MM-dd HH:mm:ss"))
        System.Console.WriteLine(Me.IOClient.RootDirectory.ToStringListing(True, False))
        Assert.AreEqual("/" & System.Environment.NewLine & "    [Dirs:?]/", Me.IOClient.RootDirectory.ToStringListing(True, False))
        EndTime = Now
        System.Console.WriteLine("<<< End after " & EndTime.Subtract(StartTime).TotalSeconds.ToString("#,##0") & "s " & EndTime.Subtract(StartTime).Milliseconds.ToString("#,##0") & "ms: " & EndTime.ToString("yyyy-MM-dd HH:mm:ss"))

        StartTime = Now
        System.Console.WriteLine(System.Environment.NewLine & "## Full directory listing - after GetDirectories(0)")
        System.Console.WriteLine(">>> Start: " & Now.ToString("yyyy-MM-dd HH:mm:ss"))
        Me.IOClient.RootDirectory.GetDirectories(0, False)
        Assert.NotZero(Me.IOClient.RootDirectory.GetDirectories(0, False).Length)
        System.Console.WriteLine(Me.IOClient.RootDirectory.ToStringListing(True, True))
        EndTime = Now
        System.Console.WriteLine("<<< End after " & EndTime.Subtract(StartTime).TotalSeconds.ToString("#,##0") & "s " & EndTime.Subtract(StartTime).Milliseconds.ToString("#,##0") & "ms: " & EndTime.ToString("yyyy-MM-dd HH:mm:ss"))

        If False Then 'Currenlty DEACTIVATED for performance reasons

            StartTime = Now
            System.Console.WriteLine(System.Environment.NewLine & "## Full directory listing - after GetDirectories(1)")
            System.Console.WriteLine(">>> Start: " & Now.ToString("yyyy-MM-dd HH:mm:ss"))
            Me.IOClient.RootDirectory.GetDirectories(1, True)
            System.Console.WriteLine(Me.IOClient.RootDirectory.ToStringListing(True, True))
            EndTime = Now
            System.Console.WriteLine("<<< End after " & EndTime.Subtract(StartTime).TotalSeconds.ToString("#,##0") & "s " & EndTime.Subtract(StartTime).Milliseconds.ToString("#,##0") & "ms: " & EndTime.ToString("yyyy-MM-dd HH:mm:ss"))

            StartTime = Now
            System.Console.WriteLine(System.Environment.NewLine & "## Full directory listing - after GetDirectories(2)")
            System.Console.WriteLine(">>> Start: " & Now.ToString("yyyy-MM-dd HH:mm:ss"))
            Me.IOClient.RootDirectory.GetDirectories(2, True)
            System.Console.WriteLine(Me.IOClient.RootDirectory.ToStringListing(True, True))
            EndTime = Now
            System.Console.WriteLine("<<< End after " & EndTime.Subtract(StartTime).TotalSeconds.ToString("#,##0") & "s " & EndTime.Subtract(StartTime).Milliseconds.ToString("#,##0") & "ms: " & EndTime.ToString("yyyy-MM-dd HH:mm:ss"))

            StartTime = Now
            System.Console.WriteLine(System.Environment.NewLine & "## Full directory listing - after GetDirectories(Int32.MaxValue)")
            System.Console.WriteLine(">>> Start: " & Now.ToString("yyyy-MM-dd HH:mm:ss"))
            Me.IOClient.RootDirectory.GetDirectories(Int32.MaxValue, True)
            System.Console.WriteLine(Me.IOClient.RootDirectory.ToStringListing(True, True))
            EndTime = Now
            System.Console.WriteLine("<<< End after " & EndTime.Subtract(StartTime).TotalSeconds.ToString("#,##0") & "s " & EndTime.Subtract(StartTime).Milliseconds.ToString("#,##0") & "ms: " & EndTime.ToString("yyyy-MM-dd HH:mm:ss"))
        End If
    End Sub

    <Test> Public Sub NavigateAndOpenSpecificPaths()
        Dim BaseTestPath As CenterDevice.IO.DirectoryInfo
        Dim OpenTestPath As String
        Dim OpenedTestDir As CenterDevice.IO.DirectoryInfo
        Dim OpenedTestFile As CenterDevice.IO.FileInfo
        BaseTestPath = Me.IOClient.RootDirectory

        OpenTestPath = "/"
        System.Console.WriteLine(System.Environment.NewLine & "## Open directory path - " & OpenTestPath & " [Start: " & BaseTestPath.ToString & "]")

        OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestDir.FullName)

        OpenTestPath = ""
        System.Console.WriteLine(System.Environment.NewLine & "## Open directory path - """ + OpenTestPath + """" + " [Start: " & BaseTestPath.ToString & "]")

        OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestDir.FullName)

        OpenTestPath = "."
        System.Console.WriteLine(System.Environment.NewLine & "## Open directory path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")

        OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestDir.FullName)

        OpenTestPath = RemoteTestDirPath & "/Test/Summen- und Saldenliste\2020/"
        System.Console.WriteLine(System.Environment.NewLine & "## Open directory path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")

        OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestDir.FullName)

        OpenTestPath = RemoteTestDirPath & "/Test/Summen- und Saldenliste\2020/test.txt.txt"
        System.Console.WriteLine(System.Environment.NewLine & "## Open file path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")

        OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestFile.FullName)

        BaseTestPath = OpenedTestDir
        OpenTestPath = "test.txt.txt"
        System.Console.WriteLine(System.Environment.NewLine & "## Open file path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestFile.FullName)

        OpenTestPath = "/"
        System.Console.WriteLine(System.Environment.NewLine & "## Open directory path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestDir.FullName)

        OpenTestPath = ""
        System.Console.WriteLine(System.Environment.NewLine & "## Open directory path - """ + OpenTestPath + """" + " [Start: " & BaseTestPath.ToString & "]")
        OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestDir.FullName)

        OpenTestPath = "."
        System.Console.WriteLine(System.Environment.NewLine & "## Open directory path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestDir.FullName)

        OpenTestPath = ".."
        System.Console.WriteLine(System.Environment.NewLine & "## Open directory path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestDir.FullName)

        OpenTestPath = "test.txt.txt"
        System.Console.WriteLine(System.Environment.NewLine & "## Open file path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath)
        System.Console.WriteLine("FullName=" + OpenedTestFile.FullName)

        OpenTestPath = "../test.txt.txt"
        System.Console.WriteLine(System.Environment.NewLine & "## Open file path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        Assert.Catch(Of CenterDevice.Model.Exceptions.FileNotFoundException)(Sub()
                                                                                 OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath)
                                                                                 System.Console.WriteLine("FullName=" + OpenedTestFile.FullName)
                                                                             End Sub)

        OpenTestPath = "/" & RemoteTestDirPath & "/Test/test.txt.txt"
        System.Console.WriteLine(System.Environment.NewLine & "## Open file path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        Assert.Catch(Of CenterDevice.Model.Exceptions.FileNotFoundException)(Sub()
                                                                                 OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath)
                                                                                 System.Console.WriteLine("FullName=" + OpenedTestFile.FullName)
                                                                             End Sub)

        OpenTestPath = "/test.txt.txt"
        System.Console.WriteLine(System.Environment.NewLine & "## Open file path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        Assert.Catch(Of CenterDevice.Model.Exceptions.FileNotFoundException)(Sub()
                                                                                 OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath)
                                                                                 System.Console.WriteLine("FullName=" + OpenedTestFile.FullName)
                                                                             End Sub)
    End Sub

    <Test> Public Sub FileDetails()
        Dim BaseTestPath As CenterDevice.IO.DirectoryInfo
        Dim OpenTestPath As String
        Dim OpenedTestDir As CenterDevice.IO.DirectoryInfo
        Dim OpenedTestFile As CenterDevice.IO.FileInfo
        BaseTestPath = Me.IOClient.RootDirectory

        OpenTestPath = RemoteTestDirPath & "/Test/Summen- und Saldenliste\2020"
        System.Console.WriteLine(System.Environment.NewLine & "## Directory listing for path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        OpenedTestDir = BaseTestPath.OpenDirectoryPath(OpenTestPath)
        OpenedTestDir.GetDirectories(10, True)
        System.Console.WriteLine(OpenedTestDir.ToStringListing(True, True))
        System.Console.WriteLine(System.Environment.NewLine & "## Open directory path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        System.Console.WriteLine(OpenedTestDir.ToStringDetails())

        BaseTestPath = IOClient.RootDirectory
        OpenTestPath = RemoteTestDirPath & "/Test/Summen- und Saldenliste\2020/test.txt.txt"
        System.Console.WriteLine(System.Environment.NewLine & "## Open file path - " + OpenTestPath + " [Start: " & BaseTestPath.ToString & "]")
        OpenedTestFile = BaseTestPath.OpenFilePath(OpenTestPath)
        System.Console.WriteLine(OpenedTestFile.ToStringDetails())
        Assert.AreEqual("/" & RemoteTestDirPath & "/Test/Summen- und Saldenliste/2020/test.txt.txt", OpenedTestFile.FullName)
    End Sub

    <Test> Public Sub OpenDirectory()
        Const ExistingDir As String = RemoteTestDirPath & "/Test/Summen- und Saldenliste\2020"
        Dim BaseTestPath As CenterDevice.IO.DirectoryInfo
        Dim OpenedTestDir As CenterDevice.IO.DirectoryInfo
        BaseTestPath = Me.IOClient.RootDirectory

        OpenedTestDir = BaseTestPath.OpenDirectoryPath(ExistingDir)
        Assert.AreEqual("/" & ExistingDir.Replace("\", "/"), OpenedTestDir.FullName)

        OpenedTestDir = BaseTestPath.OpenDirectoryPath(ExistingDir & "/")
        Assert.AreEqual("/" & ExistingDir.Replace("\", "/"), OpenedTestDir.FullName)

        OpenedTestDir = BaseTestPath.OpenDirectoryPath("/" & ExistingDir) 'test root with leading path separator "/"
        Assert.AreEqual("/" & ExistingDir.Replace("\", "/"), OpenedTestDir.FullName)

        Assert.Catch(Of Exception)(Sub()
                                       OpenedTestDir = BaseTestPath.OpenDirectoryPath(ExistingDir & "DIR-NOT-EXISTING")
                                   End Sub)
    End Sub

    ''' <summary>
    ''' Create/open remote test folder
    ''' </summary>
    ''' <param name="remotePath"></param>
    ''' <returns></returns>
    Private Function CreateRemoteTestFolderIfNotExisting(remotePath As String) As IO.DirectoryInfo
        Dim BaseTestPath As CenterDevice.IO.DirectoryInfo
        BaseTestPath = Me.IOClient.RootDirectory

        'Lookup status (and fill caches)
        BaseTestPath.DirectoryExists(remotePath)
        Try
            BaseTestPath.OpenDirectoryPath(remotePath)
        Catch
        End Try

        'Create the folder
        If BaseTestPath.DirectoryExists(remotePath) = False Then
            BaseTestPath.CreateDirectoryStructure(remotePath)
        End If

        'Lookup status again and check that old caches don't exist
        Assert.IsTrue(BaseTestPath.DirectoryExists(remotePath))

        Dim Result As IO.DirectoryInfo = BaseTestPath.OpenDirectoryPath(remotePath)
        Assert.NotNull(Result)
        If Result.Type = IO.DirectoryInfo.DirectoryType.Folder Then
            Assert.AreEqual(IO.DirectoryInfo.DirectoryType.Folder, Result.Type)
        Else
            Assert.AreEqual(IO.DirectoryInfo.DirectoryType.Collection, Result.Type)
        End If
        Return Result
    End Function

    ''' <summary>
    ''' Remove a test folder
    ''' </summary>
    ''' <param name="remotePath"></param>
    ''' <param name="folderMustExist">True throws exception on missing folder, False removes folder if it exists</param>
    Private Sub RemoveRemoteTestFolder(remotePath As String, folderMustExist As Boolean)
        Dim BaseTestPath As CenterDevice.IO.DirectoryInfo
        BaseTestPath = Me.IOClient.RootDirectory

        'Lookup status (and fill caches)
        If folderMustExist Then
            Assert.IsTrue(BaseTestPath.DirectoryExists(remotePath))
            Assert.IsNotNull(BaseTestPath.OpenDirectoryPath(remotePath))
        Else
            BaseTestPath.DirectoryExists(remotePath)
            Try
                BaseTestPath.OpenDirectoryPath(remotePath)
            Catch
            End Try
        End If

        'Delete the folder
        If folderMustExist Then
            BaseTestPath.OpenDirectoryPath(remotePath).Delete()
        Else
            If BaseTestPath.DirectoryExists(remotePath) Then
                BaseTestPath.OpenDirectoryPath(remotePath).Delete()
            End If
        End If

        'Lookup status again and check that old caches don't exist
        Assert.IsFalse(BaseTestPath.DirectoryExists(remotePath), "Remote folder path """ & remotePath & """ is expected to not exist any more")

        Assert.Catch(Of CenterDevice.Model.Exceptions.DirectoryNotFoundException)(
            Sub()
                BaseTestPath.OpenDirectoryPath(remotePath)
            End Sub,
            "Reopening of folder should fail because remote folder """ & remotePath & """ is expected to not exist any more")
    End Sub

    <Test> Public Sub CreateCollectionOrFolderAndCleanup()
        Const RemoteTestFolderName As String = "ZZZ_UnitTest_CenterDevice_TempDir"
        Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderName)
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, True)
    End Sub

    ''' <summary>
    ''' Test link share API: upload link
    ''' </summary>
    <Test> Public Sub CreateUploadLinkForCollectionAndCleanup()
        Const RemoteTestFolderName As String = "ZZZ_UnitTest_CenterDevice_TempCollectionUploadLinkDir"
        Const CenterDeviceApi_ExistingBugNotReturningCorrectResult_For__Collection_UploadLink As Boolean = True

        'Cleanup existing data from previous runs
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, False)

        '(Re-)Create folder
        Dim RemoteCollection As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderName)
        Assert.AreEqual(IO.DirectoryInfo.DirectoryType.Collection, RemoteCollection.Type)
        Assert.NotNull(RemoteCollection.CollectionID)
        Assert.IsNotEmpty(RemoteCollection.CollectionID)

        Dim SharedCollection As CenterDevice.Rest.Clients.Collections.Collection
        SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
        Assert.IsFalse(SharedCollection.HasUploadLink)
        Assert.IsNull(SharedCollection.UploadLink)

        'Create an upload link and check
        Dim CreatedUploadLink As CenterDevice.Rest.Clients.Link.UploadLinkCreationResponse
        CreatedUploadLink = Me.IOClient.ApiClient.UploadLinks.CreateCollectionLink(
                    Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID,
                                                               Tools.NotNullOrEmptyStringValue(RemoteTestFolderName & "_TempUploadLinkShare"),
                                                               Nothing,
                                                               DateTimeLocalToUtcTime(Now),
                                                               ConvertNarrowingToNullableInt32(Integer.MaxValue),
                                                               Tools.NotNullOrEmptyStringValue(Guid.NewGuid.ToString("n")),
                                                               Nothing
                                                               )
        Assert.NotNull(CreatedUploadLink)
        Assert.NotNull(CreatedUploadLink.Id)
        Assert.IsNotEmpty(CreatedUploadLink.Id)
        Assert.NotNull(CreatedUploadLink.Web)
        Assert.IsNotEmpty(CreatedUploadLink.Web)
        System.Console.WriteLine("CREATED: Collection ID " & RemoteCollection.CollectionID & " (" & RemoteTestFolderName & ") shared as upload link with")
        System.Console.WriteLine("- ID: " & CreatedUploadLink.Id)
        System.Console.WriteLine("- Web: " & CreatedUploadLink.Web)

        'Update link share and test again
        Me.IOClient.ApiClient.UploadLink.UpdateLink(
                    Me.IOClient.CurrentAuthenticationContextUserID, CreatedUploadLink.Id, RemoteCollection.CollectionID,
                                                               Tools.NotNullOrEmptyStringValue(RemoteTestFolderName & "_TempUploadLinkShare_Updated"),
                                                               Nothing,
                                                               DateTimeLocalToUtcTime(Now),
                                                               ConvertNarrowingToNullableInt32(Integer.MaxValue),
                                                               Tools.NotNullOrEmptyStringValue(Guid.NewGuid.ToString("n")),
                                                               Nothing
                                                               )

        'Test for success
        SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
        If CenterDeviceApi_ExistingBugNotReturningCorrectResult_For__Collection_UploadLink Then
            'BUG IN CENTERDEVICE API
            Assert.IsFalse(SharedCollection.HasUploadLink)
            Assert.IsNull(SharedCollection.UploadLink)
        Else
            Assert.IsTrue(SharedCollection.HasUploadLink)
            Assert.IsNotNull(SharedCollection.UploadLink)
            Assert.IsNotEmpty(SharedCollection.UploadLink)
        End If
        System.Console.WriteLine("CHECKUP 1: Collection ID " & RemoteCollection.CollectionID & " (" & RemoteTestFolderName & ") shared as view/download link with ID: " & SharedCollection.UploadLink)

        Dim AllUploadLinks As CenterDevice.Rest.Clients.Link.UploadLinks = Me.IOClient.ApiClient.UploadLinks.GetAllUploadLinks(Me.IOClient.CurrentAuthenticationContextUserID)
        Dim FoundLinkAtIndex As Integer = AllUploadLinks.UploadLinksList.FindIndex(Function(value As CenterDevice.Rest.Clients.Link.UploadLink) As Boolean
                                                                                       Return value.Id = CreatedUploadLink.Id
                                                                                   End Function)
        Dim UploadLink = AllUploadLinks.UploadLinksList(FoundLinkAtIndex)
        Assert.AreEqual(CreatedUploadLink.Id, UploadLink.Id)
        System.Console.WriteLine("CHECKUP 2: Collection ID " & RemoteCollection.CollectionID & " (" & RemoteTestFolderName & ") shared as view/download link")
        System.Console.WriteLine("- ID: " & UploadLink.Id)
        System.Console.WriteLine("- Name: " & UploadLink.Name)
        System.Console.WriteLine("- Web: " & UploadLink.Web)
        System.Console.WriteLine("- MaxBytes: " & UploadLink.MaxBytes)
        System.Console.WriteLine("- Password: " & UploadLink.Password)
        Assert.NotNull(UploadLink.Password)
        Assert.IsNotEmpty(UploadLink.Password)

        'Remove link again
        Me.IOClient.ApiClient.UploadLink.DeleteLink(Me.IOClient.CurrentAuthenticationContextUserID, CreatedUploadLink.Id)
        SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
        Assert.IsFalse(SharedCollection.HasUploadLink)
        Assert.IsNull(SharedCollection.UploadLink)

        'Cleanup
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, True)
    End Sub

    ''' <summary>
    ''' Test link share API: view/download link
    ''' </summary>
    <Test> Public Sub CreateDownloadLinkForCollectionAndCleanup()
        Const RemoteTestFolderName As String = "ZZZ_UnitTest_CenterDevice_TempCollectionDownloadLinkDir"

        'Cleanup existing data from previous runs
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, False)

        '(Re-)Create folder
        Dim RemoteCollection As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderName)
        Assert.AreEqual(IO.DirectoryInfo.DirectoryType.Collection, RemoteCollection.Type)
        Assert.NotNull(RemoteCollection.CollectionID)
        Assert.IsNotEmpty(RemoteCollection.CollectionID)

        'Initial check
        Dim SharedCollection As CenterDevice.Rest.Clients.Collections.Collection
        SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
        Assert.IsFalse(SharedCollection.HasLink)
        Assert.IsNull(SharedCollection.Link)

        'Create a view/download link and check
        Dim AccessControl As New CenterDevice.Rest.Clients.Link.LinkAccessControl() With {.ViewOnly = True}

        Dim CreatedLink As CenterDevice.Rest.Clients.Link.LinkCreationResponse
        CreatedLink = Me.IOClient.ApiClient.Links.CreateCollectionLink(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID, AccessControl)
        Assert.NotNull(CreatedLink)
        Assert.NotNull(CreatedLink.Id)
        Assert.IsNotEmpty(CreatedLink.Id)
        Assert.NotNull(CreatedLink.Web)
        Assert.IsNotEmpty(CreatedLink.Web)
        System.Console.WriteLine("CREATED: Collection ID " & RemoteCollection.CollectionID & " (" & RemoteTestFolderName & ") shared as view/download link with")
        System.Console.WriteLine("- ID: " & CreatedLink.Id)
        System.Console.WriteLine("- Download: " & CreatedLink.Download)
        System.Console.WriteLine("- Web: " & CreatedLink.Web)

        'Update link share and test again
        Me.IOClient.ApiClient.Link.UpdateLink(
                    Me.IOClient.CurrentAuthenticationContextUserID, CreatedLink.Id, New CenterDevice.Rest.Clients.Link.LinkAccessControl() With {.ViewOnly = True, .Password = Guid.NewGuid.ToString("n")})

        'Test for success
        Dim LinkDetails As CenterDevice.Rest.Clients.Link.Link = Me.IOClient.ApiClient.Link.GetLink(Me.IOClient.CurrentAuthenticationContextUserID, CreatedLink.Id)
        Assert.NotNull(LinkDetails)
        Assert.AreEqual(RemoteCollection.CollectionID, LinkDetails.Collection)
        Assert.AreEqual(CreatedLink.Id, LinkDetails.Id)
        System.Console.WriteLine("CHECKUP 1 by link: Collection ID " & RemoteCollection.CollectionID & " (" & RemoteTestFolderName & ") shared as view/download link with ID: " & SharedCollection.Link)

        'Test for success
        SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
        Assert.IsTrue(SharedCollection.HasLink)
        Assert.IsNotNull(SharedCollection.Link)
        Assert.IsNotEmpty(SharedCollection.Link)
        System.Console.WriteLine("CHECKUP 2 by collection: Collection ID " & RemoteCollection.CollectionID & " (" & RemoteTestFolderName & ") shared as view/download link with ID: " & SharedCollection.Link)

        'Remove sharing again
        Me.IOClient.ApiClient.Link.DeleteLink(Me.IOClient.CurrentAuthenticationContextUserID, CreatedLink.Id)
        SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
        Assert.IsFalse(SharedCollection.HasLink)
        Assert.IsNull(SharedCollection.Link)

        'Cleanup
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, True)
    End Sub

    Private Function GetDummyBinaryStream(individualValue As Byte) As System.IO.Stream
        Return New System.IO.MemoryStream(GetDummyBinaryData(individualValue))
    End Function

    Private Function GetDummyBinaryData(individualValue As Byte) As Byte()
        Return New Byte() {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 32, 33, 34, 35, 36, 37, individualValue}
    End Function

    ''' <summary>
    ''' Test user share API
    ''' </summary>
    <Test> Public Sub CreateUserSharingForCollectionAndCleanup()
        Const RemoteTestFolderName As String = "ZZZ_UnitTest_CenterDevice_TempCollectionUserShareDir"

        'Cleanup existing data from previous runs
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, False)

        '(Re-)Create folder
        Dim RemoteCollection As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderName)
        Assert.AreEqual(IO.DirectoryInfo.DirectoryType.Collection, RemoteCollection.Type)
        Assert.NotNull(RemoteCollection.CollectionID)
        Assert.IsNotEmpty(RemoteCollection.CollectionID)

        Dim AllUsers = Me.IOClient.ApiClient.Users.GetAllUsers(Me.IOClient.CurrentAuthenticationContextUserID, New String() {CenterDevice.Rest.Clients.User.UserStatus.ACTIVE})
        Dim A1stUser As CenterDevice.Rest.Clients.User.BaseUserData = Nothing
        Dim A2ndUser As CenterDevice.Rest.Clients.User.BaseUserData = Nothing
        System.Console.WriteLine("CurrentAuthenticationContextUserID: " & Me.IOClient.CurrentAuthenticationContextUserID)
        System.Console.WriteLine("CurrentContextUserId: " & Me.IOClient.CurrentContextUserId)
        For MyCounter As Integer = 0 To AllUsers.Users.Count - 1
            If AllUsers.Users(MyCounter).Id = Me.IOClient.CurrentContextUserId Then
                'Sharing to own user is not possible
            ElseIf AllUsers.Users(MyCounter).Id.StartsWith("TECHNICAL_USER_") Then
                'This action cannot be performed on technical user
            Else
                If A1stUser Is Nothing Then
                    A1stUser = AllUsers.Users(MyCounter)
                ElseIf A2ndUser Is Nothing Then
                    A2ndUser = AllUsers.Users(MyCounter)
                Else
                    Exit For
                End If
            End If
            System.Console.WriteLine("Found user at index " & MyCounter & ": " & AllUsers.Users(MyCounter).Id & " / " & AllUsers.Users(MyCounter).GetFullName)
            If MyCounter > 5 Then Exit For
        Next
        If AllUsers.Users.Count <> 0 AndAlso A1stUser IsNot Nothing Then
            Dim SharedCollection As CenterDevice.Rest.Clients.Collections.Collection
            Dim AllVisibleShareRecipients As List(Of String)

            'Initial check
            SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
            AllVisibleShareRecipients = SharedCollection.Users?.Visible
            Assert.IsFalse(AllVisibleShareRecipients IsNot Nothing AndAlso AllVisibleShareRecipients.Contains(A1stUser.Id))

            'Create a share
            Dim SharingFailures As CenterDevice.Rest.Clients.Common.SharingResponse
            Dim RecipientUsers As String()
            If A2ndUser Is Nothing Then
                RecipientUsers = New String() {A1stUser.Id}
            Else
                RecipientUsers = New String() {A1stUser.Id, A2ndUser.Id}
            End If
            SharingFailures = Me.IOClient.ApiClient.Collection.ShareCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID, RecipientUsers, Nothing)
            Assert.Null(SharingFailures)
            System.Console.WriteLine("CREATED: Collection ID " & RemoteCollection.CollectionID & " (" & RemoteTestFolderName & ") shared as user share")

            'Test for success
            SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
            Assert.IsTrue(SharedCollection.Users.HasSharing)
            AllVisibleShareRecipients = SharedCollection.Users.Visible
            Assert.IsTrue(AllVisibleShareRecipients.Contains(A1stUser.Id))
            System.Console.WriteLine("CHECKUP: Collection ID " & RemoteCollection.CollectionID & " (" & RemoteTestFolderName & ") shared as user share to user ID: " & A1stUser.Id)

            'Remove sharing again
            Assert.IsNull(Me.IOClient.ApiClient.Collection.UnshareCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID, New String() {A1stUser.Id}, Nothing))
            SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
            AllVisibleShareRecipients = SharedCollection.Users?.Visible
            Assert.IsFalse(AllVisibleShareRecipients IsNot Nothing AndAlso AllVisibleShareRecipients.Contains(A1stUser.Id))

            'Cleanup
            Me.RemoveRemoteTestFolder(RemoteTestFolderName, True)
        Else
            Assert.Ignore("No users found, but at least 1 user required as share recipient (except the currently logged in user as share owner)")
        End If
    End Sub

    ''' <summary>
    ''' Test group share API 
    ''' </summary>
    <Test> Public Sub CreateGroupSharingForCollectionAndCleanup()
        Const RemoteTestFolderName As String = "ZZZ_UnitTest_CenterDevice_TempCollectionGroupShareDir"

        'Cleanup existing data from previous runs
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, False)

        '(Re-)Create folder
        Dim RemoteCollection As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderName)
        Assert.AreEqual(IO.DirectoryInfo.DirectoryType.Collection, RemoteCollection.Type)
        Assert.NotNull(RemoteCollection.CollectionID)
        Assert.IsNotEmpty(RemoteCollection.CollectionID)

        Dim AllGroups = Me.IOClient.ApiClient.Groups.GetAllGroups(Me.IOClient.CurrentAuthenticationContextUserID, Model.Groups.GroupsFilter.AllVisibleGroupsForCurrentUser)
        If AllGroups.Groups.Count <> 0 Then
            Dim AFirstGroup = AllGroups.Groups(0)
            Dim SharedCollection As CenterDevice.Rest.Clients.Collections.Collection
            Dim AllVisibleShareRecipients As List(Of String)

            'Initial check
            SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
            AllVisibleShareRecipients = SharedCollection.Groups?.Visible
            Assert.IsFalse(AllVisibleShareRecipients IsNot Nothing AndAlso AllVisibleShareRecipients.Contains(AFirstGroup.Id))

            'Create a share
            Dim SharingFailures As CenterDevice.Rest.Clients.Common.SharingResponse
            SharingFailures = Me.IOClient.ApiClient.Collection.ShareCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID, New String() {}, New String() {AFirstGroup.Id})
            Assert.Null(SharingFailures)
            System.Console.WriteLine("CREATED: Collection ID " & RemoteCollection.CollectionID & " (" & RemoteTestFolderName & ") shared as group share")

            'Test for success
            SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
            Assert.IsTrue(SharedCollection.Groups.HasSharing)
            AllVisibleShareRecipients = SharedCollection.Groups.Visible
            Assert.IsTrue(AllVisibleShareRecipients.Contains(AFirstGroup.Id))
            System.Console.WriteLine("CHECKUP: Collection ID " & RemoteCollection.CollectionID & " (" & RemoteTestFolderName & ") shared as group share to group ID: " & AFirstGroup.Id)

            'Remove sharing again
            Assert.IsNull(Me.IOClient.ApiClient.Collection.UnshareCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID, Nothing, New String() {AFirstGroup.Id}))
            SharedCollection = Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, RemoteCollection.CollectionID)
            AllVisibleShareRecipients = SharedCollection.Groups?.Visible
            Assert.IsFalse(AllVisibleShareRecipients IsNot Nothing AndAlso AllVisibleShareRecipients.Contains(AFirstGroup.Id))
        Else
            Assert.Ignore("No groups found, but at least 1 group required as share recipient")
        End If

        'Cleanup
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, True)
    End Sub

    <Test> Public Sub CopyFiles()
        Const RemoteTestFolderNameSrc As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Src"
        Const RemoteTestFolderNameDestCollection As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Dest"
        Const RemoteTestFolderNameDestFolder1 As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Dest/subfolder1"
        Const RemoteTestFolderNameDestFolder2 As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Dest/subfolder2"

        Me.RemoveRemoteTestFolder(RemoteTestFolderNameSrc, False)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestCollection, False)

        Dim RemoteTestDirSrc As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameSrc)
        Dim RemoteTestDirDestCollection As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestCollection)
        Dim RemoteTestDirDestFolder1 As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestFolder1)
        Dim RemoteTestDirDestFolder2 As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestFolder2)

        Dim TestFile1Data As Byte() = New Byte() {55, 56, 32, 42, 13, 10, 44, 48, 50}

        Using TempFileData As New System.IO.MemoryStream(TestFile1Data)
            RemoteTestDirSrc.UploadAndCreateNewFile(Function() TempFileData, "test-source-file")
        End Using
        Dim SrcFile As IO.FileInfo = RemoteTestDirSrc.GetFile("test-source-file")

        Console.WriteLine("RemoteTestDirDestCollection=" & RemoteTestDirDestCollection.FullName)
        Console.WriteLine("RemoteTestDirDestCollection.CollectionID=" & RemoteTestDirDestCollection.CollectionID)
        RemoteTestDirDestCollection.AddCopy(SrcFile, "test-source-file")
        Dim CopyInDestCollection As IO.FileInfo = RemoteTestDirDestCollection.GetFile("test-source-file")
        Assert.AreNotEqual(SrcFile.ID, CopyInDestCollection.ID)

        RemoteTestDirDestFolder1.AddCopy(SrcFile, "test-source-file")
        Dim CopyInDestFolder1 As IO.FileInfo = RemoteTestDirDestFolder1.GetFile("test-source-file")
        Assert.AreNotEqual(SrcFile.ID, CopyInDestFolder1.ID)

        RemoteTestDirDestFolder2.AddCopy(SrcFile, "test-source-file")
        Dim CopyInDestFolder2 As IO.FileInfo = RemoteTestDirDestFolder2.GetFile("test-source-file")
        Assert.AreNotEqual(SrcFile.ID, CopyInDestFolder2.ID)

        Assert.AreNotEqual(SrcFile.ID, RemoteTestDirDestCollection.GetFile("test-source-file").ID)
        Assert.AreNotEqual(SrcFile.ID, RemoteTestDirDestFolder1.GetFile("test-source-file").ID)
        Assert.AreNotEqual(SrcFile.ID, RemoteTestDirDestFolder2.GetFile("test-source-file").ID)

        RemoteTestDirDestCollection.ResetFilesCache()
        RemoteTestDirDestFolder1.ResetFilesCache()
        RemoteTestDirDestFolder2.ResetFilesCache()

        Assert.AreNotEqual(SrcFile.ID, RemoteTestDirDestCollection.GetFile("test-source-file").ID)
        Assert.AreNotEqual(SrcFile.ID, RemoteTestDirDestFolder1.GetFile("test-source-file").ID)
        Assert.AreNotEqual(SrcFile.ID, RemoteTestDirDestFolder2.GetFile("test-source-file").ID)

        Assert.AreEqual(TestFile1Data, StreamToByteArray(RemoteTestDirDestCollection.GetFile("test-source-file").Download()))
        Assert.AreEqual(TestFile1Data, StreamToByteArray(RemoteTestDirDestFolder1.GetFile("test-source-file").Download()))
        Assert.AreEqual(TestFile1Data, StreamToByteArray(RemoteTestDirDestFolder2.GetFile("test-source-file").Download()))

        Me.RemoveRemoteTestFolder(RemoteTestFolderNameSrc, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestFolder1, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestFolder2, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestCollection, True)
    End Sub

    ''' <summary>
    ''' A file/document can be referenced in a 1st collection to an origin source in a 2nd collection, but there can't be more than 1 link per collection -> 1st collection can contain (including all sub folders) max. 1 link -> adding a 2nd file link in a folder of 1st collection directly removes the origin 1st link in 1st collection
    ''' </summary>
    <Test> Public Sub AddFileLinksWithSpecialCenterDeviceBehaviour()
        Const RemoteTestFolderNameSrc As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Src"
        Const RemoteTestFolderNameDestCollection As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Dest"
        Const RemoteTestFolderNameDestFolder1 As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Dest/subfolder1"
        Const RemoteTestFolderNameDestFolder2 As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Dest/subfolder2"

        Me.RemoveRemoteTestFolder(RemoteTestFolderNameSrc, False)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestCollection, False)

        Dim RemoteTestDirSrc As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameSrc)
        Dim RemoteTestDirDestCollection As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestCollection)
        Dim RemoteTestDirDestFolder1 As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestFolder1)
        Dim RemoteTestDirDestFolder2 As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestFolder2)

        Dim TestFile1Data As Byte() = New Byte() {55, 56, 32, 42, 13, 10, 44, 48, 50}

        Using TempFileData As New System.IO.MemoryStream(TestFile1Data)
            RemoteTestDirSrc.UploadAndCreateNewFile(Function() TempFileData, "test-source-file")
        End Using
        Dim SrcFile As IO.FileInfo = RemoteTestDirSrc.GetFile("test-source-file")

        Console.WriteLine("RemoteTestDirDestCollection=" & RemoteTestDirDestCollection.FullName)
        Console.WriteLine("RemoteTestDirDestCollection.CollectionID=" & RemoteTestDirDestCollection.CollectionID)
        RemoteTestDirDestCollection.AddExistingFile(SrcFile)
        Dim CopyInDestCollection As IO.FileInfo = RemoteTestDirDestCollection.GetFile("test-source-file")
        Assert.AreEqual(SrcFile.ID, CopyInDestCollection.ID)

        RemoteTestDirDestFolder1.AddExistingFile(SrcFile)
        Dim CopyInDestFolder1 As IO.FileInfo = RemoteTestDirDestFolder1.GetFile("test-source-file")
        Assert.AreEqual(SrcFile.ID, CopyInDestFolder1.ID)

        RemoteTestDirDestFolder2.AddExistingFile(SrcFile)
        Dim CopyInDestFolder2 As IO.FileInfo = RemoteTestDirDestFolder2.GetFile("test-source-file")
        Assert.AreEqual(SrcFile.ID, CopyInDestFolder2.ID)

        Assert.AreEqual(SrcFile.ID, RemoteTestDirDestCollection.GetFile("test-source-file").ID)
        Assert.AreEqual(SrcFile.ID, RemoteTestDirDestFolder1.GetFile("test-source-file").ID)
        Assert.AreEqual(SrcFile.ID, RemoteTestDirDestFolder2.GetFile("test-source-file").ID)

        RemoteTestDirDestCollection.ResetFilesCache()
        RemoteTestDirDestFolder1.ResetFilesCache()
        RemoteTestDirDestFolder2.ResetFilesCache()

        Assert.IsFalse(RemoteTestDirDestCollection.FileExists("test-source-file"))
        Assert.IsFalse(RemoteTestDirDestFolder1.FileExists("test-source-file"))
        Assert.IsTrue(RemoteTestDirDestFolder2.FileExists("test-source-file"))
        Assert.AreEqual(SrcFile.ID, RemoteTestDirDestFolder2.GetFile("test-source-file").ID)
        Assert.AreEqual(TestFile1Data, StreamToByteArray(RemoteTestDirDestFolder2.GetFile("test-source-file").Download()))

        Me.RemoveRemoteTestFolder(RemoteTestFolderNameSrc, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestFolder1, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestFolder2, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestCollection, True)
    End Sub

    ''' <summary>
    ''' A file/document can be referenced in a 1st collection to an origin source in a 2nd collection, but there can't be more than 1 link per collection -> 1st collection can contain (including all sub folders) max. 1 link -> adding a 2nd file link in a folder of 1st collection directly removes the origin 1st link in 1st collection
    ''' </summary>
    <Ignore("NUnit.Framework.AssertionException: Remote folder path ""ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Dest"" is expected to not exist any more => local caching issue?")>
    <Test> Public Sub AddFileLinksWithSpecialCenterDeviceBehaviour_Parallelized()
        Const RemoteTestFolderNameSrc As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Src"
        Const RemoteTestFolderNameDestCollection As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Dest"
        Const RemoteTestFolderNameDestFolder1 As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Dest/subfolder1"
        Const RemoteTestFolderNameDestFolder2 As String = "ZZZ_UnitTest_CenterDevice_TempDir_CopyFiles_Dest/subfolder2"

        Dim CleanupTasks As New ParallelTasksBundle()
        CleanupTasks.Add(Task.Run(Sub() Me.RemoveRemoteTestFolder(RemoteTestFolderNameSrc, False)))
        CleanupTasks.Add(Task.Run(Sub() Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestCollection, False)))
        CleanupTasks.WaitAll()

        Dim CreateDirsLevel1 As New ParallelTasksBundle
        Dim RemoteTestDirSrc As Task(Of IO.DirectoryInfo) = CleanupTasks.Add(Task.Run(Function() Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameSrc)))
        Dim RemoteTestDirDestCollection As Task(Of IO.DirectoryInfo) = CleanupTasks.Add(Task.Run(Function() Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestCollection)))
        CreateDirsLevel1.WaitAll()

        Dim CreateDirsLevel2 As New ParallelTasksBundle
        Dim RemoteTestDirDestFolder1 As Task(Of IO.DirectoryInfo) = CleanupTasks.Add(Task.Run(Function() Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestFolder1)))
        Dim RemoteTestDirDestFolder2 As Task(Of IO.DirectoryInfo) = CleanupTasks.Add(Task.Run(Function() Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestFolder2)))
        CreateDirsLevel2.WaitAll()

        Dim TestFile1Data As Byte() = New Byte() {55, 56, 32, 42, 13, 10, 44, 48, 50}

        Using TempFileData As New System.IO.MemoryStream(TestFile1Data)
            RemoteTestDirSrc.Result.UploadAndCreateNewFile(Function() TempFileData, "test-source-file")
        End Using
        Dim SrcFile As IO.FileInfo = RemoteTestDirSrc.Result.GetFile("test-source-file")

        Console.WriteLine("RemoteTestDirDestCollection=" & RemoteTestDirDestCollection.Result.FullName)
        Console.WriteLine("RemoteTestDirDestCollection.CollectionID=" & RemoteTestDirDestCollection.Result.CollectionID)
        RemoteTestDirDestCollection.Result.AddExistingFile(SrcFile)
        Dim CopyInDestCollection As IO.FileInfo = RemoteTestDirDestCollection.Result.GetFile("test-source-file")
        Assert.AreEqual(SrcFile.ID, CopyInDestCollection.ID)

        RemoteTestDirDestFolder1.Result.AddExistingFile(SrcFile)
        Dim CopyInDestFolder1 As IO.FileInfo = RemoteTestDirDestFolder1.Result.GetFile("test-source-file")
        Assert.AreEqual(SrcFile.ID, CopyInDestFolder1.ID)

        RemoteTestDirDestFolder2.Result.AddExistingFile(SrcFile)
        Dim CopyInDestFolder2 As IO.FileInfo = RemoteTestDirDestFolder2.Result.GetFile("test-source-file")
        Assert.AreEqual(SrcFile.ID, CopyInDestFolder2.ID)

        Assert.AreEqual(SrcFile.ID, RemoteTestDirDestCollection.Result.GetFile("test-source-file").ID)
        Assert.AreEqual(SrcFile.ID, RemoteTestDirDestFolder1.Result.GetFile("test-source-file").ID)
        Assert.AreEqual(SrcFile.ID, RemoteTestDirDestFolder2.Result.GetFile("test-source-file").ID)

        RemoteTestDirDestCollection.Result.ResetFilesCache()
        RemoteTestDirDestFolder1.Result.ResetFilesCache()
        RemoteTestDirDestFolder2.Result.ResetFilesCache()

        Assert.IsFalse(RemoteTestDirDestCollection.Result.FileExists("test-source-file"))
        Assert.IsFalse(RemoteTestDirDestFolder1.Result.FileExists("test-source-file"))
        Assert.IsTrue(RemoteTestDirDestFolder2.Result.FileExists("test-source-file"))
        Assert.AreEqual(SrcFile.ID, RemoteTestDirDestFolder2.Result.GetFile("test-source-file").ID)
        Assert.AreEqual(TestFile1Data, StreamToByteArray(RemoteTestDirDestFolder2.Result.GetFile("test-source-file").Download()))

        Dim FinalCleanupTasksLevel1 As New ParallelTasksBundle()
        Dim FinalCleanupTasksLevel2 As New ParallelTasksBundle()
        FinalCleanupTasksLevel2.Add(Task.Run(Sub() Me.RemoveRemoteTestFolder(RemoteTestFolderNameSrc, True)))
        FinalCleanupTasksLevel1.Add(Task.Run(Sub() Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestFolder1, True)))
        FinalCleanupTasksLevel1.Add(Task.Run(Sub() Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestFolder2, True)))
        FinalCleanupTasksLevel1.WaitAll()
        FinalCleanupTasksLevel2.Add(Task.Run(Sub() Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestCollection, True)))
        FinalCleanupTasksLevel2.WaitAll()
    End Sub

    ''' <summary>
    ''' Move a file from a first collection/folder to another one (WARNING: special CenterDevice behaviour: moving a file from a 1st collection to a 2nd collection will automatically remove any links in 2nd collection which might already exist in one of the folders of 2nd collection)
    ''' </summary>
    <Test, Ignore("Superseded by CopyAndMoveThroughTeamwork in the explicit owned-resource integration project; this legacy fixture uses fixed shared paths without failure-safe cleanup.")> Public Sub MoveFiles()
        Const RemoteTestFolderNameSrc As String = "ZZZ_UnitTest_CenterDevice_TempDir_MoveFiles_Src"
        Const RemoteTestFolderNameDestCollection As String = "ZZZ_UnitTest_CenterDevice_TempDir_MoveFiles_Dest"
        Const RemoteTestFolderNameDestFolder1 As String = "ZZZ_UnitTest_CenterDevice_TempDir_MoveFiles_Dest/subfolder1"
        Const RemoteTestFolderNameDestFolder2 As String = "ZZZ_UnitTest_CenterDevice_TempDir_MoveFiles_Dest/subfolder2"
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameSrc, False)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestCollection, False)

        Dim RemoteTestDirSrc As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameSrc)
        Dim RemoteTestDirDestCollection As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestCollection)
        Dim RemoteTestDirDestFolder1 As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestFolder1)
        Dim RemoteTestDirDestFolder2 As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestFolder2)

        Dim TestFile1Data As Byte() = New Byte() {55, 56, 32, 42, 13, 10, 44, 48, 50}

        Using TempFileData As New System.IO.MemoryStream(TestFile1Data)
            RemoteTestDirSrc.UploadAndCreateNewFile(Function() TempFileData, "test-source-file")
        End Using
        Dim SrcFile As IO.FileInfo = RemoteTestDirSrc.GetFile("test-source-file")

        Console.WriteLine("RemoteTestDirDestCollection=" & RemoteTestDirDestCollection.FullName)
        Console.WriteLine("RemoteTestDirDestCollection.CollectionID=" & RemoteTestDirDestCollection.CollectionID)
        SrcFile.Move(RemoteTestDirDestCollection)
        Dim MoveInDestCollection As IO.FileInfo = RemoteTestDirDestCollection.GetFile("test-source-file")
        Assert.AreEqual(SrcFile.ID, MoveInDestCollection.ID)
        Assert.AreEqual(SrcFile.Path, MoveInDestCollection.Path)
        Assert.AreEqual(SrcFile.FullName, MoveInDestCollection.FullName)
        Assert.AreEqual(RemoteTestDirDestCollection.Path, SrcFile.ParentDirectory.Path)
        Assert.AreEqual(RemoteTestDirDestCollection.FullName, SrcFile.ParentDirectory.FullName)

        SrcFile.Move(RemoteTestDirDestFolder1)
        Dim MoveInDestFolder1 As IO.FileInfo = RemoteTestDirDestFolder1.GetFile("test-source-file")
        Assert.AreEqual(SrcFile.ID, MoveInDestFolder1.ID)
        Assert.AreEqual(SrcFile.Path, MoveInDestCollection.Path)
        Assert.AreEqual(SrcFile.FullName, MoveInDestCollection.FullName)
        Assert.AreEqual(MoveInDestFolder1.Path, SrcFile.ParentDirectory.Path)
        Assert.AreEqual(MoveInDestFolder1.FullName, SrcFile.ParentDirectory.FullName)

        SrcFile.Move(RemoteTestDirDestFolder2)
        Dim MoveInDestFolder2 As IO.FileInfo = RemoteTestDirDestFolder2.GetFile("test-source-file")
        Assert.AreEqual(SrcFile.ID, MoveInDestFolder2.ID)
        Assert.AreEqual(SrcFile.Path, MoveInDestCollection.Path)
        Assert.AreEqual(SrcFile.FullName, MoveInDestCollection.FullName)
        Assert.AreEqual(MoveInDestFolder2.Path, SrcFile.ParentDirectory.Path)
        Assert.AreEqual(MoveInDestFolder2.FullName, SrcFile.ParentDirectory.FullName)

        '2022-05-23 JW: Implementation review required
        If False Then
            Assert.IsFalse(RemoteTestDirDestCollection.FileExists("test-source-file"))
            Assert.IsFalse(RemoteTestDirDestFolder1.FileExists("test-source-file"))
            Assert.IsTrue(RemoteTestDirDestFolder2.FileExists("test-source-file"))

            RemoteTestDirDestCollection.ResetFilesCache()
            RemoteTestDirDestFolder1.ResetFilesCache()
            RemoteTestDirDestFolder2.ResetFilesCache()

            Assert.IsFalse(RemoteTestDirDestCollection.FileExists("test-source-file"))
            Assert.IsFalse(RemoteTestDirDestFolder1.FileExists("test-source-file"))
            Assert.IsTrue(RemoteTestDirDestFolder2.FileExists("test-source-file"))
            Assert.AreEqual(SrcFile.ID, RemoteTestDirDestFolder2.GetFile("test-source-file").ID)
            Assert.AreEqual(TestFile1Data, StreamToByteArray(RemoteTestDirDestFolder2.GetFile("test-source-file").Download()))
        End If

        Me.RemoveRemoteTestFolder(RemoteTestFolderNameSrc, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestFolder1, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestFolder2, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestCollection, True)
    End Sub

    ''' <summary>
    ''' Move a directory
    ''' </summary>
    <Test> Public Sub MoveDirectory()
        Const RemoteTestFolderNameSrc As String = "ZZZ_UnitTest_CenterDevice_TempDir_MoveDir_Src"
        Const RemoteTestFolderNameDestCollection As String = "ZZZ_UnitTest_CenterDevice_TempDir_MoveDir_Dest"
        Const RemoteTestFolderNameDestFolder1 As String = "ZZZ_UnitTest_CenterDevice_TempDir_MoveDir_Dest/subfolder1"
        Const RemoteTestFolderNameDestFolder2 As String = "ZZZ_UnitTest_CenterDevice_TempDir_MoveDir_Dest/subfolder2"
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameSrc, False)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestCollection, False)

        Dim RemoteTestDirSrc As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameSrc)
        Dim RemoteTestDirDestCollection As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestCollection)
        Dim RemoteTestDirDestFolder1 As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestFolder1)
        Dim RemoteTestDirDestFolder2 As IO.DirectoryInfo = Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderNameDestFolder2)

        Dim TestFile1Data As Byte() = New Byte() {55, 56, 32, 42, 13, 10, 44, 48, 50}

        Using TempFileData As New System.IO.MemoryStream(TestFile1Data)
            RemoteTestDirDestFolder2.UploadAndCreateNewFile(Function() TempFileData, "test-source-file")
        End Using
        Dim SrcFile As IO.FileInfo = RemoteTestDirDestFolder2.GetFile("test-source-file")

        Console.WriteLine("RemoteTestDirDestCollection=" & RemoteTestDirDestCollection.FullName)
        Console.WriteLine("RemoteTestDirDestCollection.CollectionID=" & RemoteTestDirDestCollection.CollectionID)

        Assert.Catch(Of NotSupportedException)(Sub()
                                                   RemoteTestDirSrc.Move(RemoteTestDirDestCollection)
                                               End Sub)
        Assert.Catch(Of NotSupportedException)(Sub()
                                                   RemoteTestDirSrc.Move(RemoteTestDirDestFolder1)
                                               End Sub)

        RemoteTestDirDestFolder2.Move(RemoteTestDirDestFolder1)
        Assert.AreEqual(RemoteTestDirDestFolder1.Path, RemoteTestDirDestFolder2.ParentDirectory.Path)
        Assert.AreEqual(RemoteTestDirDestFolder1.FullName, RemoteTestDirDestFolder2.ParentDirectory.FullName)

        RemoteTestDirDestFolder2.Rename(RemoteTestDirDestFolder2.Name.Replace("2", "3"))
        Assert.AreEqual(RemoteTestDirDestFolder1.Path, RemoteTestDirDestFolder2.ParentDirectory.Path)
        Assert.AreEqual(RemoteTestDirDestFolder1.FullName, RemoteTestDirDestFolder2.ParentDirectory.FullName)
        Dim ExpectedFullNameAfterRename As String = Me.IOClient.Paths.CombinePath(RemoteTestDirDestFolder1.Path, RemoteTestDirDestFolder2.Name.Replace("2", "3"))
        Assert.AreEqual(ExpectedFullNameAfterRename, RemoteTestDirDestFolder2.FullName)

        Dim ReloadedDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ExpectedFullNameAfterRename)
        Assert.IsNotNull(ReloadedDir)
        Assert.AreEqual(ExpectedFullNameAfterRename, ReloadedDir.FullName)
        Assert.IsTrue(Me.IOClient.RootDirectory.DirectoryExists(ExpectedFullNameAfterRename), "Expected directory: " & ExpectedFullNameAfterRename)

        RemoteTestDirDestCollection.ResetDirectoriesCache()
        RemoteTestDirDestFolder1.ResetDirectoriesCache()
        Me.IOClient.RootDirectory.ResetDirectoriesCache()
        Dim FoundSubDirs As String() = RemoteTestDirDestFolder1.GetDirectories().Select(Of String)(Function(value)
                                                                                                       Return value.FullName
                                                                                                   End Function).ToArray
        ReloadedDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ExpectedFullNameAfterRename)
        Assert.IsNotNull(ReloadedDir)
        Assert.AreEqual(ExpectedFullNameAfterRename, ReloadedDir.FullName)
        Assert.IsTrue(Me.IOClient.RootDirectory.DirectoryExists(ExpectedFullNameAfterRename), "Expected directory: " & ExpectedFullNameAfterRename & ", but only found" & System.Environment.NewLine &
                      Strings.Join(FoundSubDirs,
                                   System.Environment.NewLine)
                                   )

        '2022-05-23 JW: Implementation review required
        If False Then
            Dim MoveInDestCollection As IO.FileInfo = RemoteTestDirDestCollection.GetFile("test-source-file")
            Assert.AreEqual(SrcFile.ID, MoveInDestCollection.ID)
            Assert.AreEqual(SrcFile.Path, MoveInDestCollection.Path)
            Assert.AreEqual(SrcFile.FullName, MoveInDestCollection.FullName)
            Assert.AreEqual(RemoteTestDirDestCollection.Path, SrcFile.ParentDirectory.Path)

            SrcFile.Move(RemoteTestDirDestFolder1)
            Dim MoveInDestFolder1 As IO.FileInfo = RemoteTestDirDestFolder1.GetFile("test-source-file")
            Assert.AreEqual(SrcFile.ID, MoveInDestFolder1.ID)
            Assert.AreEqual(SrcFile.Path, MoveInDestCollection.Path)
            Assert.AreEqual(SrcFile.FullName, MoveInDestCollection.FullName)
            Assert.AreEqual(MoveInDestFolder1.Path, SrcFile.ParentDirectory.Path)

            SrcFile.Move(RemoteTestDirDestFolder2)
            Dim MoveInDestFolder2 As IO.FileInfo = RemoteTestDirDestFolder2.GetFile("test-source-file")
            Assert.AreEqual(SrcFile.ID, MoveInDestFolder2.ID)
            Assert.AreEqual(SrcFile.Path, MoveInDestCollection.Path)
            Assert.AreEqual(SrcFile.FullName, MoveInDestCollection.FullName)
            Assert.AreEqual(MoveInDestFolder2.Path, SrcFile.ParentDirectory.Path)

            Assert.IsFalse(RemoteTestDirDestCollection.FileExists("test-source-file"))
            Assert.IsFalse(RemoteTestDirDestFolder1.FileExists("test-source-file"))
            Assert.IsTrue(RemoteTestDirDestFolder2.FileExists("test-source-file"))

            RemoteTestDirDestCollection.ResetFilesCache()
            RemoteTestDirDestFolder1.ResetFilesCache()
            RemoteTestDirDestFolder2.ResetFilesCache()

            Assert.IsFalse(RemoteTestDirDestCollection.FileExists("test-source-file"))
            Assert.IsFalse(RemoteTestDirDestFolder1.FileExists("test-source-file"))
            Assert.IsTrue(RemoteTestDirDestFolder2.FileExists("test-source-file"))
            Assert.AreEqual(SrcFile.ID, RemoteTestDirDestFolder2.GetFile("test-source-file").ID)
            Assert.AreEqual(TestFile1Data, StreamToByteArray(RemoteTestDirDestFolder2.GetFile("test-source-file").Download()))
        End If

        Me.RemoveRemoteTestFolder(RemoteTestFolderNameSrc, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestFolder1, True)
        Me.RemoveRemoteTestFolder(RemoteTestFolderNameDestCollection, True)
    End Sub

    Private Function StreamToByteArray(stream As System.IO.Stream) As Byte()
        Using MemStream As New System.IO.MemoryStream()
            stream.CopyTo(MemStream)
            Return MemStream.ToArray
        End Using
    End Function

    'TODO: adding tests for:
    'Dim CreatedLink As LinkCreationResponse
    'Select Case dmsResource.ItemType
    '    Case DmsResourceItem.ItemTypes.Collection
    '        CreatedLink = Me.IOClient.ApiClient.Links.CreateCollectionLink(Me.IOClient.CurrentAuthenticationContextUserID, dmsResource.ExtendedInfosCollectionID, AccessControl)
    '    Case DmsResourceItem.ItemTypes.Folder
    '        CreatedLink = Me.IOClient.ApiClient.Links.CreateFolderLink(Me.IOClient.CurrentAuthenticationContextUserID, dmsResource.ExtendedInfosFolderID, AccessControl)
    '    Case DmsResourceItem.ItemTypes.File
    '        CreatedLink = Me.IOClient.ApiClient.Links.CreateDocumentLink(Me.IOClient.CurrentAuthenticationContextUserID, dmsResource.ExtendedInfosFileID, AccessControl)
    '    Case DmsResourceItem.ItemTypes.Root
    '        Throw New NotSupportedException("Sharing for root directory not supported")
    '    Case Else
    '        Throw New NotImplementedException("Invalid item type: " & dmsResource.ItemType)
    'End Select

End Class
