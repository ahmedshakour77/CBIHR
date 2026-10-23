# API.HR.Node Application Review

Rechecked: 2026-08-29

## Executive summary

The application builds successfully and all 26 automated tests pass. Several issues from the first review have been meaningfully improved: permission checks now deny by default, user/device/audit pages and import endpoints have explicit policies, photo replacement has a separate permission check, and workflow action persistence now uses a database transaction.

The application is still not ready for production. Live credentials, a fallback administrator password, an unencrypted data-protection key, and employee photos remain in the source tree. Startup code also creates or upgrades two hard-coded identities to full administrator access. In addition, the workflow, passkey-administration, and session-administration Blazor pages still require only authentication, and the workflow engine still does not verify task ownership or transition permissions.

The first response should be credential/key rotation and removal of sensitive runtime data, followed by closing the remaining UI and workflow authorization gaps.

## Review scope

This was a refreshed static review of the current ASP.NET Core/Blazor application, REST endpoints, EF Core model, authentication/authorization code, profile-photo pipeline, workflow engine, configuration, and tests. I ran a Release build and the complete existing test assembly. I did not connect to or modify the live database and did not perform external penetration testing.

Severity meanings:

- **Critical:** direct compromise of credentials, accounts, cryptographic trust, or privileged administration.
- **High:** practical bypass of a stated security control or serious business-data integrity risk.
- **Medium:** meaningful security hardening, reliability, privacy, or maintainability issue.

## Progress since the first review

The following changes are confirmed in the current code:

- `PermissionAuthorizationHandler` now denies authenticated users without the required permission (`API.HR.Node/Security/PermissionAuthorizationHandler.cs:29-37`). The corresponding regression test now expects denial.
- `/users`, `/user-devices`, and `/audit` use explicit policies rather than plain `[Authorize]`.
- Employee and appreciation-letter import endpoints use dedicated import policies.
- Workflow task listing/reassignment and workflow definition creation/versioning/publishing use `ManageWorkflows`.
- Replacing an existing profile photo now requires `Employee.ProfilePhoto.Replace` in the service layer (`API.HR.Node/Services/ProfilePhotos/EmployeeProfilePhotoService.cs:98-115`).
- `WorkflowEngine.ExecuteActionAsync` now wraps transition persistence, events, tasks, comments, handlers, and idempotency persistence in one transaction (`API.HR.Node/Services/Workflows/WorkflowEngine.cs:191-328`).
- A root `.gitignore` now excludes build output, logs, and `App_Data`. This prevents new files from being added accidentally, but it does not remove or remediate sensitive files already present in this source snapshot or any prior history.

## Current findings

### 1. Critical: live secrets and privileged bootstrap credentials remain in source

Evidence:

- `API.HR.Node/appsettings.json:3` contains a SQL login connection string with a password.
- `API.HR.Node/appsettings.json:33` and `:39` contain Microsoft client secrets.
- `API.HR.Node/Program.cs:307` uses a hard-coded fallback password when `ADMIN_INITIAL_PASSWORD` is absent.
- `API.HR.Node/Program.cs:348-350` grants full access to two hard-coded identities, including a named organizational email address.
- `GrantFullAccessToUser` creates a missing local account with the fallback password (`Program.cs:301-314`) and grants every role and permission on every startup (`:326-343`).

Impact:

- Anyone with this source can attempt database access, Microsoft application impersonation, and login with the known bootstrap credential.
- A deployment without the environment setting creates predictable local credentials for privileged accounts.
- Hard-coded identity promotion creates a permanent privilege path outside ordinary administrator governance and separation of duties.

Recommendation:

1. Rotate the database password, both Microsoft client secrets, and all accounts created with the fallback password immediately.
2. Remove secrets from tracked configuration and repository history/backups. Use environment-backed deployment secrets or a managed secret store.
3. Remove the fallback password entirely. If bootstrap is necessary, require an explicit one-time secret, fail startup when it is absent, force immediate rotation, and disable bootstrap after first use.
4. Remove hard-coded personal super-administrator promotion. Manage roles through an audited, explicit administration process.
5. Avoid granting all roles indiscriminately; grant only the minimum administrator role/permissions required.

### 2. Critical: an unencrypted data-protection key and employee photos remain in the application tree

Evidence:

- `API.HR.Node/App_Data/DataProtection-Keys/key-ae1ce1de-c461-40a9-910d-33a8295db9f4.xml:10-12` contains an unencrypted ASP.NET Core data-protection master key.
- `API.HR.Node/Program.cs:33-37` persists keys below the content root without configuring encryption at rest.
- `API.HR.Node/App_Data/Storage/ProfilePhotos` currently contains 8,492 files totaling about 191 MB.

Impact:

