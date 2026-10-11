namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Specifies the document contents requested inside a ZIP download.</summary>
    public enum DocumentDownloadRepresentation
    {
        /// <summary>Downloads the original current document contents.</summary>
        Original = 0,
        /// <summary>Downloads existing PDF representations without creating them on demand.</summary>
        Pdf = 1
    }
}
