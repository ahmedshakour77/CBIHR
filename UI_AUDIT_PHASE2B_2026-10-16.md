# UI/UX Audit: Phase 2B, People Views

**Date:** 2026-10-16
**Scope:**
- `Components/Pages/EmployeeDirectory.razor` (2,502 lines)
- `Components/Pages/EmployeeProfile.razor` (7,522 lines)
- `Components/Pages/EmployeeGallery.razor` (439 lines)
- `Components/Pages/BirthdaysPage.razor` (1,413 lines)
- The services they call: `UserAvatarService` and the profile-photo endpoints

**Method:** code-level audit (impeccable `audit`) plus the impeccable design detector. Not checked in a browser: the pages need a login.
**Status:** report only. No code has been changed yet.

---

## Health score

| # | Dimension | Score | Biggest problem |
|---|---|---|---|
| 1 | Accessibility | 2 / 4 | 7 hand-built dialogs have no accessible name, and 52 table headers have no `scope` |
| 2 | Performance | 1 / 4 | The gallery and birthdays pages embed every employee's photo in the page at once |
| 3 | Responsive | 3 / 4 | Tables sit in `.hr-tablewrap` and the directory has cards and a page-size control; no structural breakage found |
| 4 | Theming | 2 / 4 | The directory's expanded row and the profile's vehicle cards fall back to light hex colours, so they stay white in dark mode |
| 5 | Implementation integrity | 2 / 4 | 759 inline styles, a global `<style>` block inside a page, a database write during page load, and no Kurdish |
| | **Total** | **10 / 20** | **Acceptable: significant work needed** |

Rating bands: 18–20 Excellent · 14–17 Good · 10–13 Acceptable · 6–9 Poor · 0–5 Critical

### Integrity verdict: fail

- **What's solid:** the directory is the best page in the app so far. It pages on the server, sorts with `aria-sort`, debounces search with cancellation, has a captioned table and handles empty states.
- **What drags the batch down:**
  - The profile has grown into a 7,500-line page with its own dialogs and roughly 500 inline styles.
  - The gallery and birthdays pages chose a photo-loading approach that doesn't scale.

The detector found 2 issues, both real (P3 item 16).

### Severity counts

| P0 | P1 | P2 | P3 |
|---|---|---|---|
| 0 | 5 | 7 | 4 |

---

## P1: major (fix before release)

### 1. The gallery embeds every employee's photo in the page as text

- **Where:** `EmployeeGallery.razor:283-325`
- **Category:** Performance.
- **Problem:**
  - The page loads **all** employees (the `.Take(500)` limit is commented out).
  - For each one with a photo, it then fetches the thumbnail one at a time.
  - Each thumbnail is converted to base64 text and kept in the Blazor session's memory as a `data:` URL.
- **Impact:**
  - With 3,000 photos at about 8 KB each, every visitor holds roughly 24 MB on the server.
  - The first render pushes all of it over the live connection.
  - `loading="lazy"` does nothing, because a `data:` URL is already in the page.
  - The page also logs `"127.0.0.1"` as the client IP for every photo read, which makes the photo audit trail wrong.
- **Fix:**
  - Render `<img src="api/employees/{id}/profile-photo/thumbnail" loading="lazy">`. That endpoint already exists, and its `ApiBearer` policy accepts the login cookie (`IdentityConstants.ApplicationScheme`).
  - The browser then fetches and caches only the tiles that scroll into view, and the endpoint logs the real IP.
  - Add paging, or the `Virtualize` component, for the grid.

### 2. The birthdays page does the same, all at once

- **Where:** `BirthdaysPage.razor:1206-1220`, `Services/ProfilePhotos/UserAvatarService.cs:15, 27-80`
- **Category:** Performance and server stability.
- **Problem:**
  - The page starts one avatar lookup per employee with a birth date, all at the same time (`Task.WhenAll`). Each lookup opens its own database scope.
  - The results are base64 `data:` URLs, rendered directly into the page.
  - `UserAvatarService` stores them in a `ConcurrentDictionary` that never expires, so the server keeps every employee's photo in memory for as long as it runs.
- **Fix:**
  - Use the same thumbnail URL as item 1.
  - At minimum, limit how many lookups run at once (`SemaphoreSlim`) and replace the dictionary with `IMemoryCache` with a size limit and an expiry.

### 3. Seven hand-built dialogs have no accessible name

