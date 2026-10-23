# Enterprise Systems Architecture and Technical Audit

**System:** API.HR.Node  
**Audit date:** 2026-10-11  
**Evidence base:** Local repository, configuration, EF models, documentation, and test results  
**Assessment type:** Source-based review; no production exploitation, load test, or rendered accessibility assessment

# 1. Executive Summary

**The reviewed implementation has critical authorization and authentication defects that should block an unrestricted production release.** It also has useful foundations: ASP.NET Identity, permission policies, private file storage, relational constraints, workflow transactions, automated tests, and a substantial design system. Those controls are applied inconsistently across sensitive operations.

This audit treats the repository at `C:\Users\Administrator\source\repos\API.HR.Node` as the supplied context, because the prompt’s input section contained no additional system details.

The findings are grounded in source, configuration, EF models, documentation, and test results. External references establish framework behavior only. **No production deployment, live exploitation, load testing, or browser accessibility assessment was performed.** Source defects are distinguished below from conditional risks and missing operational evidence.

Source links in this export are relative to the workspace root. The line numbers in their labels identify the reviewed evidence locations.

## Domain health assessment

| Domain | Rating | Assessment |
|---|---|---|
| Technical & Software Architecture | **D** | A viable monolith, weakened by oversized components, mixed responsibilities, and incomplete data integrity controls. |
| Security, Identity & Compliance | **F** | Critical financial authorization, workflow authorization, and passkey ceremony defects. |
| Performance, Scalability & Resilience | **D** | Request-bound processing, database amplification, and incomplete asynchronous delivery undermine predictable operation. Production capacity remains unverified. |
| UI/UX & Visual/System Design | **C** | Strong token and RTL foundations, with accessibility defects, missing styles, and security-policy conflicts affecting functionality. |
| Administrative, DevOps & Governance | **D** | Tests and configuration controls exist, but release assurance, audit completeness, licensing controls, and recovery evidence are insufficient. |

These are implementation assessments, not compliance certifications. **F** indicates a critical control failure; **D** indicates major deficiencies; **C** indicates material gaps despite useful foundations.

## Top three critical risks

| Priority | Confirmed finding | Consequence |
|---|---|---|
| **1** | Ordinary authenticated employees can reach financial and allowance editing controls; the corresponding save operations lack permission checks. | Unauthorized modification of HR balances, loans, advances, and compensation records. External payroll consequences are unverified. |
| **2** | Workflow execution checks state transitions but does not enforce the acting user’s assignment, required permission, or resource scope. | An authenticated user can potentially approve or reject another user’s workflow. Instance, timeline, and task reads also lack resource authorization. |
| **3** | Custom passkey completion accepts ceremony options supplied by the client, without server-held challenge state. | Replay protection and requested authentication assurance are undermined. Valid cryptographic assertions are still required; this is not evidence that arbitrary signatures can be forged. |

**Immediate intervention:** suspend or tightly restrict the affected financial write paths, workflow action endpoints, and custom passkey completion endpoints until server-side controls and negative authorization tests are in place.

## Verification results

| Check | Result | What it establishes |
|---|---|---|
| `dotnet test API.HR.Node.slnx --no-restore --verbosity minimal` | **103 passed; 0 failed; 0 skipped** | Existing tests pass. They do not establish production security or SQL transaction correctness. |
| Design-token contrast checks | **594 pairs passed**, across 11 brands and two themes | The checked token combinations meet the script’s contrast criteria. |
| CSS variable check | **Failed: four undefined variables, six usages** | Source contains unresolved styling references. |
| CSS class check | **Failed: six undefined classes, ten usages** | Source contains class names without matching CSS definitions. |

The application was not started because startup can migrate and seed the configured database. Application source was left unchanged.

# 2. Comprehensive Domain Analysis

## 2.1 Technical & Software Architecture

### Strengths

- **A monolith is a reasonable foundation for this system.** HR records, financial records, organizational structures, and approvals have substantial transactional relationships. The evidence does not justify introducing microservices.
- Interfaces exist for workflow evaluation, assignment, storage, image processing, and several application services.
- EF models declare useful constraints and indexes, including a unique employee business identifier, unique lookup names, an active-photo uniqueness constraint, and workflow query indexes.
- Financial values use decimal types. Financial transactions record before/after balances and an actor.
- Workflow runtime entities have `[Timestamp]` concurrency tokens. Action execution explicitly opens a transaction covering much of the transition, task, event, comment, and idempotency work.
- Some services use `IDbContextFactory` and short-lived contexts, demonstrating a suitable pattern already available within the codebase.

### Critical vulnerabilities and anti-patterns

#### T1 — Application boundaries are weak and responsibilities are concentrated

**Severity: High**

The architecture is best described as a **layered, feature-organized monolith with inconsistent boundaries**. The reviewed source does not establish Clean Architecture.

Examples include:

- `EmployeeProfile.razor`: approximately **5,901 lines**.
- `DataImport.razor`: approximately **5,023 lines**.
- `ExcelImportService.cs`: approximately **6,664 lines**.
- UI components perform database access, authorization decisions, financial updates, file handling, and workflow orchestration.
- The import service calls an endpoint-layer executor, reversing the expected dependency direction. Evidence: [ExcelImportService.cs:136](API.HR.Node/Services/Import/ExcelImportService.cs).

**Implication:** sensitive rules are difficult to locate and enforce consistently. The financial authorization defects illustrate the practical consequence of putting business mutations inside large UI components.

#### T2 — Circuit-scoped database access risks overlapping operations

