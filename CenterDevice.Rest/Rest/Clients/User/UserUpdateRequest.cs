namespace CenterDevice.Rest.Clients.User
{
    /// <summary>Selects user profile fields to update without changing omitted fields.</summary>
    /// <remarks>Null fields are omitted. Empty name text is sent unchanged. Picture data must encode a PNG or JPEG of at most 5 MiB; the server validates image contents and scales dimensions to at most 400 by 400 pixels. Removing a picture uses RemoveUserPicture explicitly.</remarks>
    public sealed class UserUpdateRequest
    {
        /// <summary>Gets or sets the new first name; null preserves the existing value.</summary>
        public string FirstName { get; set; }
        /// <summary>Gets or sets the new last name; null preserves the existing value.</summary>
        public string LastName { get; set; }
        /// <summary>Gets or sets Base64-encoded picture data; null preserves the existing picture.</summary>
        public string PictureData { get; set; }
    }
}