- Disclosure of the data-protection key compromises trust in data-protected authentication artifacts and other protected tokens that use this application key ring.
- Employee photos are personal data and should not travel with source archives, build output, or routine developer backups.

Recommendation:

1. Treat the current key ring as compromised, rotate it, and invalidate existing bearer/refresh tokens as part of the response.
2. Store and encrypt the key ring outside the application/source directory using a protected certificate, operating-system facility, or managed key service.
3. Move employee photos to separately permissioned runtime storage with appropriate retention, backup, and privacy controls.
4. Verify that ignored runtime data was never committed elsewhere. `.gitignore` does not remove existing history.

### 3. Critical: privileged Blazor operations still bypass the new policies

Evidence:

- `WorkflowManagementPage.razor:2` still uses only `[Authorize]`. It reads all workflow data and directly creates/publishes definitions through EF Core (`:736-767` and `:944-1014`).
- `UserPasskeysPage.razor:2` still uses only `[Authorize]`, lists all employee passkeys (`:233`), and invokes administrative revoke/permanent-delete operations with the magic user ID `"admin"` (`:326-355`). `PasskeyService` treats that value as an ownership bypass (`API.HR.Node/Services/Security/PasskeyService.cs:202-244`).
- `Sessions.razor:2` still uses only `[Authorize]`, lists all active device sessions (`:119-120`), and revokes any selected session (`:139-140`).
- Employee/photo pages use only `[Authorize]` and call `IEmployeeProfilePhotoService` directly. For example, `Employees.razor:942-1046` reads, uploads, and deletes photos without passing through endpoint policies.
- The profile-photo service enforces replacement permission, but its read, initial upload, and delete methods do not enforce their respective permissions internally.

Impact:

A normal authenticated Blazor user can administer workflows, revoke or permanently delete other users' passkeys, revoke sessions, and perform photo operations that the REST API correctly protects with policies. Securing only the endpoint layer is ineffective when server-side components call the underlying services or DbContext directly.

Recommendation:

1. Apply `ManageWorkflows`, `ManageDevices`, and a dedicated `ManagePasskeys` policy to the corresponding pages.
2. Remove the `userId == "admin"` service bypass. Pass the authenticated principal and authorize with roles/permissions explicitly.
3. Enforce authorization in application services for every privileged operation, not only in minimal API mappings.
4. Apply suitable photo policies to the employee/gallery pages or authorize each service method using `IAuthorizationService` and the target employee/resource.
5. Add component/service integration tests that use an ordinary authenticated principal and prove each mutation is denied.

### 4. Critical: workflow actions still do not enforce assignee or transition permissions

Evidence:

- The execute-action endpoint inherits only the general authenticated API policy (`API.HR.Node/Endpoints/WorkflowEndpoints.cs:16-28`).
- `WorkflowEngine.ExecuteActionAsync` loads the instance and transition, evaluates business conditions, and performs the transition without checking whether `command.UserId` owns a pending task, is a valid delegate/supervisor, or has `transition.RequiredPermission` (`API.HR.Node/Services/Workflows/WorkflowEngine.cs:144-218`).
- `GetAvailableActionsAsync` accepts `userId` but never uses it and returns every transition from the current step (`WorkflowEngine.cs:333-357`).
- `GetInstanceAsync` calculates available actions using the workflow initiator rather than the requesting principal (`WorkflowEngine.cs:360-404`).
- Workflow instance, timeline, and arbitrary task-detail endpoints are also available to every authenticated API caller (`WorkflowEndpoints.cs:30-59`).

Impact:

Any authenticated caller can discover workflow records and approve, reject, return, or otherwise transition work assigned to someone else. The new `ManageWorkflows` policy protects administrative definition endpoints but does not protect business action authorization.

Recommendation:

1. In the workflow service, require a matching pending assignment, an active delegation, or a specifically authorized supervisory override.
2. Enforce `RequiredPermission` before returning or executing a transition.
3. Scope instance, task, and timeline reads to the initiator, assignee/delegate, authorized department/role, or administrator.
4. Pass the actual requesting user into `GetInstanceAsync`; never substitute the initiator for authorization decisions.
5. Add negative tests for an unrelated authenticated user, an expired delegate, a user missing `RequiredPermission`, and a completed/reassigned task.

### 5. High: single-device binding and remote logout remain bypassable

Evidence:

- Password/Microsoft login and passkey login accept client-supplied device identity. If it is omitted, validation succeeds (`API.HR.Node/Endpoints/AuthEndpoints.cs:730-738` and `PasskeyEndpoints.cs:319-327`).
- The fallback identity can be a client-supplied model/manufacturer string (`AuthEndpoints.cs:730-733`).
- Active-device status is checked only by `GET /api/employees/me` (`API.HR.Node/Endpoints/EmployeeEndpoints.cs:325-334`), not centrally for every protected request.
- Refresh checks expiration and the Identity security stamp but not device revocation (`AuthEndpoints.cs:183-216`).

