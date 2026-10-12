using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.User
{
    public partial class UsersRestClient
    {
        /// <summary>Invites explicitly selected e-mail addresses to the current tenant.</summary>
        /// <inheritdoc cref="InviteUsersAsync"/>
        public UserInvitationResult InviteUsers(string userId, IEnumerable<string> emails, CancellationToken cancellationToken = default(CancellationToken))
            => InviteUsersAsync(userId, emails, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Invites explicitly selected e-mail addresses using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="emails">The nonempty address selection, captured once before authorization. Original spelling, duplicates and invalid syntax remain available for server-specific diagnostics.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Read-only successful invitations and exact per-address errors from HTTP 200.</returns>
        /// <remarks>Only admins and internal users may invite. Confirmation is required and the default invited role is guest. The manufacturer calls this action idempotent only while invitations remain unaccepted; the SDK adds no uncertain-write or partial-failure replay. Existing expired-token refresh is preserved. Cancellation cannot establish that no invitations were created. No existing user interfaces, listing defaults or DMS permissions change.</remarks>
        /// <exception cref="ArgumentNullException">The address selection is null.</exception>
        /// <exception cref="ArgumentException">The selection is empty or contains a null address.</exception>
        /// <exception cref="InvalidResponseDataException">The response is malformed, contains unselected addresses or omits selected outcomes.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<UserInvitationResult> InviteUsersAsync(string userId, IEnumerable<string> emails, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (emails == null) throw new ArgumentNullException(nameof(emails));
            var selected = emails.ToArray();
            if (selected.Length == 0 || selected.Any(email => email == null))
                throw new ArgumentException("Select at least one nonnull e-mail address.", nameof(emails));
            var request = CreateRestRequest(ApiVersionPrefix + "users", Method.Post, "application/json");
            request.AddJsonBody(new { action = "invite", @params = new { emails = selected } });
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            try
            {
                JObject data;
                using (var text = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                {
                    data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Unexpected content follows the invitation response.");
                }
                if (!(data["invited"] is JArray success) || !(data["not-invited"] is JArray failed))
                    throw new InvalidDataException("Both invitation outcome arrays are required.");
                var invited = new List<UserInvitation>(); var failures = new List<UserInvitationFailure>();
                var reported = new List<string>();
                foreach (var entry in success)
                {
                    var email = ReadInvitationAddress(entry, selected);
                    if (entry["user-id"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(entry.Value<string>("user-id")))
                        throw new InvalidDataException("Successful invitations must identify a new user.");
                    invited.Add(new UserInvitation(email, entry.Value<string>("user-id"))); reported.Add(email);
                }
                foreach (var entry in failed)
                {
                    var email = ReadInvitationAddress(entry, selected);
                    long code;
                    if (entry["code"]?.Type != JTokenType.Integer || !long.TryParse(entry["code"].ToString(Formatting.None),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out code))
                        throw new InvalidDataException("Invitation failures must have Int64 numeric codes.");
                    failures.Add(new UserInvitationFailure(email, code)); reported.Add(email);
                }
                if (selected.Any(email => !reported.Any(value => string.Equals(value, email, StringComparison.OrdinalIgnoreCase))))
                    throw new InvalidDataException("The server did not report an outcome for every selected address.");
                return new UserInvitationResult(invited, failures);
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException || error is OverflowException)
            { throw new InvalidResponseDataException("The user invitation response is invalid; do not replay the invitation automatically.", error); }
        }

        private static string ReadInvitationAddress(JToken item, string[] selected)
        {
            if (!(item is JObject data) || data["email"]?.Type != JTokenType.String)
                throw new InvalidDataException("Invitation outcomes must contain an e-mail string.");
            var email = data.Value<string>("email");
            if (!selected.Any(value => string.Equals(value, email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("An invitation outcome identifies an unselected address.");
            return email;
        }
    }
}
