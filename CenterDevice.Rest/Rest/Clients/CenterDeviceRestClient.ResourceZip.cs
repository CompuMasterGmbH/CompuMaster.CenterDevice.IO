using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    public abstract partial class CenterDeviceRestClient
    {
        internal Task<Stream> OpenResourceZipAsync(string userId, string resource, string id, object body, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..")
                throw new ArgumentException("The resource identifier is missing or is a relative path segment.", nameof(id));
            var uri = BuildResourceDownloadUri(ApiVersionPrefix + resource + "/" + Uri.EscapeDataString(id));
            var json = JsonConvert.SerializeObject(body);
            return OpenResourceDownloadAsync(userId, () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/zip"));
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return request;
            }, HttpStatusCode.OK, new[] { "application/zip" }, false, token);
        }
    }
}