Impact:

A client can omit or spoof device fields. Tokens issued before device revocation can continue calling other endpoints and can be refreshed while the Identity security stamp remains valid.

Recommendation:

Require a server-verifiable device credential if device binding is a real security requirement. Bind token families to a device record and validate it on protected requests and refresh. On revocation, revoke the token family and/or update the Identity security stamp. Centralize enforcement in authentication/authorization middleware or a policy.

### 6. High: WebAuthn challenge state is still trusted when returned by the client

Evidence:

- Registration returns serialized options and completion accepts those options back from the caller (`API.HR.Node/Endpoints/PasskeyEndpoints.cs:80-87` and `:108-123`).
- Anonymous login uses the same pattern (`PasskeyEndpoints.cs:155-190`).
- The service verifies against client-returned `OriginalOptions` (`API.HR.Node/Services/Security/PasskeyService.cs:77-82` and `:167-174`) rather than server-retained one-time state.

Impact:

The server does not establish that a completion challenge was issued recently by this server for the same ceremony and context. This weakens challenge integrity and replay protection; authenticator signature counters are not a complete replacement.

Recommendation:

Store challenge state server-side with a short expiration, ceremony type, user/session binding, relying-party context, and one-time consumption. Completion should accept the authenticator response and an opaque ceremony identifier, not trusted original options.

### 7. Medium: workflow transactionality improved, but start and idempotency remain inconsistent

Evidence:

- Execute-action persistence is now transactional, resolving the main atomicity issue from the first review.
- `StartAsync` still saves the instance, timeline, step transition, and task in separate operations without a surrounding transaction (`API.HR.Node/Services/Workflows/WorkflowEngine.cs:59-113`). A failure can leave a partially initialized workflow.
- The idempotency lookup now matches key + workflow instance + action (`WorkflowEngine.cs:129-140`), but the database still enforces global uniqueness on `IdempotencyKey` alone (`API.HR.Node/Data/Workflows/WorkflowConfigurations.cs:137-143`). Reusing a key for another valid instance/action therefore reaches insert and fails instead of operating as the code's scope suggests.
- Caller identity is not part of the idempotency record or replay check, and replay happens before workflow authorization/state validation.

Recommendation:

Make workflow start atomic. Define the intended idempotency scope once in both code and the database index, include caller/tenant where appropriate, reserve the key before applying work, and never replay a result before authorization has succeeded.

### 8. Medium: upload malware scanning is still a no-op

Evidence:

- Production DI registers `NullFileMalwareScanner` (`API.HR.Node/Program.cs:54`).
- It always returns `Bypassed` (`API.HR.Node/Services/Security/NullFileMalwareScanner.cs:3-14`).
- Upload rejects only `Infected`, so `Bypassed` is accepted (`API.HR.Node/Services/ProfilePhotos/EmployeeProfilePhotoService.cs:56-63`).

Impact:

There is no live malware scan despite the abstraction and audit language. Image decoding/re-encoding is a useful compensating control, but the effective policy should be explicit.

Recommendation:

Integrate the intended scanner and fail closed on required scanner failure, or formally document decoded-and-reencoded image validation as the accepted control and remove misleading scanner behavior.

### 9. Medium: the image-processing dependency has a known vulnerability

Evidence:

- `API.HR.Node/API.HR.Node.csproj:24` pins `SixLabors.ImageSharp` 3.1.10.
- Build reports `NU1902` for moderate-severity advisory `GHSA-rxmq-m78w-7wmc`.

Impact:

This package processes untrusted uploads, so an applicable decoder/processor vulnerability has a security-relevant exposure path.

Recommendation:

Upgrade to a patched compatible version, review the advisory's affected paths, rerun image tests, and add dependency advisory checks to CI.

### 10. Medium: local storage containment checks remain incomplete

Evidence:

- `LocalFileStorageService.SaveAsync` and `OpenReadAsync` use a plain string `StartsWith` root check without a directory-boundary separator (`API.HR.Node/Services/Storage/LocalFileStorageService.cs:21-26` and `:55-60`). A sibling path sharing the root prefix can pass.
- `ExistsAsync` and `DeleteAsync` perform no root containment check after resolving the full path (`LocalFileStorageService.cs:78-100`).

Impact:

Upload keys are currently server-generated, limiting immediate exploitability. A malformed database value or future caller could nevertheless read, overwrite, test, or delete outside the intended root.

Recommendation:

Reject rooted keys and traversal segments, normalize the root with a trailing separator, and use one shared containment routine for all operations. Add mixed-separator, `..`, and sibling-prefix traversal tests.

