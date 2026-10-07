using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    // RestSharp emits file parameters before BodyParameter metadata, regardless of insertion order.
    // Order only the two-part document upload shape. Original parts stay owned by the caller.
    internal sealed class MetadataFirstMultipartContent : MultipartFormDataContent
    {
        private MetadataFirstMultipartContent(MultipartFormDataContent original, string boundary, HttpContent metadata, HttpContent document)
            : base(boundary)
        {
            Add(new BorrowedPart(metadata));
            Add(new BorrowedPart(document));
            Headers.Clear();
            foreach (var header in original.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        internal static MetadataFirstMultipartContent CreateIfNeeded(HttpRequestMessage request)
        {
            if (request.Method != HttpMethod.Post || !(request.Content is MultipartFormDataContent multipart)) return null;
            var path = request.RequestUri.AbsolutePath;
            if (!path.EndsWith("/documents", StringComparison.Ordinal) && path.IndexOf("/document/", StringComparison.Ordinal) < 0) return null;
            var parts = multipart.ToArray();
            if (parts.Length != 2 || Name(parts[0]) != "document" || Name(parts[1]) != "metadata") return null;
            if (parts[1].Headers.ContentType?.MediaType != "application/json") return null;
            var boundary = multipart.Headers.ContentType?.Parameters.FirstOrDefault(p => p.Name == "boundary")?.Value?.Trim('"');
            return string.IsNullOrEmpty(boundary) ? null : new MetadataFirstMultipartContent(multipart, boundary, parts[1], parts[0]);
        }

        private static string Name(HttpContent part) => part.Headers.ContentDisposition?.Name?.Trim('"');

        // Disposing the temporary multipart view disposes these proxies, not their source contents.
        // The request is restored before the caller disposes its original multipart/streams.
        private sealed class BorrowedPart : HttpContent
        {
            private readonly HttpContent original;
            internal BorrowedPart(HttpContent original)
            {
                this.original = original;
                foreach (var header in original.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) => original.CopyToAsync(stream);
            protected override bool TryComputeLength(out long length)
            {
                var size = original.Headers.ContentLength;
                length = size ?? 0;
                return size.HasValue;
            }
        }
    }
}
