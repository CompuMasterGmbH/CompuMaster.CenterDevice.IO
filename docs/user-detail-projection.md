# Correct user detail projection

API 2.29 section 5.8.2 declares GET user/{user-id}?data=... . The existing GetUserDataField and native asynchronous counterpart incorrectly sent fields, a parameter used for other resources, instead of the user-specific data parameter. Servers could ignore that unknown parameter and return the default all-field projection.

Both existing forms now send the exact requested value under data. Method signatures, selected/context user IDs, models, authorization, native cancellation and other getter defaults remain unchanged; the shared FIELDS constant remains unchanged for resources that legitimately use it. This fixes explicit user projections without changing the default logged-in-user workflow.

Manufacturer examples include id/first-name/last-name/email, groups, collections, picture and all. The server decides which details are visible; groups/collections are only available for the current user. This query correction does not grant visibility or establish complete modern user model coverage.

Fourteen regression cases cover sync/native projections, cancellation and two actual private loopback factory requests. Twelve cases fail with the original fields parameter, including both actual HTTP wire cases; only the two cancellation cases pass before the fix. Actual server role/field visibility and broader typed metadata/list/settings remain separate under #20/#15. No GUI, shared server or user data mutation is needed for this correction.
