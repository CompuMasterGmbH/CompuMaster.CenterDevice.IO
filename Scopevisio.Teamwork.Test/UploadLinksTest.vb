Imports NUnit.Framework

<TestFixture> Public Class UploadLinksTest
    Inherits TestBase

    <Test> Public Sub GetAllUploadLinks()
        Dim CheckResult As CenterDevice.Rest.Clients.Link.UploadLinks
        Try
            CheckResult = IOClient.TeamworkRestClient.UploadLinks.GetAllUploadLinks(IOClient.CurrentContextUserId)
            System.Console.WriteLine(CheckResult)
            System.Console.WriteLine(CheckResult.UploadLinksList)
            System.Console.WriteLine(CheckResult.UploadLinksList.Count)
            System.Console.WriteLine(CheckResult.UploadLinksList.Item(0).Name & "=" & CheckResult.UploadLinksList.Item(0).Web)
            Assert.NotNull(CheckResult)
            Assert.NotNull(CheckResult.UploadLinksList)
            Assert.NotZero(CheckResult.UploadLinksList.Count)
        Catch ex As CenterDevice.Rest.Exceptions.RestClientException
            Assert.Fail(ex.ToString + System.Environment.NewLine + ex.ResultBody)
        End Try

    End Sub

End Class
