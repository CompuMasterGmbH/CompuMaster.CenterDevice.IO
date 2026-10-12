using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Workflows
{
    /// <summary>Executes explicitly selected direct workflow commands using existing authorization and transport.</summary>
    /// <remarks>Participation, visibility, creator rights, completion state, metadata requirements and finalizing/sharing side effects remain server-enforced. No state is inferred or fetched to authorize a command.</remarks>
    public class WorkflowsRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes a workflow client using existing authorization and transport.</summary>
        /// <param name="oauthInfoProvider">The authorization provider.</param>
        /// <param name="configuration">The origin and user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing resource prefix.</param>
        public WorkflowsRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>Assigns the caller to an explicitly selected workflow.</summary>
        /// <inheritdoc cref="AssignToWorkflowAsync"/>
        public void AssignToWorkflow(string userId, string workflowId, CancellationToken cancellationToken = default(CancellationToken))
            => AssignToWorkflowAsync(userId, workflowId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Assigns the caller to an explicitly selected workflow using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="workflowId">The exact selected nonblank workflow identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed assignment.</returns>
        /// <remarks>The caller must be a visible workflow participant. Assignment requires a started metadata-form workflow with no other assignee. The manufacturer defines this command as idempotent; the existing transport conservatively does not replay POST writes.</remarks>
        /// <exception cref="ArgumentException">The workflow identity is unusable.</exception>
        public Task AssignToWorkflowAsync(string userId, string workflowId, CancellationToken cancellationToken = default(CancellationToken))
            => CommandAsync(userId, workflowId, "assign", null, cancellationToken);

        /// <summary>Unassigns the caller from an explicitly selected workflow.</summary>
        /// <inheritdoc cref="UnassignFromWorkflowAsync"/>
        public void UnassignFromWorkflow(string userId, string workflowId, CancellationToken cancellationToken = default(CancellationToken))
            => UnassignFromWorkflowAsync(userId, workflowId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Unassigns the caller from an explicitly selected workflow using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="workflowId">The exact selected nonblank workflow identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed unassignment.</returns>
        /// <remarks>The caller must be a visible workflow participant. The manufacturer defines this command as idempotent; the existing transport conservatively does not replay POST writes. No undocumented current assignment/type restriction is inferred locally.</remarks>
        /// <exception cref="ArgumentException">The workflow identity is unusable.</exception>
        public Task UnassignFromWorkflowAsync(string userId, string workflowId, CancellationToken cancellationToken = default(CancellationToken))
            => CommandAsync(userId, workflowId, "unassign", null, cancellationToken);

        /// <summary>Responds to an explicitly selected workflow for a document version.</summary>
        /// <inheritdoc cref="RespondToWorkflowAsync"/>
        public void RespondToWorkflow(string userId, string workflowId, string result, long documentVersion, string comment = null, CancellationToken cancellationToken = default(CancellationToken))
            => RespondToWorkflowAsync(userId, workflowId, result, documentVersion, comment, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Responds to an explicitly selected workflow using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="workflowId">The exact selected nonblank workflow identifier.</param>
        /// <param name="result">The current confirmed or rejected result; approval-only rejection remains server-validated.</param>
        /// <param name="documentVersion">The explicit positive version on which this response is based.</param>
        /// <param name="comment">The exact optional plain text; null omits it and empty text remains explicit.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed response.</returns>
        /// <remarks>The caller must participate in the visible, incomplete workflow; metadata-form requires document metadata fields. A response can complete the workflow, execute finalizing actions and remove created sharings. It uses params, unlike template-use data. State and permissions remain server-enforced; uncertain outcomes are not replayed.</remarks>
        /// <exception cref="ArgumentException">The workflow identity, result or version selection is invalid.</exception>
        public Task RespondToWorkflowAsync(string userId, string workflowId, string result, long documentVersion, string comment = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); RequireId(workflowId);
            if (result != "confirmed" && result != "rejected") throw new ArgumentException("A current workflow response result is required.", nameof(result));
            if (documentVersion <= 0) throw new ArgumentOutOfRangeException(nameof(documentVersion));
            var parameters = new JObject { ["result"] = result, ["document-version"] = documentVersion }; if (comment != null) parameters["comment"] = comment;
            return CommandAsync(userId, workflowId, "respond", parameters, cancellationToken);
        }

        /// <summary>Removes an explicitly selected workflow as its creator.</summary>
        /// <inheritdoc cref="RemoveWorkflowAsync"/>
        public void RemoveWorkflow(string userId, string workflowId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveWorkflowAsync(userId, workflowId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Removes an explicitly selected workflow using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="workflowId">The exact selected nonblank workflow identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed removal.</returns>
        /// <remarks>Only the creator can remove a workflow, which must remain visible to the creator. Completed and incomplete workflows may be removed; document visibility, timeline and other server side effects are not inferred locally. Uncertain outcomes are not replayed.</remarks>
        /// <exception cref="ArgumentException">The workflow identity is unusable.</exception>
        public Task RemoveWorkflowAsync(string userId, string workflowId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); return SendAsync(userId, Resource(workflowId), Method.Delete, null, cancellationToken); }

        private Task CommandAsync(string userId, string workflowId, string action, JObject parameters, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); var resource = Resource(workflowId); var body = new JObject { ["action"] = action };
            if (parameters != null) body["params"] = (JObject)parameters.DeepClone(); return SendAsync(userId, resource, Method.Post, body, token);
        }
        private string Resource(string id) { RequireId(id); return ApiVersionPrefix + "workflow/" + Uri.EscapeDataString(id); }
        private static void RequireId(string id) { if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("A usable selected workflow identity is required.", nameof(id)); }
        private async Task SendAsync(string userId, string resource, Method method, JObject body, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); var request = CreateRestRequest(resource, method); if (body != null) request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
