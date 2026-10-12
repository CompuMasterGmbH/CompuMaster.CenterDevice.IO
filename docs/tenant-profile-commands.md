# Tenant profile and explicit default commands

API 2.29 sections 5.12.1 and 5.12.4 define PUT tenant profile updates and POST tenants/set-default-tenant.

TenantRestClient.UpdateTenant and UpdateTenantAsync select a name and/or Base64 logo data. Null fields are omitted, empty names are sent unchanged, and nested logo data is captured before I/O. Base64 syntax and the nonempty decoded 5 MiB size bound are validated locally; actual PNG/JPEG validity and dimension scaling (maximum 200 by 70 pixels) are server-authoritative. Updating a logo never guesses a MIME type, modifies pixels or writes a local file.

RemoveTenantLogo and RemoveTenantLogoAsync explicitly send an empty logo object. Null LogoData merely preserves the existing image. Only an admin may update or remove tenant profile fields; the token's tenant is addressable through current.

TenantsRestClient.SetDefaultTenant and its native async form expressly change the current login's default-tenant preference. They do not change an existing client's OAuth context, automatically choose a tenant, reorder existing tenant results or alter any DMS default-instance behavior. The server validates login membership. Existing interfaces/getters/models remain unchanged.

All commands confirm HTTP 204, preserve original HTTP errors and native cancellation, and inherit the existing expired-token refresh. Uncertain writes are not retried. Cancellation does not prove the absence of changes. Isolated fixtures and private loopback probes cannot prove actual admin permissions, tenant membership, image acceptance, or Teamwork exposure; those require separate coordinated acceptance. No real tenant profile/default is changed during local verification, and no publication or default DMS dependency change is implied.