### 11. Medium: authentication and production hardening remain weak

Evidence:

- Local passwords require only six characters and no character classes (`API.HR.Node/Program.cs:179-188`).
- The Blazor login disables lockout-on-failure (`API.HR.Node/Components/Account/Pages/Login.razor:616`).
- No ASP.NET Core rate limiter is configured for login, refresh, Microsoft-token validation, or anonymous passkey endpoints.
- Swagger is enabled in every environment (`API.HR.Node/Program.cs:356-362`).
- `FileLoggerOptions.RetainDays` is unused; logging can silently drop queued entries or suppress write errors (`API.HR.Node/Services/Logging/FileLogger.cs:10`, `:39-44`, and `:62-69`).
- Database migrations and security bootstrap run synchronously during every application startup (`Program.cs:251-353`).

Impact:

The application has avoidable brute-force and resource-exhaustion exposure, weak local-password defaults, public API discovery unless separately restricted, potentially unbounded/unreliable local logs, and deployment-time database race/downtime risk.

Recommendation:

Use a modern password policy or disable local passwords where federated/passkey authentication suffices, enable consistent lockout and endpoint-specific rate limits, restrict Swagger, centralize protected logs with retention/alerts, and run migrations/bootstrap as controlled deployment operations with separately scoped credentials.

### 12. Medium: Blazor Server components retain EF Core contexts and bypass service boundaries

Evidence:

- The application uses Interactive Server rendering (`API.HR.Node/Program.cs:40-41` and `:382-384`).
- `UserDevices`, `AuditLogsPage`, `WorkflowManagementPage`, `UserPasskeysPage`, and `Sessions` retain an injected `ApplicationDbContext` and perform direct queries/mutations.

Impact:

A scoped dependency lives for the Blazor circuit, while `DbContext` is designed as a short unit of work and is not thread-safe. Long-lived tracking and overlapping UI events can cause stale data, memory growth, and concurrency errors. Direct EF access also makes consistent authorization and auditing difficult.

Recommendation:

Use `IDbContextFactory<ApplicationDbContext>` per operation and route privileged changes through application services that enforce authorization, validation, transactions, and audit logging.

## Positive observations

- Permission authorization now fails closed and includes an explicit administrator override.
- Sensitive import and several administrator surfaces now have dedicated policies.
- Profile-photo replacement permission is enforced in the service rather than solely at the endpoint.
- Workflow action persistence now uses a transaction and a transactional outbox for the reviewed notification handler.
- `ImageSharpProcessor` validates size, extension, signature, decoded dimensions, and pixel count, then re-encodes images and removes EXIF/IPTC/XMP metadata.
- Photos are outside `wwwroot`, use random storage keys, retain version metadata, and have a filtered unique index for one active photo per employee.
- Photo responses use private caching, ETags, `nosniff`, and inline content disposition.
- Microsoft token validation checks issuer, audience, signature, lifetime, tenant, and immutable object identity before account linking.
- SQL in reviewed paths uses EF Core LINQ rather than string-built raw SQL.

## Verification performed after the changes

- `dotnet build API.HR.Node.slnx --no-restore --configuration Release -m:1`
  - Result: succeeded with 0 errors and two `NU1902` warnings for the ImageSharp advisory.
- `dotnet test API.HR.Node.Tests/API.HR.Node.Tests.csproj --no-build --no-restore --configuration Release`
  - Result: 26 passed, 0 failed, 0 skipped.

Current tests cover photo validation/retrieval/versioning/deletion/replacement, permission-handler behavior, basic employee persistence/lookups, and workflow happy paths/idempotency. They do not exercise HTTP or Blazor authorization boundaries, task ownership/transition permissions, WebAuthn challenge expiry/replay, device revocation across endpoints, SQL Server transaction rollback/concurrency, bootstrap configuration, or storage traversal.

## Recommended remediation order

1. Rotate exposed credentials and data-protection keys, invalidate affected tokens, and remove secrets/photos/key rings from source and history.
2. Remove the fallback password and hard-coded personal super-administrator bootstrap.
3. Protect `/workflows`, `/passkeys`, and `/sessions`; remove the passkey `"admin"` bypass; enforce authorization in services used directly by Blazor.
4. Enforce workflow assignment/delegation, `RequiredPermission`, and object-level read access in the workflow service.
5. Make device binding/revocation and WebAuthn ceremonies server-verifiable and centrally enforced.
6. Finish workflow-start/idempotency consistency, upgrade ImageSharp, fix storage containment, and decide the malware-scanning policy.
7. Add integration tests for all negative authorization paths plus CI checks for secrets, dependencies, runtime artifacts, and generated output.

