# Explicit own-profile updates

API 2.29 section 5.8.1 defines PUT user/{user-id} with optional first-name, last-name and nested picture.data. UserRestClient.UpdateUserDetails and its native async form capture these fields before authorization, preserving null omissions, empty names and original Base64 data. The authentication-context user and explicit resource ID remain separate; only the server can verify they identify the same caller. No ID or name is inferred or rewritten.

RemoveUserPicture and its native async form explicitly send an empty picture object. Null PictureData preserves the current image. Base64 syntax and the nonempty decoded 5 MiB limit are checked locally; actual PNG/JPEG validity, own-profile permissions and dimension scaling to 400 by 400 pixels are server-authoritative. Pictures are not transformed or written to disk.

The additive concrete API preserves existing interfaces/getters/default metadata. HTTP 204 confirms a command. Native cancellation covers authorization/admission/HTTP and cannot prove no change occurred. Original errors and inherited expired-token refresh remain; no uncertain mutation retry or automatic compensation is added.

Isolated and private loopback fixtures verify request/omission/snapshot/size/identity/cancellation/error/204/no replay contracts. They do not change actual user pictures/names or prove real own-profile access. Full user details, listing, invitation, picture downloads and user/tenant settings remain separate source scopes in #20; coordinated actual permission/module acceptance, publication and default DMS adoption remain open.
