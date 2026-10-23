# UI Audit: Whole Application

**Date:** 2026-10-17
**Scope:** every Razor component under `API.HR.Node/Components` (110 files, ~55,000 lines) plus `wwwroot/hr-ui.css`, `wwwroot/app.css` and `wwwroot/css/hr-components.css`.
**Method:** a code-level audit (impeccable `audit`), the impeccable design detector, the project's own checks (`check-tokens`, `check-classes`, `check-vars`), and custom scans that respect quoted attributes. Every finding below was checked against the source. I did not open the app in a browser.
**Status:** report only. No code has been changed.

---

## Health score

| # | Dimension | Score | Biggest problem |
|---|---|---|---|
| 1 | Accessibility | 2 / 4 | 11 hand-built dialogs have no accessible name, and 96 form fields have labels that are not linked to them |
| 2 | Performance | 1 / 4 | The gallery and leaves pages still turn every employee photo into base64 text held in the server session |
| 3 | Responsive | 3 / 4 | The layouts hold up. The vehicles table won't scroll sideways, and small text buttons are only 32px tall |
| 4 | Theming | 2 / 4 | 50 colour reads use token names that don't exist, so they fall back to light-mode hex colours and stay light in dark mode |
| 5 | Implementation integrity | 2 / 4 | 3,119 inline styles, 19 class names no stylesheet defines, and 8 `<style>` blocks inside pages |
| | **Total** | **10 / 20** | **Acceptable: significant work needed** |

Rating bands: 18–20 Excellent · 14–17 Good · 10–13 Acceptable · 6–9 Poor · 0–5 Critical

### Integrity verdict: fail

- **The system is strong.** `design/tokens.json` generates every token, and `check-tokens` checks **594 contrast pairs across 11 brands × 2 themes. All pass.** Shared components (`Modal`, `DsDrawer`, `CommandPalette`, `FloatingDock`, `MobileNav`) are labelled correctly. All 89 icon-only buttons have an accessible name.
- **The pages don't follow it.** Large pages (`EmployeeProfile` at 7,522 lines, `DataImport` at 5,023, `Home` at 4,525) build their own dialogs and styling inline. Three pages still use token names from an older system that `hr-tokens.css` no longer declares.
- **Detector:** 16 findings.
  - 10 × `overused-font` (Inter) are **false positives**. PRODUCT.md and DESIGN.md pin Inter for tabular figures.
  - 6 × `layout-transition`. Four are documented exceptions in DESIGN.md §7 (`.hr-meter-fill`, `.hr-split-a`, `.hr-deck-meter-fill`, `.hr-col-bar`). Two are not documented (P3 item 14).

### Severity counts

| P0 | P1 | P2 | P3 |
|---|---|---|---|
| 0 | 5 | 7 | 4 |

### Progress since the Phase 2B audit (2026-10-16)

| Phase 2B finding | Now |
|---|---|
| Gallery embeds every photo as base64 | **Not fixed.** `EmployeeGallery.razor:286` still has `.Take(500)` commented out |
| 7 dialogs without an accessible name | **Worse across the whole app:** 11 |
| 52 `<th>` without `scope` | Whole app: 322 of 475 `<th>` have no `scope` |
| Directory and vehicle cards stay light in dark mode | **Not fixed.** Root cause found: undeclared legacy tokens (P1 item 4) |
| Photo reads logged as `127.0.0.1` | **Not fixed**, and the same pattern is now in `Leaves.razor` too |

---

## P1: major (fix before release)

### 1. Photos are embedded as base64 text in the server session

> **Fixed 2026-10-17.** The gallery, leaves, directory and profile now point `<img>` at the photo endpoint via `Services/ProfilePhotos/ProfilePhotoUrls.cs`. The endpoint revalidates every use (`private, no-cache`) and admits HRManager, FormationManager and Supervisor. **Correction:** the directory and profile were also using base64. Earlier drafts said the directory used the endpoint; that was wrong.

- **Where:**
  - `Pages/EmployeeGallery.razor:315-324`. Every employee who has a photo, with no limit (`.Take(500)` is commented out at line 286).
  - `Pages/Leaves.razor:1344-1353`. One thumbnail for each employee in the leave list.
- **Category:** Performance.
- **What happens:**
  - Thumbnails are fetched one at a time, in sequence.
  - Each one becomes a `data:` URL that stays in the Blazor circuit's memory.
  - The first render sends all of them over SignalR.