**Severity: High**

Several Interactive Server components and scoped services use a directly injected `ApplicationDbContext`. Examples include [JobClassificationsPage.razor:9](API.HR.Node/Components/Pages/JobClassificationsPage.razor) and the financial service registration in `Program.cs:149`.

A Blazor circuit can outlive an individual interaction. Reusing its context across asynchronous operations creates risks of overlapping EF operations, accumulated tracking, and stale entities.

**This is a lifecycle risk observed in the registration and consumption pattern; no runtime concurrency failure was demonstrated.**

#### T3 — Financial updates can lose concurrent changes

**Severity: High**

[EmployeeFinancialService.cs:146](API.HR.Node/Services/Financial/EmployeeFinancialService.cs) loads an account, computes a new balance in memory, and saves the account and transaction. The account model has no concurrency token.

For an existing account, the balance and transaction are saved together—a useful guarantee—but that does not prevent lost updates.

Example:

1. Two operations read a balance of 100.
2. One deposits 10; another deposits 20.
3. They compute 110 and 120 independently.
4. The final balance can become 120 rather than 130, while both transaction records exist.

Initial account creation also uses a separate save and can race against another creator.

#### T4 — Important “one active record” invariants are not enforced by the database

**Severity: High**

The EF configuration shows:

- A nonunique active-device index: `ApplicationDbContext.cs:425`.
- A nonunique allowance-period index including `EmployeeId` and `IsCurrent`: `ApplicationDbContext.cs:553`.
- A workflow-start check for an existing active instance, without a corresponding unique active-instance constraint: `WorkflowEngine.cs:35` and `WorkflowConfigurations.cs:78`.

Application-level check-then-write logic permits concurrent requests to pass the same check.

Allowance deletion also performs separate saves when deleting the current period and promoting another period. A failure between them can leave no current period. Evidence: [EmployeeAllowanceService.cs:204](API.HR.Node/Services/Allowances/EmployeeAllowanceService.cs).

#### T5 — Transaction and idempotency guarantees are incomplete

**Severity: High**

Although workflow action execution has a transaction, other multi-step operations have weaker boundaries:

- Workflow start saves its instance and subsequent records separately.
- Organizational-unit creation and closure-table updates use multiple saves.
- Hierarchy updates remove and rebuild closure relationships across separate saves.

Evidence: [OrganizationalHierarchyService.cs:208](API.HR.Node/Services/OrganizationalHierarchy/OrganizationalHierarchyService.cs).

Workflow idempotency is also inconsistent:

- The lookup uses **key + instance + action**.
- The database uniqueness constraint uses **key alone**.
- `RequestHash` is not used to validate repeated payloads.
- Cached responses are returned before resource authorization.

Actual EF rowversion protection exists, but malformed client rowversions are silently ignored and the endpoint does not consistently translate `DbUpdateConcurrencyException` into a conflict response.

#### T6 — Workflow assignment can select an inappropriate approver

**Severity: High**

[WorkflowAssignmentResolver.cs:49](API.HR.Node/Services/Workflows/WorkflowAssignmentResolver.cs) falls back to the initiator when a manager cannot be resolved. Malformed assignment configuration also falls back to the initiator.

The legacy formation fallback selects another linked user in the formation without verifying a managerial role.

Additionally:

- The delegation endpoint accepts a task ID but creates a general delegation without checking that task.
- Assignment resolution does not apply the delegation’s workflow-definition scope.
- The “my tasks” query handles direct user assignment, leaving role/department task visibility incomplete.

**Implication:** approval routing and separation of duties remain unreliable even after fixing the missing action authorization.

### Actionable recommendations

1. **Keep the monolith, but establish application command boundaries.** Separate employee records, compensation, finance, identity, and workflow modules. Move mutations and authorization into application services; let endpoints and components invoke those services.
2. Extract the largest pages into task-focused components. Remove service-to-endpoint dependencies and consolidate duplicated device and financial logic.
3. Adopt one short-lived context per operation for circuit-driven work. Pass cancellation tokens and avoid retaining tracked entities between unrelated interactions.
4. Protect financial updates with a concurrency token or an appropriate atomic database operation. Keep the ledger entry and balance mutation in the same transaction; define retry and duplicate-command behavior.
5. Enforce active-record invariants through suitable filtered unique constraints and transactional replacement logic.
6. Make workflow start and hierarchy maintenance atomic. Align idempotency lookup and uniqueness, validate the request fingerprint, and authorize before returning a cached response.
7. Fail closed when no legitimate approver exists. Validate delegation ownership, scope, dates, and recipient eligibility; implement role/department work queues.

---

## 2.2 Security, Identity & Compliance

### Strengths

- ASP.NET Identity provides established password hashing, security stamps, and account-management infrastructure.
- Permission-based policies exist for several sensitive operations, including user administration, photo operations, and employee imports.
- API password checking requests lockout accounting.
- File storage is outside the public web root, and photo processing includes extension, signature, decoding, dimension, and metadata controls.
- Data Protection keys are persisted and protected using Windows DPAPI when applicable.
- No embedded administrator password or populated external-service secret was established in the reviewed configuration.
- Refresh handling checks token expiry and the user security stamp.
- Several self-service reads verify employee ownership.

### Critical vulnerabilities and anti-patterns

#### S1 — Employee self-service permits sensitive financial and allowance writes

**Severity: Critical**

[EmployeeProfile.razor:18](API.HR.Node/Components/Pages/EmployeeProfile.razor) requires authentication, and its load logic permits an ordinary employee to view their own dossier.