- **Where:**
  - `EmployeeProfile.razor:4109` (martyr), `:4197` (residence), `:4461` (card lightbox), `:4488` (passport), `:4681` (add transaction), `:4779` (update balances)
  - `BirthdaysPage.razor:1044` (greeting)
- **Category:** Accessibility, WCAG 4.1.2.
- **Problem:** each has `role="dialog"` and `aria-modal` but no `aria-labelledby`, so a screen reader announces only "dialog". The allowance dialog (`:3868`) is the one that does it right.
- **Fix:** add `aria-labelledby` pointing at each dialog's existing title. Phase 3 replaces these with the shared `<Modal>`.

### 4. 52 table headers have no `scope`

- **Where:** `EmployeeProfile.razor` (34 headers across the martyrs, leaves, courses, letters, penalties and movements tables, e.g. `:1149-1157`, `:1440`); `BirthdaysPage.razor` (all 18, e.g. `:585-587`)
- **Category:** Accessibility, WCAG 1.3.1.
- **Fix:** add `scope="col"` to each. In the profile's martyrs table, the `#` column header also needs a screen-reader name (`<span class="hr-sr">التسلسل</span>`).

### 5. Staff may be able to view any employee, outside their formation (needs your decision)

- **Where:** `EmployeeProfile.razor:5233-5260`; also `EmployeeDirectory.razor`
- **Category:** Access control. This isn't a UI defect, but the audit found it.
- **What the code does:**
  - The login adds a `formation_id` claim for formation-scoped accounts (`ApplicationUserClaimsPrincipalFactory.cs:111`), and `FormationKpiService` honours it.
  - The profile treats every `FormationManager` and `Supervisor` as general staff. Any of them can open `/employees/{any id}` and see the full record, including the financial and allowances tabs.
  - The directory doesn't filter by formation either.
- **Question:** are formation managers supposed to be limited to their own formation? If yes, both pages need the same check `FormationKpiService` uses. If no, nothing needs to change.

---

## P2: minor

### 6. A database error tells the employee their account isn't linked

- **Where:** `EmployeeProfile.razor:5499-5503`, and the message at `:171-183`
- **Problem:** the `catch` sets the same `loadFailed` flag as "no linked record". An employee hitting a database fault is told "no record is linked to your account, contact HR". This is the same bug that was fixed on the old portal page.
- **Fix:** add a separate `loadError` state with a retry message.

### 7. A staff member with no linked record sees someone else's file

- **Where:** `EmployeeProfile.razor:5256-5259`
- **Problem:** at `/portal` or `/my-profile`, a staff account with no linked employee gets **the first employee in the database**, presented as their own file.
- **Fix:** send staff without a link to the directory (`employees`), or show the "not linked" state.

### 8. Opening a profile writes to the database

- **Where:** `EmployeeProfile.razor:~5470-5490`
- **Category:** Implementation integrity.
- **Problem:** if the employee has no administrative unit but manages one, simply *viewing* the profile saves that unit onto their record (`SaveChangesAsync`). This happens even for an ordinary employee viewing their own file. It isn't audited and can't be seen.
- **Fix:** use `effectiveUnit` for display only, and move the assignment into the hierarchy or admin flow.

### 9. The directory's expanded row ignores the theme

- **Where:** `EmployeeDirectory.razor:759-1020`, and the `<style>` block at `:1383-1480`
- **Category:** Theming.
- **Problem:** it uses tokens that don't exist (`--s-card`, `--s-subtle`, `--b-mute`, `--t-loud`, `--t-mute`, `--font-mono`), so their light fallbacks apply: `#fff`, `#f8fafc`, `#e2e8f0`, `#0f172a`. One element hard-codes `background:#fff`. In dark mode the detail panel is a bright white block.
- **Fix:** map to the real tokens: `--surface`, `--surface-sunken`, `--line-soft`, `--text`, `--text-muted`.

### 10. The profile has the same fallback problem in three places

- **Where:**
  - `EmployeeProfile.razor:3728-3795`: vehicle cards use `--b-mute` and `--s-subtle`.
  - `:1997-2000`: a section uses `--brand-700`, which falls back to `#0284c7`, a blue that isn't in the palette.
- **Fix:** use `--line`, `--surface-sunken`, and `--series-2` or `--accent`.

### 11. The page's `<style>` block applies to the whole app

