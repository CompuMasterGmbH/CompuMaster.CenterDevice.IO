using RestSharp;
using System;
using System.Linq;
using System.Threading;

namespace CenterDevice.Rest.Clients
{
    public abstract partial class CenterDeviceRestClient
    {
        internal RestRequest CreateFilteredListingRequest(string resource, string filter, string value, string[] fields, bool folder, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("The listing filter is missing.", nameof(value));
            var snapshot = fields == null ? null : (string[])fields.Clone();
            if (snapshot != null && snapshot.Any(field => string.IsNullOrWhiteSpace(field) || field.Contains(",")))
                throw new ArgumentException("Each field must contain one nonempty field name.", nameof(fields));
            if (folder && snapshot != null && snapshot.Any(field => field == "full-path" || field == "inherited-sharings"))
                throw new NotSupportedException("This folder listing does not support full-path or inherited-sharings. Full-path requires the top-most shared-folder query.");
            var request = CreateRestRequest(resource, Method.Get);
            request.AddQueryParameter(filter, value);
            if (snapshot != null) request.AddQueryParameter("fields", string.Join(",", snapshot));
            return request;
        }
    }
}
