using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Folders;
using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using NUnit.Framework;
using RestSharp;
using RestSharp.Serializers.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class FolderMetadataTests
    {
        // Exercise RestSharp's default serializer, also used by CenterDeviceRestClient.
        private static T Read<T>(string json)
        {
            return new SystemTextJsonSerializer().Deserialize<T>(new RestResponse { Content = json, ContentType = "application/json" });
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        [TestCase("null", null)]
        public void ExplicitChildMetadataPreservesTrueFalseAndUnknown(string value, bool? expected)
        {
            var folder = Read<Folder>("{\"id\":\"folder\",\"has-subfolders\":" + value + "}");
            Assert.That(folder.HasSubFolders, Is.EqualTo(expected));
            Assert.That(folder.SubFolders, Is.Null, "Child existence does not manufacture fetched children.");
        }

        [Test]
        public void OmittedChildrenRemainUnknownForLazyLoading()
        {
            var folder = Read<Folder>("{\"id\":\"folder\",\"name\":\"Folder\"}");
            Assert.That(folder.HasSubFolders, Is.Null);
            Assert.That(folder.SubFolders, Is.Null);
            Assert.That(folder.Documents, Is.Null);
        }

        [TestCase("true", false)]
        [TestCase("false", false)]
        [TestCase("null", false)]
        [TestCase("true", true)]
        [TestCase("false", true)]
        [TestCase("null", true)]
        public void ExplicitlyFetchedChildrenOverrideServerExistenceMetadata(string value, bool populated)
        {
            var folder = Read<Folder>("{\"has-subfolders\":" + value + "}");
            folder.SubFolders = populated ? new List<Folder> { new Folder { Id = "child" } } : new List<Folder>();
            Assert.That(folder.HasSubFolders, Is.EqualTo(populated));
            folder.SubFolders = null;
            Assert.That(folder.HasSubFolders, Is.EqualTo(value == "null" ? (bool?)null : value == "true"));
        }

        [Test]
        public void FolderListingKeepsIndependentChildAndGuestVisibilityMetadata()
        {
            var result = Read<FoldersResponse>("{\"folders\":[{\"id\":\"known-empty\",\"has-subfolders\":false,\"parent\":\"none\",\"collection\":\"collection\",\"path\":[\"root\",\"leaf\"],\"users\":{\"visible\":[\"user-id\"],\"not-visible-count\":2},\"groups\":{\"visible\":[],\"not-visible-count\":1}},{\"id\":\"has-children\",\"has-subfolders\":true},{\"id\":\"guest\"}]}");
            Assert.That(result.Folders.Count, Is.EqualTo(3));
            var known = result.Folders[0];
            Assert.That(known.HasSubFolders, Is.False);
            Assert.That(known.IsRootFolder, Is.True);
            Assert.That(known.Path, Is.EqualTo(new[] { "root", "leaf" }));
            Assert.That(known.Collection, Is.EqualTo("collection"));
            Assert.That(known.Users.Visible, Is.EqualTo(new[] { "user-id" }));
            Assert.That(known.Users.NotVisibleCount, Is.EqualTo(2));
            Assert.That(known.Groups.Visible, Is.Empty);
            Assert.That(known.Groups.NotVisibleCount, Is.EqualTo(1));
            Assert.That(known.IsShared, Is.True);
            Assert.That(result.Folders[1].HasSubFolders, Is.True);
            var guest = result.Folders[2];
            Assert.That(guest.HasSubFolders, Is.Null);
            Assert.That(guest.Parent, Is.Null);
            Assert.That(guest.Collection, Is.Null);
            Assert.That(guest.Path, Is.Null);
            Assert.That(guest.Users, Is.Null);
            Assert.That(guest.Groups, Is.Null);
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        [TestCase("null", null)]
        public void CollectionChildMetadataRetainsItsExistingBinding(string value, bool? expected)
        {
            var collection = Read<Collection>("{\"id\":\"collection\",\"has-folders\":" + value + ",\"owner\":\"not-visible\",\"archived-date\":\"2025-09-10T12:30:00Z\"}");
            Assert.That(collection.HasFolders, Is.EqualTo(expected));
            Assert.That(collection.Owner, Is.EqualTo("not-visible"));
            Assert.That(collection.Archived, Is.True);
            Assert.That(collection.ArchivedDate.Value.Year, Is.EqualTo(2025));
            Assert.That(collection.SubFolders, Is.Null);
            Assert.That(collection.HasDocuments, Is.Null);
        }

        [Test]
        public void CollectionOmittedOwnerArchiveAndChildrenRemainUnknown()
        {
            var collection = Read<Collection>("{\"id\":\"collection\"}");
            Assert.That(collection.HasFolders, Is.Null);
            Assert.That(collection.HasDocuments, Is.Null);
            Assert.That(collection.Owner, Is.Null);
            Assert.That(collection.ArchivedDate, Is.Null);
            collection.SubFolders = new List<Folder>();
            Assert.That(collection.HasFolders, Is.False);
        }

        [TestCase(false, false), TestCase(true, false)]
        [TestCase(false, true), TestCase(true, true)]
        public async Task FolderGetPreservesDefaultFieldsAndBindsExplicitlyRequestedChildMetadata(bool sync, bool requestChildren)
        {
            var client = new FolderClient();
            var fields = requestChildren ? new[] { "id", "has-subfolders" } : null;
            var folder = sync ? client.GetFolder("selected-context", "selected-folder", fields)
                : await client.GetFolderAsync("selected-context", "selected-folder", fields);
            Assert.That(client.Request.Resource, Is.EqualTo("v2/folder/selected-folder"));
            Assert.That(client.Request.Method, Is.EqualTo(Method.Get));
            Assert.That(client.UserId, Is.EqualTo("selected-context"));
            var parameter = client.Request.Parameters.SingleOrDefault(p => p.Name == "fields");
            if (requestChildren) Assert.That(parameter.Value, Is.EqualTo("id,has-subfolders"));
            else Assert.That(parameter, Is.Null, "Existing default metadata requests stay unchanged.");
            Assert.That(folder.HasSubFolders, Is.False);
            Assert.That(folder.SubFolders, Is.Null);
        }

        private sealed class Configuration : IRestClientConfiguration
        {
            public string BaseAddress => "https://isolated-folder-metadata.invalid/";
            public string UserAgent => "folder-metadata-tests";
        }

        private sealed class Authorization : IAsyncOAuthInfoProvider
        {
            public OAuthInfo GetOAuthInfo(string userId) => new OAuthInfo { UserId = userId, access_token = "isolated-token" };
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken)) => Task.FromResult(GetOAuthInfo(userId));
        }

        private sealed class FolderClient : FolderRestClient
        {
            internal FolderClient() : base(new Authorization(), new Configuration(), null, "v2/") { }
            internal RestRequest Request;
            internal string UserId;
            protected override Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken cancellationToken = default(CancellationToken))
            {
                Request = request;
                UserId = info.UserId;
                var response = new RestResponse(request) { Content = "{\"id\":\"selected-folder\",\"has-subfolders\":false}", ContentType = "application/json", StatusCode = HttpStatusCode.OK };
                return Task.FromResult(new RestResponse<T>(request) { StatusCode = response.StatusCode, Data = client.Serializers.DeserializeContent<T>(response) });
            }
        }
    }
}
