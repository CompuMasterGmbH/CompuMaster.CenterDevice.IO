using System;
using System.IO;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Provides an unbuffered preview body and its negotiated content type.</summary>
    /// <remarks>Single-page bodies contain an image; multiple-page bodies contain multipart/form-data with page-number part names. Requested pages can be missing. The caller must inspect the multipart parts rather than infer that every requested page was returned.</remarks>
    public sealed class DocumentPreviewDownload : IDisposable
    {
        internal DocumentPreviewDownload(Stream content, string contentType) { Content = content; ContentType = contentType; }
        /// <summary>Gets the response-owned image or multipart body.</summary>
        /// <remarks>Disposing this download disposes the stream and releases the response. Cancellation remains active while reading.</remarks>
        public Stream Content { get; }
        /// <summary>Gets the negotiated MIME header, including the boundary for a multipart response.</summary>
        public string ContentType { get; }
        /// <summary>Disposes the body and its owning response.</summary>
        public void Dispose() => Content.Dispose();
    }
}