However:

- Allowance editing controls are exposed around lines 1536–1573.
- Financial transaction and balance-editing controls are exposed around lines 1808–1832.
- `SaveAllowancePackageAsync` uses the authenticated name as attribution without checking a compensation permission.
- `SaveNewTransactionAsync` and `SaveUpdatedBalancesAsync` similarly lack authorization checks.
- Their underlying services do not enforce those permissions.

Evidence: [allowance save:5136](API.HR.Node/Components/Pages/EmployeeProfile.razor), [transaction save:5801](API.HR.Node/Components/Pages/EmployeeProfile.razor), [balance save:5856](API.HR.Node/Components/Pages/EmployeeProfile.razor).

Allowance-history deletion follows the same weak pattern.

**Confirmed impact:** the implementation permits unauthorized modification of HR financial and compensation records. Connection to an external payroll payment process is not established.

#### S2 — Workflow authorization stops at authentication

**Severity: Critical**

[WorkflowEndpoints.cs:16](API.HR.Node/Endpoints/WorkflowEndpoints.cs) applies the `ApiBearer` policy. That policy accepts an authenticated principal; it does not establish authority over a particular workflow.

[WorkflowEngine.cs:128](API.HR.Node/Services/Workflows/WorkflowEngine.cs) validates instance state, transition validity, conditions, and concurrency. It does **not** enforce:

- Pending-task assignment.
- Transition `RequiredPermission`.
- Department or entity scope.
- Delegation eligibility.
- Separation of initiator and approver.

The acting user ID is used for attribution rather than access enforcement. Available-action calculation receives a user ID but does not use it to filter actions.

Instance, timeline, and task reads also lack resource-level authorization. Workflow start accepts client-supplied entity references without demonstrated ownership or entity compatibility checks.

**Implication:** authenticated access can become unauthorized workflow control and disclosure.

#### S3 — Custom passkey ceremonies trust client-supplied options

**Severity: Critical**

Registration completion reconstructs options from `request.OptionsJson`. Login completion does the same for assertion options:

[PasskeyEndpoints.cs:182](API.HR.Node/Endpoints/PasskeyEndpoints.cs).

The reviewed path does not retain the original ceremony options server-side with an expiry and single-use consumption rule.