- **Impact:**
  - About 8 KB × N photos sits in memory for each connected admin.
  - The first paint is slow.
  - `loading="lazy"` has no effect on `data:` URLs.
  - This works against the ETag-cached REST endpoints that PRODUCT.md says exist for exactly this purpose.
- **Fix:** set `src="/api/employees/{id}/photo/thumbnail?v={ProfilePhotoVersion}"` with `loading="lazy"`. Then delete the base64 loops.
- **Command:** `/impeccable optimize`

### 2. Eleven hand-built dialogs have no accessible name

> **Fixed 2026-10-17 (`harden`).** Each dialog points `aria-labelledby` at its own title, and every modal title is now an `<h2>`. The payslip modal also closes on Escape now.

- **Where:**
  - `BirthdaysPage.razor:1044`
  - `EmployeeFeedbacksPage.razor:405`
  - `EmployeeProfile.razor:4109, 4197, 4461, 4488, 4681, 4779`
  - `MonthlySalariesPage.razor:777`
  - `ResidencesMapPage.razor:566`
  - `VehiclesPage.razor:692`
- **Category:** Accessibility.
- **Problem:** each has `role="dialog" aria-modal="true"` but no `aria-labelledby` or `aria-label`.
- **Impact:** a screen reader announces "dialog" with no title, so the user can't tell which form has just opened.
- **Standard:** WCAG 4.1.2 Name, Role, Value.
- **Fix:** give each modal's heading an `id` and point `aria-labelledby` at it. Better still, move these modals onto `Components/Common/Modal.razor`, which already does this.
- **Command:** `/impeccable harden`

### 3. 96 form fields have labels that aren't linked to them

> **Fixed 2026-10-17 (`harden`).** 66 labels gained `for`/`id` pairs, with ids following the existing `f-<page>-<n>` pattern. 27 filter and search fields that only had a placeholder got an `aria-label`. The housing-type toggle and the vehicle site checkboxes are now labelled groups. A scan finds no unnamed field left.

- **Where:**
  - `EmployeeProfile.razor` (52). Example: lines 3922–3948, the salary-change modal.
  - `VehiclesPage.razor` (13)
  - `AdminLookupsPage.razor` (10). Example: lines 265–284.
  - `BirthdaysPage.razor` (6)
  - `DataImport.razor`, `JobClassificationsPage.razor` and `MonthlySalariesPage.razor` (4 each)
  - `ResidencesMapPage.razor` (2)
  - `JobTitlePages/Index.razor` (1)
- **Category:** Accessibility.
- **Problem:** a `<label class="hr-label">` sits next to the `<input>` but has no `for`/`id` pair. The field isn't nested inside the label either.
- **Impact:**
  - Screen readers announce "edit text" with no name, or only the placeholder.
  - Clicking the label doesn't focus the field.
  - This hits the dates and order numbers in the salary and residence forms.
- **Standard:** WCAG 1.3.1 Info and Relationships, and 4.1.2.
- **Fix:** add `id` and `for` pairs, or wrap the field in the label.
- **Command:** `/impeccable harden`

### 4. Dark mode leaks from legacy token names

> **Fixed 2026-10-17 (`colorize`).** Every legacy name now points at a semantic token, and the hex fallbacks are gone. The expanded directory row also lost its fixed light gradient.

- **Where:**
  - `EmployeeDirectory.razor` (27 reads). Example: lines 759–770, the expanded row.
  - `VehiclesPage.razor` (15). Example: lines 150, 206, 239.
  - `EmployeeProfile.razor` (8)
