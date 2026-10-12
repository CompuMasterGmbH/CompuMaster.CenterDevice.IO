# User picture downloads

`UserRestClient.DownloadUserPicture` and `DownloadUserPictureAsync` implement manufacturer API 2.29 section 5.8.4, `GET user/<user-id>/picture`.

The authentication context and selected user ID are separate. The server enforces whether the selected user is visible and whether a picture is available. User IDs are escaped as one path segment, preserving configured Teamwork bridge paths and resource prefixes. Existing interfaces and user-detail getters remain unchanged.

Both entry points return a response-owned stream containing the original PNG or JPEG bytes. Dispose that stream after reading it. The asynchronous method uses native streaming HTTP I/O and does not buffer the picture or convert it to Base64. Cancellation covers authorization, origin admission, HTTP I/O and the returned stream. Redirects are not followed. HTTP failures remain failures; a missing image does not become an empty successful result. Existing bounded expired-token refresh behavior is inherited from the resource download transport.

The reference specifies no resize, range or image-conversion parameter for this endpoint; these methods introduce none. User-picture upload size constraints are not applied to downloads.

Isolated tests cover MIME validation, original bytes, response ownership, cancellation, selected identity, paths, HTTP failures and private TCP loopback requests. They do not establish real user visibility, direct CenterDevice authorization or Teamwork server compatibility. Those acceptance checks remain tracked separately in issues #20 and #15.