Fido2 4.0.1 validates the assertion against the supplied original challenge and uses the supplied user-verification requirement. Its counter protection does not reject assertions reporting a zero counter. These behaviors make the missing server challenge binding consequential. [Fido2 4.0.1 assertion implementation](https://raw.githubusercontent.com/passwordless-lib/fido2-net-lib/4.0.1/Src/Fido2/AuthenticatorAssertionResponse.cs).

**Risk:** a captured valid assertion from an applicable authenticator can be replayed, and a client can weaken the ceremony’s requested user-verification policy.

**Insufficient data to verify potential risk assumes...** an attacker can obtain a valid assertion and the deployed authenticator’s counter behavior permits the relevant replay. No forged-signature capability was established.

This finding concerns the **custom employee/mobile passkey path**; it should not be generalized to the separate built-in Identity browser passkey implementation.

#### S4 — Authentication paths enforce different account-assurance rules

**Severity: High**

The API password flow and Basic handler call `CheckPasswordSignInAsync`, then construct an authenticated principal. A successful password check does not itself complete the MFA flow that `PasswordSignInAsync` performs. [ASP.NET Core SignInManager implementation](https://github.com/dotnet/aspnetcore/blob/v10.0.11/src/Identity/Core/src/SignInManager.cs).

Other inconsistencies include:

- AD login directly calls `SignInAsync` after directory validation.
- External login explicitly uses `bypassTwoFactor: true`.
- Some account linking relies on email or username-prefix matching rather than a demonstrated stable directory identifier.

Evidence: [BasicAuthenticationHandler.cs](API.HR.Node/Security/BasicAuthenticationHandler.cs), [Login.razor:531](API.HR.Node/Components/Account/Pages/Login.razor), [ExternalLogin.razor:132](API.HR.Node/Components/Account/Pages/ExternalLogin.razor).

**Insufficient data to verify potential risk assumes...** accounts depend on local MFA, local lockout restrictions, or a specific upstream assurance level. Those effective policies were not supplied.

The confirmed defect is inconsistent enforcement, not proof that every deployed account currently uses MFA.

#### S5 — Device binding and revocation do not establish reliable session control

**Severity: High**

Device validation accepts optional client-reported identifiers and can skip binding when identifiers are absent. Model and vendor strings are spoofable and do not prove possession of a specific device.

Device revocation changes `IsActive` without consistently revoking sessions or changing security stamps. `/me` checks whether **any** active device exists for the employee, rather than whether the requesting session belongs to that device.

Evidence: [AuthEndpoints.cs:753](API.HR.Node/Endpoints/AuthEndpoints.cs), [UserDevices.razor:300](API.HR.Node/Components/Pages/UserDevices.razor), [EmployeeEndpoints.cs:346](API.HR.Node/Endpoints/EmployeeEndpoints.cs).

ASP.NET Identity bearer-token validation checks the protected ticket and expiry; it does not automatically look up a user’s current security stamp for every access-token request. [BearerTokenHandler implementation](https://github.com/dotnet/aspnetcore/blob/v10.0.11/src/Security/Authentication/BearerToken/src/BearerTokenHandler.cs).

Role and permission changes also do not consistently update security stamps. Consequently, changes may leave existing principals with stale privileges.

#### S6 — Job-classification import lacks its administrative permission boundary

**Severity: High**

The UI is Admin-only, but `/api/import/job-titles/excel` inherits only authenticated access from its endpoint group.

Other import endpoints explicitly require import permissions; this route does not. Evidence: [ExcelImportEndpoints.cs:313](API.HR.Node/Endpoints/ExcelImportEndpoints.cs).

**Implication:** an ordinary authenticated cookie user can potentially modify shared classification data. Antiforgery requirements do not substitute for authorization.

#### S7 — Employee identifier confusion affects feedback ownership

**Severity: High**

The claims factory emits `employee_id` from `ApplicationUser.EmployeeID`, the employee’s **business identifier**.

The feedback service parses that claim as an integer and uses it as `Employees.Id`, the **database primary key**. The attachment ownership check repeats that interpretation.

Evidence: [ApplicationUserClaimsPrincipalFactory.cs:82](API.HR.Node/Security/ApplicationUserClaimsPrincipalFactory.cs), [EmployeeFeedbackService.cs:122](API.HR.Node/Services/Feedback/EmployeeFeedbackService.cs), attachment ownership around line 389.

**Confirmed defect:** inconsistent identifier semantics can misattribute feedback.

**Insufficient data to verify potential risk assumes...** a numeric employee business identifier matches another employee’s database key. Such a collision could make the attachment ownership check authorize the wrong person.

#### S8 — Cookie-authenticated API mutations have inconsistent CSRF protection

**Severity: High, conditional exploitability**

The `ApiBearer` policy accepts cookies alongside bearer tokens and Basic authentication. Several upload and import routes explicitly disable antiforgery.

A global `UseAntiforgery` call does not establish protection for every arbitrary mutation endpoint.

**Insufficient data to verify potential risk assumes...** the browser sends the authentication cookie with a hostile request, such as from an allowed same-site origin. Effective cookie policy and deployment origins were not supplied.

The code establishes a protection gap; it does not establish that every cross-site request succeeds.

#### S9 — SQL connection settings disable certificate trust validation

**Severity: High**

Both checked-in SQL connection strings contain `TrustServerCertificate=True`:

[appsettings.json:3](API.HR.Node/appsettings.json).

This permits bypassing normal server-certificate trust validation. It is **not evidence that SQL traffic is necessarily plaintext**; encryption and certificate validation are separate properties. [Microsoft SQL client encryption documentation](https://learn.microsoft.com/en-us/sql/connect/ado-net/encryption-and-certificate-validation?view=sql-server-ver17).

**Insufficient data to verify potential risk assumes...** these connection strings are effective in production rather than overridden.

#### S10 — Malware scanning can fail open; upload controls are uneven

**Severity: High**

When Defender is not detected, startup selects `NullFileMalwareScanner`. That scanner returns `Bypassed`, and rejection logic rejects only `Infected` or `Failed`.

Evidence: [Program.cs:122](API.HR.Node/Program.cs), [NullFileMalwareScanner.cs:7](API.HR.Node/Services/Security/NullFileMalwareScanner.cs).

Feedback attachments lack a demonstrated content allowlist comparable to the hardened photo path and retain caller-supplied content type. Other file paths also have different validation coverage.

Private storage and attachment download disposition reduce direct exposure; no uploaded-file code execution was established.

**Insufficient data to verify potential risk assumes...** production starts without the supported scanner, or malicious content reaches a path with weaker validation.

#### S11 — Security and financial audit evidence is incomplete

**Severity: High**

Several sensitive account, role, permission, and linking changes lack corresponding structured audit records.

The financial ledger captures balance transactions, but loan and advance values can be overwritten without equivalent before/after history.

The dossier export service catches failure to persist its verification record and continues generating the document:

[EmployeeDossierPdfService.cs:315](API.HR.Node/Services/Pdf/EmployeeDossierPdfService.cs).

Audit records are stored in the operational database. The source does not establish independent retention, immutability, or administrator separation.

**Insufficient data to verify potential risk assumes...** database administrators or application credentials can alter audit records without an independent evidentiary copy. Database permissions and external archival controls were not provided.

### Actionable recommendations

1. Put authorization inside every sensitive application command. Require explicit finance, compensation, catalog, and workflow permissions plus resource scope. Mirror those rules in the UI.
2. For workflows, authorize reads and actions against the actual entity, pending assignment, transition permission, and valid delegation. Deny self-approval where the business policy requires separation of duties.
3. Store passkey options server-side under an unpredictable ceremony ID. Bind them to user/session/purpose, enforce a short expiry, consume them atomically, and keep RP and user-verification requirements server-controlled.
4. Centralize authentication eligibility and assurance. Use complete supported sign-in flows; require documented upstream assurance before bypassing local MFA. Restrict Basic authentication to an explicitly justified use case.
5. Replace descriptive device strings with session binding and, where required, proof of possession of a device key. Make revocation affect refresh and sensitive access immediately.
6. Standardize employee identifiers. Resolve business identifiers through `Employee.EmployeeID`; use immutable user ownership for feedback wherever possible.
7. Require antiforgery for cookie-authenticated mutations, or make designated API routes token-only and reject cookies.
8. Configure trusted SQL certificates and explicit encryption with `TrustServerCertificate=False`.
9. Fail startup or quarantine uploads when required malware scanning is unavailable. Apply format allowlists, content inspection, and size budgets consistently.
10. Audit sensitive changes with actor, subject, before/after values, outcome, correlation ID, and timestamp. Make critical audit persistence transactional and export evidence to separately controlled storage.

---

## 2.3 Performance, Scalability & Resilience

### Strengths

- Employee-directory queries use database-side paging and projections.
- Photos have version-aware caching behavior.
- Basic authentication has a per-IP request budget.
- HTTP integrations use `HttpClient` infrastructure.
- The file logger has a bounded queue and batch processing.
- Workflow outbox entities exist, providing a foundation for reliable asynchronous delivery.
- Relevant employee, workflow, and audit indexes are declared.

### Critical vulnerabilities and anti-patterns

#### P1 — User lists and photo loading amplify database, memory, and file work

**Severity: Medium**

The directory loads thumbnails sequentially through the photo service, copies them into memory, converts them to Base64, and retains them in circuit-local caches.

Each uncached photo can involve metadata access, a file read, and an audit write. The returned source stream is copied without being disposed in the shown component.

Evidence: [EmployeeDirectory.razor:1287](API.HR.Node/Components/Pages/EmployeeDirectory.razor).

The user-management page loads all users and then queries roles and claims per user: `Users.razor:879`.

**Implication:** growing user counts and concurrent circuits produce more round trips, retained memory, and file-handle pressure. No measured latency or throughput figures were supplied.

#### P2 — Expensive imports, document generation, and integration polling occupy interactive requests

**Severity: High**

Excel parsing constructs `XLWorkbook` synchronously and processes workbook contents in application memory. The reviewed paths do not establish a consistent application-specific compressed-size, decompressed-size, row-count, and processing-time budget.

PDF generation is synchronous after multiple queries. Canva operations contain repeated polling delays.

Evidence: [ExcelImportService.cs:28](API.HR.Node/Services/Import/ExcelImportService.cs), `EmployeeDossierPdfService.cs:380`, and Canva polling logic.

Framework request limits and some row limits exist; this finding does **not** mean all upload sizes are unlimited.

**Implication:** individually valid requests can consume substantial memory and worker time, reducing service capacity for ordinary users.

#### P3 — Notification delivery is not transactionally reliable

**Severity: High**

A workflow handler inserts `MOBILE_PUSH_NOTIFICATION` outbox records, but the reviewed repository contains no demonstrated consumer that processes them:

[WorkContinuationWorkflowHandler.cs:45](API.HR.Node/Services/Workflows/WorkContinuationWorkflowHandler.cs).

Separately, workflow execution calls notification persistence before committing its own transaction. The notification service uses another context, allowing a notification to persist even if the workflow transaction later rolls back. Failures are swallowed.

**Insufficient data to verify potential risk assumes...** no separately deployed outbox consumer exists. None was supplied in the repository or deployment context.

#### P4 — Local state complicates multi-instance operation; Canva coordination is instance-local

**Severity: High**

The implementation stores or coordinates important state locally:

- Private files.
- Data Protection keys protected for the local Windows machine.
- Canva tokens in `canva_token.json`.
- OAuth state in memory.
- Feature settings in an application-local cache.

Canva is registered as a typed HTTP client, while its token lock is an instance field. Multiple service instances therefore do not share that lock, even though they write the same token file.

Evidence: [CanvaService.cs:21](API.HR.Node/Services/Canva/CanvaService.cs), `Program.cs:156`.

**Confirmed risk:** token refresh and file writes lack coordination across service instances.

**Insufficient data to verify potential risk assumes...** the application runs multiple processes or nodes, or concurrent Canva service instances refresh the shared token. Actual hosting topology was not supplied.

#### P5 — The authentication limiter is a shared availability budget

**Severity: Medium**

`auth-limit` uses `AddFixedWindowLimiter` with 10 requests per minute. This registration provides a shared named limiter rather than a per-user or per-IP partition. One client can consume the budget available to other clients. Evidence: [Program.cs:84](API.HR.Node/Program.cs), [framework limiter implementation](https://source.dot.net/Microsoft.AspNetCore.RateLimiting/RateLimiterOptionsExtensions.cs.html).

The separate Basic limiter is partitioned by remote IP, which is a useful distinction.

Reverse-proxy address handling and cluster-wide limit coordination remain unverified.

### Actionable recommendations

1. Batch role and claim reads and paginate user administration. Serve authorized thumbnail URLs instead of transporting Base64 images through component state where practical.
2. Dispose returned file streams deterministically. Establish bounded photo-cache memory, not only entry counts.
3. Move large imports, PDF generation, and long integration work into bounded jobs with progress, cancellation, retry, and idempotency. Keep small operations synchronous only within measured budgets.
4. Write workflow changes and notification outbox records in one transaction. Dispatch after commit using a durable worker with retries, poison-message handling, and delivery metrics.
5. Coordinate Canva token refresh through a shared token store or correctly scoped coordinator; use atomic/versioned writes.
6. If multiple nodes are required, design shared storage, shared key protection, distributed state, and Blazor routing explicitly.
7. Partition authentication limits appropriately and combine account and network abuse controls without creating an easily exhausted global login quota.
8. Establish workload targets before tuning: concurrent circuits, employee volume, import size, p95 latency, memory, database waits, and recovery objectives.

**Architectural trade-off:** reliable queues and shared state add operational cost. Adopt them for the identified workloads and intended hosting model; neither microservices nor a distributed cache is automatically required.

---

## 2.4 UI/UX & Visual/System Design

### Strengths

- Arabic-first document configuration includes `lang="ar"` and RTL direction.
- A centralized design-token system supports multiple brands and light/dark themes.
- All **594 checked contrast pairs passed**. Checked body-text ratios exceeded 14:1, and the lowest checked control-edge ratio was 4.48:1.
- Reusable components exist for dialogs, tabs, grids, badges, and notifications.
- Dialog behavior includes focus management, a Tab trap, and focus restoration.
- Loading, empty, and status states appear in the reviewed UI.
- Theme values are allowlisted, assets are fingerprinted, and selected heavier assets are loaded lazily.

These are meaningful strengths, but token contrast alone does not establish WCAG conformance.

### Critical vulnerabilities and anti-patterns

#### U1 — Sorting and form controls have accessibility defects

**Severity: Medium**

Job-classification table headers have click handlers without equivalent keyboard controls. Several filter selects lack associated labels or explicit accessible names.

Evidence: [JobClassificationsPage.razor:79](API.HR.Node/Components/Pages/JobClassificationsPage.razor), sortable headers around line 184.

Native keyboard-operable buttons are the appropriate control for sorting. Click-only interaction conflicts with keyboard access requirements. [WCAG keyboard guidance](https://www.w3.org/WAI/WCAG22/Understanding/keyboard.html).

Placeholder text alone also provides poor persistent guidance for data-entry controls.

#### U2 — Tabs implement incomplete interaction and relationships

**Severity: Medium**

[DsTabs.razor:4](API.HR.Node/Components/DesignSystem/DsTabs.razor) supplies tab roles and selected state but lacks complete roving tabindex, arrow-key behavior, and tab-to-panel relationships.

The employee-profile tabs repeat a similar custom pattern.

The buttons remain usable through basic Tab/Enter interaction, so this is not a claim that they are entirely keyboard inaccessible. It is an incomplete tab implementation relative to the documented pattern. [WAI-ARIA tabs pattern](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/).

#### U3 — Undefined tokens and classes create design-system drift

**Severity: Medium**

The automated checks found:

| Missing definition | Affected source |
|---|---|
| `--border-subtle`, `--primary`, `--text-high` | Job classifications |
| `--text-1` | `wwwroot/hr-ui.css:1956` |
| `hr-badge--subtle`, `hr-badge--info`, `hr-badge--success`, `hr-badge--accent` | Import and classification interfaces |
| `hr-table-container`, `hr-table-row-hover` | Import and classification tables |

Several undefined variables have no fallback. Undefined badge variants lose their intended styling, although the base badge class still exists.

The classification table has an inline overflow rule; the missing wrapper class alone does not prove that table overflow is broken.

#### U4 — Content Security Policy conflicts with implemented UI features

**Severity: High**

The CSP permits self-hosted and inline scripts but does not permit `unsafe-eval`.

Several components invoke the browser’s `eval` through JS interop, including geolocation, preferences, and report-related behavior.

Evidence: [Program.cs:412](API.HR.Node/Program.cs), [EmployeeProfile.razor:5466](API.HR.Node/Components/Pages/EmployeeProfile.razor), `CommandPalette.razor:155`, and `ReportsSlider.razor:518`.

**Implication:** these calls conflict with the shipped policy and should fail under normal browser CSP enforcement.

The existing `unsafe-inline` allowance also weakens the policy’s containment value. No exploitable XSS was established in this review.

#### U5 — Dossier verification overstates what has been verified

**Severity: High**

The verification UI presents a broad current-data assurance, but its digest covers selected core employee fields, not all content included in the dossier.

Evidence: [DossierVerificationHelper.cs:46](API.HR.Node/Services/Pdf/DossierVerificationHelper.cs).

Consequences include:

- Changes to financial, allowance, course, leave, or other dossier sections can escape the freshness comparison.
- The check does not verify the bytes of the presented PDF.
- `isDataCurrent` starts as `true` and remains true if the current employee cannot be found.
- Export can continue after its verification record fails to persist.
- The anonymous verification page reveals selected employee metadata to anyone possessing the URL.

Evidence: [VerifyDossier.razor:433](API.HR.Node/Components/Pages/VerifyDossier.razor).

The random opaque token is a strength, but possession of the URL functions as access authority.

**Insufficient data to verify potential risk assumes...** recipients treat the page as validation of the entire presented document, or public disclosure exceeds the organization’s approved verification policy.

### Actionable recommendations

1. Use buttons inside sortable headers and expose sort state. Give every search and filter control a persistent label and accessible name.
2. Implement tabs once, with unique IDs, appropriate focus movement, keyboard behavior, and associated panels.
3. Replace undefined variables and classes with approved design-system definitions. Make existing source checks release gates.
4. Replace `eval` calls with named JS module functions. Then tighten inline-script handling using appropriate nonces or hashes.
5. Match verification wording to the actual evidence. For full-document assurance, retain an immutable document snapshot/digest or use a suitable signing process.
6. Treat missing employee data, failed audit persistence, revoked tokens, and expired records as explicit verification failures.
7. Minimize public verification data and define expiry, revocation, caching, and referrer behavior.
8. Complete browser verification with keyboard navigation, screen readers, zoom, narrow viewports, and both themes.

**Insufficient data to verify potential risk assumes...** remaining responsive or assistive-technology defects exist. Source inspection cannot establish their presence or absence; no rendered assessment was performed.

---

## 2.5 Administrative, DevOps & Governance

### Strengths

- A test project and automated design checks are available.
- Configuration supports disabling startup migrations.
- Administrator seeding requires configured credentials and preserves an existing administrator rather than blindly recreating it.
- Ignore rules cover several runtime, local configuration, and development artifacts.
- File logging includes queue bounds, batching, and age-based retention.
- The reviewed SQL dump contains schema and limited lookup/settings inserts; it did not establish an employee-data or password-hash leak.

### Critical vulnerabilities and anti-patterns

#### G1 — Application startup is also a schema-change operation

**Severity: High**

Startup runs the seeder before accepting requests, and migrations default to enabled:

[StartupSeeder.cs:49](API.HR.Node/Data/StartupSeeder.cs), `Program.cs:379`.

EF’s pending-model-change warning is suppressed in both context registrations.

**Implication:** ordinary application startup is coupled to database availability and schema mutation. Multiple instances starting together and incompatible application/schema changes require controls beyond the shown code.

**Insufficient data to verify potential risk assumes...** production retains the default migration setting. Effective deployment configuration and database privileges were not supplied.

#### G2 — Passing tests do not cover the highest-risk boundaries

**Severity: High**

The reviewed workflow tests use EF InMemory and suppress its transaction-ignored warning:

[WorkflowEngineTests.cs:19](API.HR.Node.Tests/WorkflowEngineTests.cs).

The reviewed suite does not establish:

- Real SQL uniqueness and rowversion behavior.
- Rollback across multi-step operations.
- End-to-end authorization across cookie, bearer, and Basic schemes.
- Passkey challenge replay prevention.
- Employee denial of financial writes.
- Workflow assignment enforcement.

InMemory tests are useful for logic, but they cannot validate SQL Server’s transactional and constraint behavior.

#### G3 — Logging can lose evidence without making that loss visible

**Severity: Medium**

The file logger ignores failed queue insertion, catches write failures, and does not retain logging scopes.

Evidence: [FileLogger.cs:43](API.HR.Node/Services/Logging/FileLogger.cs).

Age-based retention is present, but the reviewed implementation does not establish operational alerts for dropped entries, failed writes, disk pressure, or centralized correlation.

**Implication:** incidents can occur while the logging system silently loses the evidence needed to investigate them.

#### G4 — A dependency licensing safeguard is deliberately overridden

**Severity: High, governance**

The repository includes an empty target that overrides the ImageSharp Release license check:

[Directory.Build.targets:2](Directory.Build.targets).

The application also selects QuestPDF’s Community license mode.

**Confirmed finding:** the build suppresses a vendor licensing safeguard.

**Insufficient data to verify potential risk assumes...** the organization lacks the entitlement or eligibility required for its dependency usage. This audit does not establish a license violation.

#### G5 — Production configuration can depend on development secret infrastructure

**Severity: Medium**

`Program.cs:44` adds User Secrets outside development after other configuration sources, allowing local user-profile configuration to override earlier values.

Evidence: [Program.cs:36](API.HR.Node/Program.cs).

**Implication:** effective production behavior can depend on the service account’s local profile and configuration precedence. This complicates reproducibility and auditability.

No secret values were inspected outside the supplied workspace.

#### G6 — Deployment, recovery, and observability evidence is insufficient

**Severity: High, verification gap**

The snapshot does not establish reviewed CI/CD gates, infrastructure definitions, readiness checks, centralized monitoring, recovery runbooks, or tested backups.

That is an evidence limitation—not proof that the organization has none.

**Insufficient data to verify potential risk assumes...** these controls are not supplied by separately managed infrastructure or operational teams.

The same limitation applies to:

- SQL HA and failover.
- Backup encryption and restore tests.
- File-storage and Data Protection key recovery.
- RPO/RTO.
- Database-at-rest encryption.
- Host hardening and network segmentation.
- TLS termination and AD transport controls.
- Production dependency vulnerability scanning.

The local SQL/file/key dependencies are visible; their actual deployment redundancy is not.

#### G7 — Sensitive-data governance is not established

**Severity: Medium, verification gap**

The system handles identity, family, residence, employment, and financial information. Broad staff categories also appear in access logic, but the repository does not establish the organization’s approved need-to-know model.

**Insufficient data to verify potential risk assumes...** formal data classification, retention, access recertification, privacy review, and export controls are absent outside the repository.

No jurisdiction, regulatory mandate, or certification target was supplied. Therefore, this audit does not claim a specific legal or regulatory violation.

### Actionable recommendations

1. Run production migrations as a controlled deployment step using a separate migration identity. Keep ordinary application credentials limited to necessary runtime operations.
2. Restore model-drift detection. Validate migrations against a representative SQL Server database before release.
3. Add HTTP-level negative authorization tests and relational integrity tests covering the critical findings.
4. Capture structured correlation data and metrics for failed/dropped logs, failed authentication, authorization denials, imports, notification backlog, and database latency.
5. Verify dependency entitlement and restore vendor checks or document an authorized, validated alternative.
6. Define configuration precedence explicitly. Use an approved production secret source and startup validation.
7. Obtain and test the actual deployment and recovery design, including joint restoration of SQL data, private files, and encryption keys.
8. Assign owners for data classification, retention, privileged-access review, public verification, and sensitive export policy.

# 3. Consolidated Action Matrix

Severity reflects the observed defect or the urgency of verifying a conditional risk. Rows marked **verification gap** are not assertions that an unseen control is absent. Effort is relative engineering effort, not a delivery estimate.

| Domain | Finding / Issue | Severity (Critical/High/Medium/Low) | Effort to Fix (Low/Med/High) | Recommended Action |
|---|---|---|---|---|
| Architecture | **T1:** Oversized components and reversed dependencies | High | High | Establish application commands and module boundaries; extract focused components. |
| Architecture | **T2:** Circuit-scoped EF context reuse | High | Med | Use a short-lived context per operation and explicit cancellation. |
| Architecture | **T3:** Financial lost updates and creation races | High | Med | Add concurrency protection, atomic ledger updates, and retry/idempotency rules. |
| Architecture | **T4:** Active-record invariants enforced only in code | High | Med | Add suitable unique constraints and transactional replacement logic. |
| Architecture | **T5:** Partial atomicity and inconsistent idempotency | High | Med | Enclose multi-step writes in transactions; align keys, fingerprints, and conflict responses. |
| Architecture | **T6:** Unsafe approver and delegation fallbacks | High | Med | Fail closed; verify managers and task-specific delegation scope. |
| Security | **S1:** Employee financial and allowance writes | **Critical** | Med | Enforce finance/compensation authorization in application services immediately. |
| Security | **S2:** Missing workflow resource/action authorization | **Critical** | Med | Check assignment, permission, delegation, and entity scope on reads and mutations. |
| Security | **S3:** Client-controlled passkey ceremony options | **Critical** | Med | Store, bind, expire, and atomically consume server-controlled ceremony state. |
| Security | **S4:** Inconsistent MFA and account eligibility | High | Med | Centralize assurance rules and use complete sign-in flows. |
| Security | **S5:** Weak device binding and delayed revocation | High | High | Bind sessions to verifiable device/session identity and enforce revocation. |
| Security | **S6:** Underprotected classification import | High | Low | Require the appropriate administrative/catalog permission. |
| Security | **S7:** Business identifier confused with database key | High | Low | Standardize claim semantics and correct ownership resolution. |
| Security | **S8:** Inconsistent CSRF protection | High | Med | Protect cookie mutations or separate token-only endpoints. |
| Security | **S9:** SQL certificate validation bypass | High | Med | Provision trusted certificates and disable trust bypass. |
| Security | **S10:** Optional scanning and uneven upload validation | High | Med | Require scanner health or quarantine; unify content and size controls. |
| Security | **S11:** Incomplete sensitive-change audit evidence | High | High | Record before/after changes transactionally and archive independently. |
| Performance | **P1:** N+1 reads, Base64 thumbnails, undisposed streams | Medium | Med | Batch queries, use authorized image delivery, and dispose streams. |
| Performance | **P2:** Expensive processing in interactive requests | High | High | Add bounded jobs, processing budgets, progress, and cancellation. |
| Resilience | **P3:** Unproven outbox consumer and premature notifications | High | Med | Commit outbox with business changes; dispatch reliably afterward. |
| Resilience | **P4:** Local state and Canva token-refresh races | High | Med | Coordinate token refresh and design shared state for the intended topology. |
| Performance | **P5:** Shared authentication request quota | Medium | Low | Partition limits and verify proxy/client addressing. |
| UI/UX | **U1:** Click-only sorting and unnamed filters | Medium | Low | Use sortable buttons and persistent associated labels. |
| UI/UX | **U2:** Incomplete accessible tab behavior | Medium | Med | Implement focus management, keyboard interaction, and panel associations. |
| UI/UX | **U3:** Undefined design tokens and classes | Medium | Low | Correct references and enforce existing checks in release gates. |
| UI/UX | **U4:** CSP conflicts with `eval` interop | High | Med | Replace `eval` with named modules and tighten script policy. |
| UI/UX | **U5:** Verification claims exceed verification coverage | High | Med | Verify the intended snapshot/document; fail closed and correct wording. |
| DevOps | **G1:** Startup migrations and suppressed model drift | High | Med | Separate migration deployment and restore drift detection. |
| Governance | **G2:** Missing security and relational test assurance | High | Med | Add HTTP authorization, replay, SQL concurrency, and rollback tests. |
| Operations | **G3:** Silent logging loss | Medium | Med | Expose loss/failure metrics and centralized structured correlation. |
| Governance | **G4:** Licensing safeguard override | High | Low | Verify entitlement and restore or formally validate licensing controls. |
| DevOps | **G5:** Production User Secrets precedence | Medium | Low | Define deterministic configuration and approved secret providers. |
| Resilience | **G6:** Deployment and recovery verification gap | High | High | Review topology and demonstrate backup restoration against agreed RPO/RTO. |
| Governance | **G7:** Sensitive-data policy verification gap | Medium | Med | Establish classification, retention, access review, and export ownership. |

## Remediation order and release acceptance

1. **Contain the critical access paths first.** Restrict financial writes, workflow actions, and custom passkey completion. Review historical changes and authentication evidence for the affected paths.
2. **Repair trust boundaries before refactoring broadly.** Implement application-level authorization, server-bound passkey challenges, consistent account assurance, and reliable session revocation.
3. **Protect data integrity.** Address financial concurrency, active-record constraints, idempotency, assignment rules, and transactional outbox delivery.
4. **Improve operational and user-facing reliability.** Fix CSP conflicts, accessibility defects, unresolved styles, processing budgets, and observability.
5. **Validate the actual deployment.** Demonstrate recovery, secret handling, certificate validation, dependency entitlement, and approved data governance.

At minimum, the next release should demonstrate that:

- An ordinary employee cannot change financial or compensation records.
- An unassigned user cannot read or act on another workflow.
- An expired, altered, or reused passkey ceremony is rejected.
- Concurrent financial commands preserve both the balance and ledger.
- Revoked access behaves according to a documented, tested revocation window.
- Failed workflow transactions cannot emit successful-action notifications.
- Dossier verification does not report success when supporting evidence is missing.
- Existing token, CSS-variable, and CSS-class checks pass, followed by keyboard and browser verification.
