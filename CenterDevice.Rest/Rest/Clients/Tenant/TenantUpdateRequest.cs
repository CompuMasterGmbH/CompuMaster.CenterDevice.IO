namespace CenterDevice.Rest.Clients.Tenant
{
    /// <summary>Selects tenant profile fields to update without changing omitted fields.</summary>
    /// <remarks>A logo is supplied as Base64-encoded PNG or JPEG data, limited to 5 MiB. The server scales oversized dimensions to at most 200 by 70 pixels while retaining aspect ratio. Removing a logo uses RemoveTenantLogo explicitly.</remarks>
    public sealed class TenantUpdateRequest
    {
        /// <summary>Gets or sets the new tenant name; null omits the field and empty text is sent unchanged.</summary>
        public string Name { get; set; }
        /// <summary>Gets or sets Base64-encoded logo image data; null preserves the existing logo.</summary>
        public string LogoData { get; set; }
    }
}
