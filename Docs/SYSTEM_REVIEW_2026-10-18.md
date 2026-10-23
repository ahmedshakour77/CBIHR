# HR system review notes

Reviewed: **18 October 2026**  
Scope: the HR application in `API.HR.Node` and its existing test project. “Hall system” was interpreted as “whole system”; no hall-specific module was found.

## Overall assessment

The system has substantial functionality across employee records, identity documents, photos, leave, workflows, organizational structure, payroll reporting, financial balances, and imports. Several safeguards identified as missing in older reviews are now present. However, the reviewed source has release-blocking compilation errors and serious gaps in document privacy and workflow authorization.

**Priorities:** protect national-card images and the public promotion report, enforce workflow authorization, restore a passing build, then address authentication state and financial consistency.

This review contains **16 findings: 1 critical, 7 high, and 8 medium**. These are source-based findings except where build output provides direct verification. Their production reachability depends on the deployed version and any external access controls.

## Review method and limits

- Inspected current startup, route policies, sensitive Blazor pages, identity-document services, workflow engine/configuration, financial and allowance services, employee deletion, storage, logging, import entry points, migrations, and relevant tests.
- Ran the existing Release test command, which also compiles the application. The final attempt failed before tests could execute; no current passing test count is claimed.
- Source changed during the review: the two initially missing passport scan handlers appeared later. The final findings below reflect the subsequently inspected source; the initial missing-handler errors are not treated as unresolved findings.
- No application-source fixes were made for this review. No production requests, database migrations, record changes, or exploit attempts were performed.
- Browser appearance, actual SQL Server constraints/data, deployment settings, backup restoration, external identity-provider configuration, and current dependency advisories were not independently validated. This is a broad source review, not an exhaustive penetration test or a business-rule certification.
- No Git working tree was available in either the workspace root or application directory, so the review cannot identify a commit or distinguish pre-existing edits by author.

## Findings

### 01 — Critical: national-card images lack authentication and ownership checks