- **Where:** `EmployeeDirectory.razor:1383`
- **Problem:** a `<style>` element inside a component isn't scoped. `.hr-info-*`, `.hr-gate-pill` and `.iq-license-plate` apply app-wide once the page has been visited, and only then.
- **Fix:** move them to `hr-ui.css`, or to a `EmployeeDirectory.razor.css` file, which Blazor scopes automatically.
- **Note:** `.iq-license-plate` uses literal hex colours on purpose: it reproduces an Iraqi number plate. Keep those colours.

### 12. Clicking the dark background closes a dialog even mid-drag

- **Where:** the 7 profile dialogs and the 1 birthdays dialog
- **Problem:** the background closes the dialog on `click`. Selecting text inside the dialog and releasing the mouse outside it closes the dialog and loses the entry, for example in "add transaction". The shared `<Modal>` already guards against this (`pressedOnScrim`, Phase 1).
- **Fix:** use the same guard until Phase 3 replaces these with `<Modal>`.

---

## P3: polish

| # | Issue | Where | Fix |
|---|---|---|---|
| 13 | 15 profile images have no `loading="lazy"`. The document images in the docs tab only load when the tab is open, so the impact is low | `EmployeeProfile.razor:1713-2193` | Add `loading="lazy" decoding="async"` |
| 14 | The profile loads the allowances and financial data with the main record, even when those tabs are never opened | `EmployeeProfile.razor:5495-5496` | Load each tab's data the first time the tab is opened |
| 15 | 759 inline styles: 513 in the profile, 175 in birthdays, 64 in the directory. The same layout snippets repeat | All four pages | Reuse `.hr-card-pad`, `.hr-fd-tile*` and the other classes from 2A |
| 16 | The detector flagged a thick coloured top edge on the directory's vehicle card and a thick side edge on the birthday generation cards | `EmployeeDirectory.razor:949`, `BirthdaysPage.razor:689` | Replace each with a small coloured dot, as on Formation |

---

## Working well

- **The directory is the model to copy:**
  - Server-side paging (`Skip`/`Take`) with a page-size control.
  - Sortable headers with `aria-sort` and screen-reader sort text.
  - Search debounced with a `CancellationTokenSource`.
  - A captioned table, and row-expansion buttons with `aria-expanded` and descriptive labels.
- **The profile's tabs** use `aria-selected` with explicit strings, and its photo has a descriptive `alt`.
- **Every image in all four pages has `alt` text.** The decorative ones next to visible names use `alt=""`, which is correct.
- **The gallery tiles are buttons** with full accessible names (name, ID, action) and a real empty state.
- **The allowance dialog** (`EmployeeProfile.razor:3868`) is the correct pattern for the others: labelled, with Escape handling.

---

## Not in scope here

- **Kurdish:** none of the four pages uses `Lang.T` or `IsKurdish`. Added to the separate Kurdish translation task (see the end of the 2A report).
- **Moving the 8 hand-built dialogs onto the shared `<Modal>`:** that's Phase 3 (C4). Items 3 and 12 are the minimum fixes for now.

---

## Proposed fix pass

| Step | Items | Command |
|---|---|---|
| 1 | 1, 2: photos as lazy-loaded URLs, a bounded avatar cache | `/impeccable optimize` |
| 2 | 3, 4, 12: dialog names, `scope` on headers, the background-click guard | `/impeccable harden` |
| 3 | 6, 7, 8: error vs not-linked, no someone-else's file, no write on view | `/impeccable harden` |
| 4 | 9, 10, 11: theme tokens, scoped CSS | `/impeccable polish` |
| 5 | 13–16: lazy images, per-tab loading, inline styles, detector findings | `/impeccable polish` |
| — | 5: formation scoping, which needs your decision | — |

**After the fixes:** build, run all 110 tests, and re-run the detector on all four pages.

---

## Phase 2 roadmap

| Batch | Pages | Status |
|---|---|---|
| 2A: Dashboards | Home, FormationDashboard (PortalDashboard deleted) | Done |
| **2B: People views** | EmployeeDirectory, EmployeeProfile, EmployeeGallery, BirthdaysPage | Audited (this report) |
| 2C: Tables and reports | Leaves, MonthlySalariesPage, AuditLogsPage, ReportsSlider, CanvaReportsPage, AdminControlPanel | Not started |
| 2D: Structure and maps | OrganizationalHierarchyPage, JobHierarchyPage, ResidencesMapPage | Not started |
