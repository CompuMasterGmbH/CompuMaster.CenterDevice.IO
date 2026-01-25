#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.OAuth
{
    public class OAuthInfo
    {
        public OAuthInfo()
        {
        }

        public OAuthInfo(string loginEMailAddress, OAuthInfo token)
        {
            this.Email = loginEMailAddress;
            //TODO: what to do with token?
        }

        //public OAuthInfo(string loginEMailAddress, CenterDevice.Rest.Model.TokenResponse centerDeviceToken)
        //{
        //}

        public string UserId { get; set; }
        public string Email { get; set; }
        public string TenantId { get; set; }
        public string token_type { get; set; }
        public string access_token { get; set; }
        public long expires_in { get; set; }
        public string refresh_token { get; set; }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element