**Evidence:** [NationalCardEndpoints.cs](../API.HR.Node/Endpoints/NationalCardEndpoints.cs#L15), image routes at lines 32, 49, and 66; [NationalCardService.cs](../API.HR.Node/Services/IdentityDocuments/NationalCardService.cs#L151).

The `/api/national-cards` group has no authorization policy. Its biometric, front, and back image routes have no individual policy either. `GetImageAsync` retrieves a card by integer ID and opens its stored image without checking the caller, employee ownership, or `IsActive`.

**Impact:** a caller who can reach these routes can request identity and biometric images by changing the card ID, including images belonging to inactive card records. The document-number lookup at line 19 requires authentication but also lacks an owner/staff check, allowing an ordinary account to request another employee's card metadata when its document number is known.

**Recommended change:** require authentication for the entire group; authorize the linked employee or explicitly permitted staff for every card read. Apply the same rule to document-number lookup. Reject inactive records for normal reads and define private caching and access-audit behavior.

**Verification:** anonymous image requests return 401; another employee receives 403/404; the owner and permitted staff succeed; inactive records cannot be retrieved through alternate routes.

### 02 — High: `/jobtitles` exposes employee promotion and disciplinary information without route protection

**Evidence:** [JobTitlePages/Index.razor](../API.HR.Node/Components/Pages/JobTitlePages/Index.razor#L1), data loading from line 279; [Routes.razor](../API.HR.Node/Components/Routes.razor#L4); authorization configuration in [Program.cs](../API.HR.Node/Program.cs#L217).

The page has no `[Authorize]` attribute or inherited authorization attribute. No fallback authorization policy is configured. It loads employees, grades/stages, appreciation letters, and penalties and displays employee numbers, names, promotion predictions, and disciplinary effects. `AuthorizeRouteView` alone does not make a page with no authorization metadata private. The layout also renders its body for unauthenticated visitors.

**Impact:** sensitive workforce information is available through an unprotected page in the reviewed application configuration. Loading it can also invoke reference-data seeders when their tables are empty.

**Recommended change:** add an explicit HR/staff policy and any required formation scope. Move reference-data initialization out of page reads. Review routable components for missing authorization metadata with an automated route inventory.

**Verification:** anonymous and ordinary employee accounts cannot load the report or trigger initialization; permitted staff see only the intended scope.

### 03 — High: workflow transitions and detail reads do not enforce the caller's assignment or permission

**Evidence:** [WorkflowEndpoints.cs](../API.HR.Node/Endpoints/WorkflowEndpoints.cs#L16), routes at lines 26–57; [WorkflowEngine.cs](../API.HR.Node/Services/Workflows/WorkflowEngine.cs#L128), action execution and `GetAvailableActionsAsync` at line 362.

Execution checks workflow state, transition code, optional row version, and conditions, but does not require a matching pending assignment, role/department assignment, valid delegation, or `RequiredPermission`. Available actions accept `userId` without using it to filter results. Instance details calculate actions with the initiator's ID rather than the requesting user's identity. Instance, timeline, and task-by-ID reads require only the general authenticated API policy.

**Impact:** an unrelated authenticated account can read workflow information and invoke valid transitions, potentially approving or rejecting another user's work. Administrative definition policies do not protect these business actions.

**Recommended change:** enforce resource authorization in the engine/service for reads and writes. Resolve the actual principal, pending task assignment, active delegation, transition permission, and any explicit administrative override before processing or replaying an action.

**Verification:** deny unrelated employees, expired delegates, users missing transition permission, and previously assigned users after reassignment; allow intended individual and role assignments.

### 04 — High: the current employee-profile source prevents a Release build

**Evidence:** [EmployeeProfile.razor](../API.HR.Node/Components/Pages/EmployeeProfile.razor), approximately 7,491 lines at the final inspection; final build/test output described below.

The final compilation reported `CS1527` for a private element at namespace scope and `CS0426` for the generated `__PrivateComponentRenderModeAttribute` on `EmployeeProfile`. These errors are in Razor-generated output for this component. Its closing code boundaries near the end of the source should be checked first; generated files should not be edited as a fix.

**Impact:** the current source cannot produce a verified release, and the existing tests cannot run against it.

**Recommended change:** repair the Razor/component code boundaries and rebuild. Split passport scanning, financial operations, vehicles, and lifecycle dialogs into smaller components once the immediate compilation issue is resolved.

**Verification:** a clean Release build and the existing suite pass against the same source snapshot. The earlier `ApplyMrzScan`/`ApplyChipReading` missing-handler errors were superseded by later edits and are recorded only as review history.

### 05 — High: passkey completion trusts options supplied by the client

**Evidence:** [PasskeyEndpoints.cs](../API.HR.Node/Endpoints/PasskeyEndpoints.cs#L110), also line 182; [PasskeyService.cs](../API.HR.Node/Services/Security/PasskeyService.cs#L80), also line 170.

Registration and login options are returned to the caller, then completion deserializes `request.OptionsJson` and supplies it to FIDO verification as `OriginalOptions`. The reviewed API flow does not retain and consume a server-issued ceremony with an expiration and context binding.

**Impact:** the server cannot establish that a submitted challenge was recently issued by this server for the same ceremony and user/session. This weakens challenge integrity and replay protection. It does not mean an attacker can fabricate an authenticator signature without its key.

**Recommended change:** retain ceremony state server-side, return an opaque identifier, bind it to the expected account/session and operation, and consume it once with a short expiry. Use shared storage if multiple instances serve the API.

**Verification:** reject altered original options, unknown/expired ceremonies, repeated completion, and cross-account/session use.

### 06 — High: device binding and remote session termination are incomplete

**Evidence:** [AuthEndpoints.cs](../API.HR.Node/Endpoints/AuthEndpoints.cs#L765), [PasskeyEndpoints.cs](../API.HR.Node/Endpoints/PasskeyEndpoints.cs#L319), refresh at [AuthEndpoints.cs:218](../API.HR.Node/Endpoints/AuthEndpoints.cs#L218), device check at [EmployeeEndpoints.cs:368](../API.HR.Node/Endpoints/EmployeeEndpoints.cs#L368), [UserDevices.razor:300](../API.HR.Node/Components/Pages/UserDevices.razor#L300), and [Sessions.razor:210](../API.HR.Node/Components/Pages/Sessions.razor#L210).

Login device validation accepts omitted identity and otherwise trusts fields supplied by the client. Session/device revocation changes `IsActive` without revoking a token family or changing the user's security stamp. Refresh validates expiry and security stamp but not device status. The employee `/me` path checks whether *any* active device exists for the employee; other protected routes do not centrally enforce this state.

**Impact:** omitted or copied device fields can bypass the intended binding. A token issued before revocation can remain usable on other routes and can still be refreshed; later activating another device can also satisfy the employee-level active-device check for an older token.

**Recommended change:** tie authentication sessions/token families to a specific server-side device/session record, verify that record centrally and during refresh, and revoke it when an administrator terminates the session. Define web and mobile rules explicitly.

**Verification:** after termination, the old session fails across employee, document, workflow, and refresh routes; a new valid device does not reactivate old tokens.

### 07 — High: concurrent financial transactions can lose balance updates

**Evidence:** [EmployeeFinancialService.cs](../API.HR.Node/Services/Financial/EmployeeFinancialService.cs#L146), especially balance read/update at lines 184–225; [EmployeeFinancialAccount.cs](../API.HR.Node/Data/Financial/EmployeeFinancialAccount.cs); mapping at [ApplicationDbContext.cs:614](../API.HR.Node/Data/ApplicationDbContext.cs#L614).

Transactions read the balance, calculate a new value in memory, then save the account and ledger row. The account has no concurrency token and this operation does not serialize competing balance updates or use an atomic increment. There is also no request-id uniqueness check to stop duplicate submissions.

**Impact:** starting from 100, simultaneous deposits of 10 and 20 can leave a balance of 110 or 120 while both ledger entries exist; the intended result is 130. A retried successful request can create a second transaction.

**Recommended change:** use database-enforced concurrency or serialized/atomic balance updates with the ledger insert in the same transaction. Add an idempotency identifier for financial commands and explicitly handle account-creation races.

**Verification:** use separate SQL Server contexts for simultaneous deposits/withdrawals and retry tests; assert both final balance and ledger continuity.

### 08 — High: national-card verification status can be asserted by the submitting client

**Evidence:** [NationalCardService.cs](../API.HR.Node/Services/IdentityDocuments/NationalCardService.cs#L85); [NationalCardModels.cs](../API.HR.Node/Services/IdentityDocuments/NationalCardModels.cs#L191); self-service registration in [NationalCardEndpoints.cs](../API.HR.Node/Endpoints/NationalCardEndpoints.cs#L102).

The service persists `request.GetEffectivePassiveAuth()` as the card's passive-authentication result. That helper returns true when any of `IsPassiveAuthenticationValid`, `IsSodHashVerified`, or `IsBacAuthenticated` is set by the caller. The service does not verify a signed document or independently establish the claimed result before storing it.

**Impact:** a self-service submitter can mark their own document as verified without corresponding server-side evidence. Downstream readers cannot distinguish submitted assertions from independently verified results.

**Recommended change:** store client assertions separately from trusted verification results. Set trusted status only after an approved verification process validates the evidence; preserve verifier identity, method, and evidence references.

**Verification:** posting verification flags alone never produces a trusted verified record.

### 09 — Medium: MRZ parsing and validation results are inconsistent

**Evidence:** [NationalCardService.cs](../API.HR.Node/Services/IdentityDocuments/NationalCardService.cs#L175), validity calculation at line 238; [PassportReaderService.cs](../API.HR.Node/Services/IdentityDocuments/PassportReaderService.cs#L94), validity at line 105 and chip success at line 156.

The national-card parser creates an error list but does not add checksum/date validation errors before returning `IsValid`. The passport parser collects failed checksum errors but sets `IsValid` solely from a nonempty passport number. Chip parsing can then report `Success` from that permissive result, or from a supplied Arabic name, without propagating MRZ errors into its own error list.

**Impact:** structurally readable or malformed data may be presented as valid/successful even when integrity checks fail. Consumers must currently understand several incompatible success flags.

**Recommended change:** separate “parsed” from “validated,” define explicit validation outcomes, propagate errors consistently, and gate trusted automatic updates on the intended validation outcome. Parsing a string should not itself imply document authenticity.

**Verification:** cover bad check digits, invalid dates, truncated input, failed MRZ inside chip input, and name-only input.

### 10 — Medium: national-card image uploads bypass the established image-validation pipeline

**Evidence:** [NationalCardService.cs](../API.HR.Node/Services/IdentityDocuments/NationalCardService.cs#L57), helper at line 258; compare [ImageSharpProcessor.cs](../API.HR.Node/Services/Images/ImageSharpProcessor.cs#L20).

National-card images are decoded from Base64 and written directly to storage under a `.jpg` key. This path does not verify image signatures/decodability, dimensions, pixel limits, or re-encode/remove metadata, and it does not invoke the scanner. Filename uniqueness is only to the second, allowing rapid updates to target the same file. Replacing an image also leaves its previous file behind.

**Impact:** the document path accepts arbitrary decoded bytes as an image, retains possible embedded metadata, and can accumulate orphan files or overwrite a rapid prior update. General request-size limits do not replace decoded image limits.

**Recommended change:** use a shared document-image validation pipeline with explicit size/pixel limits and appropriate scanning policy. Generate random immutable keys and coordinate replacement cleanup with successful persistence.

**Verification:** reject non-images and oversized decoded images; simultaneous uploads have distinct keys; failed saves do not leave untracked files or destroy the previous image.

### 11 — Medium: workflow initialization and idempotency constraints are inconsistent

**Evidence:** [WorkflowEngine.cs](../API.HR.Node/Services/Workflows/WorkflowEngine.cs#L18), start saves at lines 63/94 and task creation at line 458; idempotency lookup at line 133; [WorkflowConfigurations.cs](../API.HR.Node/Data/Workflows/WorkflowConfigurations.cs#L78) and line 142.

`StartAsync` persists the instance, events, initial transition, and task through multiple saves without a surrounding transaction. Its “already running” check has no matching unique filtered database constraint. Action idempotency is looked up by key + instance + action, but the database makes the key alone unique. Cached results are also returned before caller authorization/state validation.

**Impact:** failure during initialization can leave an incomplete workflow; competing starts can both pass the existence check. Reusing an idempotency key across distinct instances/actions can cause a database error despite missing the lookup. Replay handling must not become an authorization bypass when finding 03 is repaired.

**Recommended change:** make start atomic; enforce the intended active-instance uniqueness in the database; align idempotency lookup/index scope and bind replay to the authorized caller and operation.

**Verification:** inject a failure after initial persistence, attempt simultaneous starts, reuse a key across the intended scopes, and test unauthorized replay using SQL Server.

### 12 — Medium: allowance periods lack temporal and concurrent-state safeguards

**Evidence:** [EmployeeAllowanceService.cs](../API.HR.Node/Services/Allowances/EmployeeAllowanceService.cs#L134), deletion at line 204; nonunique index at [ApplicationDbContext.cs:589](../API.HR.Node/Data/ApplicationDbContext.cs#L589); payroll selection at [FormationPayrollService.cs:85](../API.HR.Node/Services/Salary/FormationPayrollService.cs#L85).

Saving a period closes whichever records are currently marked `IsCurrent`, then creates a new current record without rejecting reversed dates or an earlier start that conflicts with existing history. Concurrent saves can create multiple current records because the index is nonunique. Deleting the current period saves deletion before separately reactivating an earlier record and clearing its end date. Payroll chooses one overlapping period for an entire month.

**Impact:** periods can overlap or have invalid date ranges; a failed delete/reactivation can leave no current period. Historical and mid-month payroll reporting can select a package without a clearly defined effective-date rule.

**Recommended change:** validate date order/overlap, enforce one current period at the database level, make delete/reactivation atomic, and explicitly define how historical and mid-month changes affect reports.

**Verification:** reject invalid ranges; cover backdated changes, concurrent saves, failed reactivation, and two packages within the same reporting month.

### 13 — Medium: permanent employee deletion does not coordinate database and file cleanup

**Evidence:** [EmployeeLifecycleService.cs](../API.HR.Node/Services/Employees/EmployeeLifecycleService.cs#L138), photo file deletion at lines 192/194 and database save at line 311; document relationships in [ApplicationDbContext.cs](../API.HR.Node/Data/ApplicationDbContext.cs#L136).

Profile-photo files are deleted before the database changes are committed. If the eventual database save fails, the retained employee/photo rows can reference files that have already been removed. The routine only explicitly cleans profile-photo files; national-card and passport records can cascade away while their stored document images remain.

**Impact:** failed deletion can damage an otherwise retained employee record, while successful deletion can leave sensitive document files without their owning rows.

**Recommended change:** collect all owned file keys and persist durable cleanup work with the successful database transaction. Perform idempotent cleanup afterwards with retries and monitoring; include national-card/passport and other stored attachments.

**Verification:** simulate database-save failure and file-delete failure separately; verify retained records keep their files and successful deletion eventually removes every owned file.

### 14 — Medium: authentication throttling is shared across users and misses passkey routes

**Evidence:** [Program.cs](../API.HR.Node/Program.cs#L84), named fixed-window limiter at line 87; [AuthEndpoints.cs](../API.HR.Node/Endpoints/AuthEndpoints.cs#L29); [PasskeyEndpoints.cs](../API.HR.Node/Endpoints/PasskeyEndpoints.cs#L22).

The named `auth-limit` permits ten requests per minute in a shared fixed-window limiter for the mapped auth group, including refresh and authenticated context reads. It is not partitioned by account/client. Passkeys are mapped through a separate group with no named limiter. The global limiter only budgets requests carrying a Basic Authorization header.

**Impact:** a small burst of login, refresh, or `/api/auth/me` requests can exhaust the shared budget for other users. Ordinary anonymous passkey requests avoid this named throttle and can trigger database work.

**Recommended change:** use deliberate per-client/account budgets for authentication operations, account for trusted proxy configuration, apply limits to passkey routes, and give routine authenticated reads an appropriate separate budget.

**Verification:** one client's activity does not exhaust another client's login budget; passkey option/completion floods are limited; normal mobile refresh/context traffic remains usable.

### 15 — Medium: startup schema creation can bypass migration history and hide schema drift

**Evidence:** [StartupSeeder.cs](../API.HR.Node/Data/StartupSeeder.cs#L50), unconditional fallback calls at lines 56/59 and raw table creation at lines 138/186; warning suppression at [Program.cs:313](../API.HR.Node/Program.cs#L313); [national-card migration](../API.HR.Node/Data/Migrations/20261014222924_AddEmployeeNationalCards.cs).

Setting `Database:MigrateOnStartup=false` skips EF migrations but still executes raw `CREATE TABLE` fallbacks for national cards/passports. These operations do not record migration history. The national-card fallback also declares `CreatedBy` as 150 characters while the migration declares 100. Pending-model-change warnings are suppressed in both context registrations.

**Impact:** different startup paths can produce different schemas. A later migration can try to create an already existing table. The application continues performing schema work even when operators disable startup migrations, undermining deployments that separate runtime and migration privileges.

**Recommended change:** make migrations the single schema authority; remove ad hoc table creation from normal startup; honor migration-disable settings for all schema changes and surface model/migration drift in build/deployment checks.

**Verification:** provision a fresh database through migrations, start with a runtime-only database account, and test migration-disabled startup followed by a controlled upgrade.

### 16 — Medium: several interactive pages retain an injected EF context for the circuit lifetime

**Evidence:** direct `ApplicationDbContext` injection in [AuditLogsPage.razor](../API.HR.Node/Components/Pages/AuditLogsPage.razor#L6), [AdminLookupsPage.razor](../API.HR.Node/Components/Pages/AdminLookupsPage.razor#L11), [UserPasskeysPage.razor](../API.HR.Node/Components/Pages/UserPasskeysPage.razor#L5), [JobClassificationsPage.razor](../API.HR.Node/Components/Pages/JobClassificationsPage.razor#L9), [Sessions.razor](../API.HR.Node/Components/Pages/Sessions.razor#L5), and [UserDevices.razor](../API.HR.Node/Components/Pages/UserDevices.razor#L6).

These interactive server pages issue reads/mutations through a scoped context retained across UI interactions. `EmployeeFinancialService`, injected into the large interactive profile, also retains a scoped context. Other parts of the system already create contexts per operation.

**Impact:** overlapping awaited UI events can contend on the same non-thread-safe context; long sessions retain tracked state and can show stale records. Direct mutations spread authorization, validation, and auditing responsibilities across pages.

**Recommended change:** use `IDbContextFactory<ApplicationDbContext>` per operation and application services for privileged mutations. Avoid retaining tracked entities between operations; pass identifiers and reload the current state.

**Verification:** exercise overlapping refresh/save actions and long-lived circuits; confirm no second-operation errors, stale overwrites, or unbounded tracking growth.

## Safeguards already present

These observations are based on current source; they are not claims that the blocked suite passed:

- `PermissionAuthorizationHandler` denies unauthenticated users and users without the requested permission, with an explicit Admin override.
- Many sensitive routes/pages now have dedicated permission or staff policies; the sessions page requires Admin.
- Bootstrap uses a configuration-supplied initial password, skips an existing bootstrap user, and does not reset its password or re-grant its roles on each restart.
- Windows data-protection keys are configured for DPAPI protection; Swagger is enabled only in Development.
- Local file storage uses a common normalized root-containment routine for save, read, existence, and delete. Traversal/sibling-prefix regression tests exist.
- Profile-photo processing checks file size, signatures, dimensions, and pixel count, re-encodes images, and removes EXIF/IPTC/XMP metadata. National-card uploads need the same level of control.
- Defender scanning is selected when its engine is available, with a null-scanner fallback elsewhere. Actual deployment scanner availability and fallback acceptance still need verification.
- Workflow action persistence has a transaction. Its authorization, initialization, and idempotency gaps remain separate findings.
- File logging now includes retention pruning; older statements that `RetainDays` is entirely unused no longer describe the current code.
- Password length is now ten characters with four unique characters, and the reviewed web login enables lockout on failed password attempts. This does not establish that all authentication paths have equivalent controls.

## Build and test evidence

Command:

```powershell
dotnet test API.HR.Node.Tests/API.HR.Node.Tests.csproj --no-restore --configuration Release --logger "console;verbosity=minimal"
```

**Final result:** exit code 1 during application compilation; tests did not execute.

The final attempt reported these errors in Razor-generated `EmployeeProfile_razor.g.cs`:

```text
CS1527: Elements defined in a namespace cannot be explicitly declared as private,
        protected, protected internal, or private protected.
CS0426: '__PrivateComponentRenderModeAttributeAttribute' does not exist in EmployeeProfile.
CS0426: '__PrivateComponentRenderModeAttribute' does not exist in EmployeeProfile.
```

An earlier attempt also reported obsolete FIDO configuration properties, a nullable dictionary key warning in `AdminLookupsPage`, and a Windows-platform analyzer warning for the Active Directory service. Treat those as cleanup items after the build is restored.

The existing tests include permissions, profile-photo ownership/storage, employee lifecycle, imports, national-card persistence/payload mapping, and workflow happy paths/idempotency. However:

- Workflow tests use EF InMemory and explicitly ignore transaction warnings; they cannot establish SQL Server rollback, uniqueness, or concurrent-update behavior.
- Authorization handler tests do not establish that every route has the correct metadata or object-ownership checks.
- No HTTP integration harness using `WebApplicationFactory` or `TestServer` was found in the reviewed test sources, despite the test project referencing the testing package.
- The reviewed tests do not demonstrate anonymous/other-user national-card denial, workflow assignment enforcement, server-side passkey ceremony expiry/replay protection, central device revocation, or financial concurrency correctness.

## Suggested implementation order

1. **Restore a stable release:** repair the profile component, build Release, and execute the full existing suite on one source snapshot.
2. **Close exposed data paths:** authorize national-card images/lookups and `/jobtitles`; add anonymous, ordinary-user, owner, and permitted-staff route tests.
3. **Protect business decisions:** enforce workflow read/action authorization, including role/delegation assignments and required permissions.
4. **Repair authentication state:** retain one-time passkey ceremonies server-side; bind and revoke device sessions centrally; partition and complete auth throttling.
5. **Protect records and balances:** serialize financial updates with idempotency, enforce allowance temporal invariants, and distinguish submitted document claims from verified outcomes.
6. **Make persistence predictable:** align workflow constraints/transactions, use migration-only schema management, and introduce durable file cleanup.
7. **Improve maintainability:** split the profile component, replace circuit-retained EF contexts, and add SQL Server and HTTP integration coverage for the identified failure paths.

Completion should be demonstrated by the checks attached to each finding, plus a passing build and suite. A visual/browser review and deployment/backup review remain separate follow-up work.