- **Category:** Theming.
- **Problem:** these pages read `--s-subtle`, `--b-mute`, `--t-loud`, `--s-card`, `--t-mute` and `--brand-700`. None of them is declared anywhere in `hr-tokens.css`, `hr-ui.css` or `hr-components.css`, so the light fallback always wins:
  - `#f8fafc`
  - `#e2e8f0`
  - `#0f172a`
  - `#3b82f6` (a blue that isn't in any of the 11 brands)
- **Also:** `EmployeeDirectory.razor:760` paints a fixed light `rgba(241,245,249,…)` gradient.
- **Impact:**
  - In dark mode the expanded directory row and the vehicle cards show near-black headings on a white panel.
  - They ignore the brand switcher completely.
  - This is the screen admins open most (PRODUCT.md: the roster as the hub).
- **Fix:** map each name to its semantic token:
  - `--s-subtle` → `--surface-sunken`
  - `--b-mute` → `--line-soft`
  - `--t-loud` → `--text`
  - `--t-mute` → `--text-muted`
  - `--brand-700` → `--accent`

  Then remove the hex fallbacks so a missing token shows up in `check-vars` instead of hiding.
- **Command:** `/impeccable colorize` for the token mapping, or `/impeccable polish`

### 5. CSS declarations silently dropped by undeclared tokens without fallbacks

> **Fixed 2026-10-17 (`colorize`).** `check-vars` now reports nothing outside ApexCharts.

- **Where:**
  - `AdminLookupsPage.razor` uses `--color-primary`, `--surface-alt`, `--border-subtle`, `--fs-sm`, `--fs-base` and `--color-danger` (lines 47–403).
  - `JobClassificationsPage.razor` uses `--border-subtle`, `--primary` and `--text-high` (lines 182–325).
  - `MonthlySalariesPage.razor:560` uses `--meta`.
  - `VehiclesPage.razor:382` uses `--t-mute`.
  - `wwwroot/hr-ui.css:2120` uses `color: var(--text-1)`.
- **Category:** Theming / Implementation integrity.
- **Problem:** a `var()` with no fallback that points at an undeclared property is invalid when the value is computed. The browser drops the declaration without any warning.
- **Impact:**
  - Borders vanish.
  - The required-field asterisk loses its danger colour (`AdminLookupsPage.razor:266`).
  - Text sizes fall back to whatever the element inherits.
  - Those two admin lookup pages render half-styled.
- **Fix:** use the real names: `--accent`, `--surface-sunken`, `--line-soft`, `--fs-caption`/`--fs-body`, `--danger`, `--text`, `--text-muted`.
- **Command:** `/impeccable polish`

---

## P2: minor (fix in the next pass)

### 6. 322 of 475 table headers have no `scope`

> **Fixed 2026-10-17 (`harden`).** All 322 now carry `scope="col"`. Every one sat inside `<thead>` or `<HeaderContent>`, so none needed `scope="row"`.

- **Category:** Accessibility (WCAG 1.3.1).
- **Impact:** in the wide HR tables, screen-reader users lose which column a cell belongs to.
- **Fix:** add `scope="col"` to header cells, or add it once in `DsDataGrid` and the shared table markup.
- **Command:** `/impeccable harden`

### 7. Two admin modals aren't dialogs at all

> **Fixed 2026-10-17 (`harden`).** Both now use `Modal.razor`. Their scrim, title, close-button and alert classes weren't defined anywhere, so they also gain a real scrim, a visible close icon and Escape.

- **Where:** `AdminLookupsPage.razor:245` and `:377`.
- **Category:** Accessibility.
- **Problem:** a bare `<div class="hr-modal">` with no `role="dialog"` and no `aria-modal`.
- **Impact:** `hr-dialogs.js` only traps focus inside elements that carry both attributes. Here, Tab escapes into the page behind the modal and focus isn't returned when it closes.
- **Fix:** move both onto `Modal.razor`.
- **Command:** `/impeccable harden`

### 8. 19 class names that no stylesheet defines

> **Fixed 2026-10-17 (`polish`).** `check-classes` reports none undefined.
> - Neutral modifiers (`-neutral`, `--subtle`, `-sm`) are dropped, because plain `hr-badge` is already the neutral badge.
> - Coloured ones map onto §9: `-warning` → `-warn`, `--info`/`--accent` → `-accent`, `--success` → `-ok`.
> - `hr-table-container` → `hr-tablewrap` and `hr-actions` → `hr-rowactions`. The redundant hover classes and `hr-detailpad-rich` are removed.
> - The salaries empty state's emoji became the standard `hr-empty-art` icon plate.
>
> The same pass removed the coloured status stripes from the vehicle cards (vehicles page, profile, directory) and the KPI side stripe on salaries. Each card already shows its status as a badge.

- **Category:** Implementation integrity.
- **Where:** `check-classes` lists them all. The main ones:
  - `hr-badge-neutral` (16 uses)
  - `hr-badge-sm` (8)
  - `hr-badge--subtle` (7)
  - `hr-gate-pill` (5)
  - `hr-table-container`, `hr-table-hover`, `hr-badge--info`, `hr-badge--success`, `hr-detailpad-rich`, `hr-table-wrap`
- **Impact:** these render as unstyled shells. For example, neutral status badges on the profile and formation dashboard have no tint.
- **Fix:** use the vocabulary in DESIGN.md §9: `-accent`/`-ok`/`-warn`/`-danger`, `hr-tablewrap`, `hr-rowactions`.
- **Command:** `/impeccable polish`

### 9. The vehicles table doesn't scroll on narrow screens

> **Fixed 2026-10-17 (`adapt`).** The wrapper is now `hr-tablewrap`, and the 9-column table is `hr-table-wide`, so on a phone or narrow laptop it scrolls inside its own well and its columns keep their width.

- **Where:** `VehiclesPage.razor:344`.
- **Category:** Responsive.
- **Problem:** the table is wrapped in `hr-table-wrap`, which isn't defined (item 8). The correct name is `hr-tablewrap`.
- **Impact:** on a phone the table forces the whole page to scroll sideways. PRODUCT.md principle 5 says no capability may disappear on a phone.
- **Command:** `/impeccable adapt`

### 10. The photo audit trail logs a fake client IP

- **Where:** `EmployeeGallery.razor:317` and `Leaves.razor:1347`.
- **Category:** Implementation integrity.
- **Problem:** both pass `"127.0.0.1"` to `PhotoService.GetPhotoAsync`.
- **Impact:** every gallery or leave-page photo read is recorded against localhost. That undermines PRODUCT.md principle 4 (every photo read is audited). Fixing item 1 removes this too, because the REST endpoint sees the real client.
- **Command:** `/impeccable harden`

### 11. Small text buttons are under the touch-target size

> **Fixed 2026-10-17 (`adapt`).** `.hr-btn-sm` and `.hr-btn-xs` get the icon buttons' invisible 44px ring under `@media (pointer: coarse)`. On a mouse they're unchanged, so clicks in dense rows can't land on a neighbouring button. `DsButton` at size Sm is covered too.

- **Where:** `wwwroot/hr-ui.css:753` (`.hr-btn-sm`, `min-height: 2rem`) and `:5546` (`.hr-btn-xs`, `1.75rem`).
- **Category:** Responsive.
- **Problem:** icon buttons get a 44px invisible hit area (DESIGN.md §8). Text buttons at `-sm` (144 uses) and `-xs` don't.
- **Impact:** row actions are hard to hit on a phone, and phones are a first-class layout here.
- **Standard:** WCAG 2.5.8 (24px minimum) passes. The project's own 44px rule is not met.
- **Fix:** apply the same `::before` hit-area pattern to `.hr-btn-sm` and `.hr-btn-xs` when the pointer is coarse.
- **Command:** `/impeccable adapt`

### 12. Heading structure

> **Fixed 2026-10-17 (`harden`).** Every routable page has exactly one `<h1>`. The profile was already fixed, the Leaves print header is no longer headings, and the residences map title is now the `<h1>`. Eight headings that skipped a level were re-levelled.

- **Where:** `EmployeeProfile.razor` has three `<h1>`. `Leaves.razor` has two. `ResidencesMapPage.razor` has none.
- **Category:** Accessibility (WCAG 1.3.1, 2.4.6).
- **Impact:** screen-reader users who jump by heading can't find the page title.
- **Command:** `/impeccable harden`

---

## P3: polish

### 13. Reduced motion is a global 0.01ms kill switch

> **Fixed 2026-10-18 (`animate`).** Reduced motion now removes travel but keeps change. Keyframes stop; transitions keep only colour, opacity and shadow; overlays fade in over `--t-state`; the spinner keeps turning at half speed. The OS setting and the in-app preference share one rule set. The duplicate kill switch in `hr-components.css`, which zeroed every transition, is gone.

- **Where:** `wwwroot/hr-ui.css:145` and `hr-components.css:39`.
- **Detail:** the spinner exception is thoughtful. Elsewhere, though, drawers and toasts appear with no state cue at all. A short opacity fade would keep the change of state visible without movement.
- **Command:** `/impeccable animate`

### 14. Two undocumented layout transitions

> **Resolved 2026-10-18 (`animate`).** Both are now listed under the DESIGN.md §7 meter exception. The cohort segments are flex siblings that `scaleX` can't express, and `scaleX` would squash the rounded ends of the horizontal fill.

- **Where:** `.hr-horiz-fill` (`hr-ui.css:6268`) and `.hr-stack-segment` (`:6305`).
- **Detail:** both transition `width` and aren't on the DESIGN.md §7 exception list. Either document them or switch to `transform: scaleX`.
- **Command:** `/impeccable optimize`

### 15. `check-vars.cjs` crashes unless it runs from `API.HR.Node/`

- **Where:** `design/check-vars.cjs:11`.
- **Detail:** it walks the relative path `Components` and throws `ENOENT` from the repo root. The sibling checkers run from the repo root without trouble. Resolve the path from `__dirname` so CI can't skip it by accident.

### 16. Inline style volume

- **Detail:** 3,119 `style="…"` attributes. The biggest offenders:
  - `EmployeeProfile` (513)
  - `DataImport` (436)
  - `Home` (230)
  - `MonthlySalariesPage` (227)
  - `OrganizationalHierarchyPage` (191)
- **Impact:** not a defect on its own, but it's where items 4, 5 and 8 hide. No checker can see a hard-coded value in an inline style.
- **Command:** `/impeccable distill`, one page at a time

---

## Patterns and systemic issues

- **Two generations of tokens.** Pages written before the current `tokens.json` still use `--s-*`, `--b-*`, `--t-*`, `--color-*` and `--brand-*` names. Hex fallbacks hide the problem in light mode. A single rename pass fixes P1 items 4 and 5 and part of item 16.
- **Hand-built modals keep reappearing.** `Modal.razor` and `hr-dialogs.js` give a name, a focus trap and focus return for free, yet 13 page-level modals build their own and lose the name. That covers P1 item 2 and P2 item 7.
- **Labels are written but not linked.** The visible label text is right everywhere. Only the `for`/`id` link is missing. This is mechanical to fix.
- **Photos skip their own API.** The ETag REST endpoints existed, but no page used them. *(Fixed 2026-10-17, see P1 item 1.)*

## What's working well

- **The contrast system is verified, not assumed.** 594 pairs pass across every brand and theme, including the Mid anchor that was corrected to clear 3:1 on the ground colour.
- **Every icon-only button (89 of 89) has an accessible name**, often with the employee's name in it, for example "حذف نهائي للموظف …".
- **Shared components are correct:** `Modal`, `DsDrawer`, `CommandPalette`, `FloatingDock`, `MobileNav`, `UserPreferencesModal` and `EmployeeFeedbackFab` all have names, and the app-wide dialog trap is wired up.
- **Heavy libraries load only where needed.** ApexCharts (~0.9 MB), html2pdf (~0.9 MB) and Leaflet aren't in `App.razor`.
- **Charts follow the theme live** through a `MutationObserver` on `data-theme` (`hr-formation-charts.js:189`).
- **RTL and `lang` are set at the root** (`App.razor:33`). The prefs script runs before first paint, so there's no theme flash.
- **The checkers exist.** `check-classes` and `check-vars` are what surfaced items 5 and 8, so they are worth adding to CI.

---

## Recommended actions

1. **[P1] `/impeccable optimize`:** replace the base64 photo loops in `EmployeeGallery` and `Leaves` with the thumbnail REST endpoint plus `loading="lazy"`. This also fixes the fake audit IP.
2. **[P1] `/impeccable colorize`:** map the legacy tokens (`--s-subtle`, `--b-mute`, `--t-loud`, `--color-primary`, `--surface-alt`, `--border-subtle`, …) to semantic tokens in `EmployeeDirectory`, `VehiclesPage`, `EmployeeProfile`, `AdminLookupsPage` and `JobClassificationsPage`, and delete the hex fallbacks.
3. **[P1] `/impeccable harden`:** name the 11 dialogs, move the two `AdminLookupsPage` modals onto `Modal.razor`, link the 96 labels, add `scope` to the table headers, and fix the heading levels.
4. **[P2] `/impeccable adapt`:** fix the vehicles table wrapper and add coarse-pointer hit areas to `.hr-btn-sm` and `.hr-btn-xs`.
5. **[P2] `/impeccable polish`:** clear the 19 undefined classes against the DESIGN.md vocabulary.
6. **[P3] `/impeccable distill`:** reduce inline styles, starting with `EmployeeProfile`.
7. **[P3] `/impeccable animate`:** replace the reduced-motion kill switch with opacity-only fades.
8. **Final step: `/impeccable polish`.**
