namespace CenterDevice.Rest
{
    public class RestClientConfiguration : IRestClientConfiguration
    {
        /// <summary>
        /// Default configuration for accessing the token endpoint of CenterDevice API
        /// </summary>
        public RestClientConfiguration(string userAgent)
        {
            this.BaseAddress = "https://auth.centerdevice.de/token";
            this.UserAgent = userAgent;
        }

        /// <summary>
        /// Custom configuration for accessing the token endpoint of CenterDevice API
        /// </summary>
        public RestClientConfiguration(string baseAddress, string userAgent)
        {
            this.BaseAddress = baseAddress;
            this.UserAgent = userAgent;
        }

        public string BaseAddress { get; set; }

        public string UserAgent { get; set; }

        string IRestClientConfiguration.BaseAddress => this.BaseAddress;

        string IRestClientConfiguration.UserAgent => this.UserAgent;
    }
}
