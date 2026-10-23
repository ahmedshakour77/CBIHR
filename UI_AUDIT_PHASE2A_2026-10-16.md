# UI/UX Audit: Phase 2A, Dashboards

**Date:** 2026-10-16
**Scope:** `Components/Pages/Home.razor`, `Components/Pages/FormationDashboard.razor`, `Components/Pages/PortalDashboard.razor`, and the chart script `wwwroot/js/hr-formation-charts.js`
**Method:** code-level audit (impeccable `audit`) plus the impeccable design detector. Not checked in a browser: the pages need a login.
**Status:** all findings fixed on 2026-10-16 (see the fix log at the end). Kurdish translation is logged separately. `PortalDashboard.razor` is deleted.

---

## Health score

| # | Dimension | Score | Biggest problem |
|---|---|---|---|
| 1 | Accessibility | 2 / 4 | Screen readers never hear the selected state of tabs and filter chips, and chart data is hidden from them |
| 2 | Performance | 2 / 4 | Each filter change makes about 50 database queries one after another, and a quick second change can show stale numbers |
| 3 | Responsive | 3 / 4 | Grids and tables reflow well; there are some fixed pixel heights and 11px text |
| 4 | Theming | 2 / 4 | Two CSS tokens that don't exist leave Formation cards without borders or rounded corners; charts ignore the theme |
| 5 | Implementation integrity | 2 / 4 | The same breakdowns are drawn two or three times per page, there's English jargon in Arabic labels, and one 1,316-line page can't be reached |
| | **Total** | **11 / 20** | **Acceptable: significant work needed** |

Rating bands: 18–20 Excellent · 14–17 Good · 10–13 Acceptable · 6–9 Poor · 0–5 Critical

### Integrity verdict: fail, but it's fixable

Both dashboards are built on the real design system (`.hr-kpi-*`, `.hr-card`, `.hr-barlist`, `.hr-table`). The problem is accumulation: sections were added beside older sections that show the same thing instead of replacing them.

The detector found 2 issues, both real (P3 item 14).

### Severity counts

| P0 | P1 | P2 | P3 |
|---|---|---|---|
| 0 | 5 (all fixed) | 8 (all fixed) | 3 (all fixed) |

---

## P1: major (fix before release)

### 1. True/false ARIA states are lost (34 places in 8 files) ✅ fixed

- **Where:**
  - `Home.razor:1252-1292`: the 9 analytics-hub chips.
  - `FormationDashboard.razor:496-535`: the 5 tabs.
  - Also `EmployeeDirectory` (8), `WorkflowManagementPage` (6), `Users` (3), `DsTabs`, `DsButton` and `DsIconButton`.
  - The first count of 56 also included 22 bindings in `DataImport`, `EmployeeProfile`, `DsDataGrid`, `SalaryScalePage` and `MyFeedbackPage` that were already correct.
- **Category:** Accessibility, WCAG 4.1.2 (Name, Role, Value).
- **Problem:** code such as `aria-pressed="@(activeTab == "grades")"` passes a C# bool. Blazor drops the attribute entirely when the value is false and doesn't write a valid `"true"` when it's true, so screen readers never announce which tab or chip is selected.
- **Fix:** the pattern already used in the Phase 1 shell (`NotificationBell`, `CommandPalette`, `FloatingDock`, `MobileNav`):
  ```razor
  aria-pressed="@(activeTab == "grades" ? "true" : "false")"
  ```
  The fix is mechanical, so all 34 sites were fixed in one pass.

### 2. The age chart on Home hides its own data from screen readers ✅ fixed

- **Where:** `Home.razor:728`
- **Category:** Accessibility, WCAG 1.3.1.
- **Problem:** `role="img"` on `.hr-col-chart-columns` turns every count and label inside it into decoration, so a screen reader hears only "bar chart of age groups".
- **Fix:** remove `role="img"` and mark the columns as a list (`role="list"` / `role="listitem"`), so the numbers already on screen are read out.

### 3. The 12 ApexCharts on Formation have no text alternative ✅ fixed

- **Where:** `FormationDashboard.razor:330-490` (`gauge-*`, `chart-*` containers).
- **Category:** Accessibility, WCAG 1.1.1.
- **Problem:** each chart is an empty `<div id="chart-…">` with no label or summary.
- **Fix:** give each container `role="img"` and an `aria-label` that states the key figures, for example "Gender: 812 men (71%), 331 women (29%)". Where a table or bar list on the page already shows the same data, the chart can be `aria-hidden` instead.

### 4. The Formation roster filter chips show their state by colour only ✅ fixed

- **Where:** `FormationDashboard.razor:934-948`
- **Category:** Accessibility, WCAG 1.4.1 and 4.1.2.
- **Problem:** the `is-selected` class has no `aria-pressed` to go with it.
- **Fix:** add `aria-pressed="@(rosterFilter == "all" ? "true" : "false")"` and the equivalent to each chip.

### 5. Two CSS tokens don't exist: `--border` and `--radius-md` ✅ fixed

- **Where:** `FormationDashboard.razor:69, 110, 686, 722, 753, 759, 831, 835`
- **Category:** Theming.
- **Problem:** `hr-tokens.css` defines `--line` and `--r-md`, not `--border` or `--radius-md`. A `var()` that doesn't exist invalidates the whole declaration, so these stat tiles currently have no border and square corners.
- **Fix:** `var(--border)` → `var(--line)` and `var(--radius-md)` → `var(--r-md)`.

---

## P2: minor (fix in this pass)

### 6. Overlapping reloads can show stale numbers on Home ✅ fixed

- **Where:** `Home.razor:2958` (`LoadDashboardAnalytics`)
- **Category:** Performance and correctness.
- **Problem:** each filter chip calls `LoadDashboardAnalytics()` with no cancellation. Removing two chips quickly runs two loads at once, and whichever finishes last wins, which may be the older filter.
- **Fix:** keep a load counter (`var seq = ++loadSeq; … if (seq != loadSeq) return;`) or cancel the previous load with a `CancellationTokenSource`.

### 7. Every filter change blanks the whole dashboard ✅ fixed

- **Where:** `Home.razor:252`
- **Category:** Performance (layout shift) and usability.
- **Problem:** `isLoading` swaps all 12 KPI cards for skeletons, so the page jumps on every chip.
- **Fix:** show skeletons only on the first load. On later reloads, keep the current numbers visible but dimmed, and mark the region `aria-busy="true"`.

### 8. About 50 queries run one after another per load ✅ fixed

- **Where:** `Home.razor:2958-3757`
- **Category:** Performance.
- **Problem:** lookups such as statuses, grades and education levels are fetched again on every filter change. The genders table is loaded 4 times per load (`:3034, :3800, :4182, :4221`).
- **Fix:** cache the lookup tables in `IMemoryCache` the same way `PresenceCacheKey` already does, and load genders once per load.

### 9. Charts ignore the theme and reduced motion ✅ fixed

- **Where:** `wwwroot/js/hr-formation-charts.js:13-69`
- **Category:** Theming and accessibility (WCAG 2.3.3).
- **Problems:**
  - Colours are fixed hex values, not the `--series-*` tokens.
  - Switching theme doesn't redraw the charts.
  - The 700ms animation ignores `prefers-reduced-motion`.
- **Fix:**
  - Read colours from `getComputedStyle(document.documentElement)`.
  - Set `animations.enabled = !matchMedia('(prefers-reduced-motion: reduce)').matches`.
  - Redraw when `data-theme` on `<html>` changes (watch it with a `MutationObserver`).

### 10. Each page draws the same data several times ✅ fixed

- **Category:** Implementation integrity.
- **Problem:**
  - Home draws the age pyramid, years of service, grades and formations both in the "executive charts" section (`:681-1237`) and again in the analytics hub (`:1238+`).
  - Formation draws gender 3 times (KPI split, chart, roster chips), and education, grades, age and tenure twice each (chart, then tab).
- **Fix:** **needs your decision** (see below).

### 11. English jargon in Arabic labels (about 20 cases) ✅ fixed

- **Where:**
  - `Home.razor:133, 665, 672, 1005, 1163, 1368, 1594, 1623, 1729, 1739-1743, 2172`
  - `FormationDashboard.razor:450, 472, 475, 672, 713, 955`
- **Examples:** `(Age Demographics)`, `(Tenure Curve)`, an `Area Spline` badge, `(Generational Cohorts)`, `(Basic Treemap)`, `(Heatmap)`, and table headers `(Count)` and `(Percentage)`.
- **Category:** Implementation integrity, UX copy.
- **Problem:** the English describes how the chart is built, not anything the user needs.
- **Fix:** remove it. Keep only file-format names that users recognise (Excel, CSV).

### 12. Distribution bars are marked as progress bars ✅ fixed

- **Where:** `FormationDashboard.razor:570, 599`, and other `hr-meter` uses.
- **Category:** Accessibility.
- **Problem:** a share of the workforce isn't progress, and the bars have no name anyway.
- **Fix:** the percentage is already visible text, so mark the bar `aria-hidden="true"` and remove its role.

### 13. Hard-coded colours ✅ fixed

- **Where:**
  - `FormationDashboard.razor:352, 400`: white text on `--series-4` and `--series-5`, contrast not checked in dark mode.
  - `Home.razor:2244`: `#F59E0B` and `#D97706` on the peak bar.
- **Category:** Theming.
- **Fix:** use the `--series-on` token for text and `--warn` / `--warn-strong` for the bar.

---

## P3: polish

| # | Issue | Where | Fix |
|---|---|---|---|
| 14 | The detector flagged a thick coloured edge on the tenure tiles and a `transition: width` | `FormationDashboard.razor:722`, `Home.razor:1646` | Replace the edge with a small coloured dot; remove the width transition or replace it with a `transform` |
| 15 | About 20 hard-coded `font-size:11px` values | `FormationDashboard.razor` (gauges and badges) | Use `var(--fs-caption)` |
| 16 | 439 inline styles; the same 5–6 patterns repeat (`padding:var(--s-5)` appears 19 times, the stat-tile style 4 times) | Both pages | Move them into classes; a natural follow-up to G9 |

---

## Needs your call

1. **Delete `PortalDashboard.razor` (1,316 lines)?** It's mounted at `/portal-legacy` and nothing links to it. `/portal` and `/portal/profile` are served by `EmployeeProfile.razor`. Deleting it removes code nobody can open.
2. **Remove the duplicate sections (item 10)?**
   - **Proposal:** keep the analytics hub on Home and the tabs on Formation, which hold the detailed versions, and remove the duplicated summary charts above them.
   - **Effect:** roughly 400–600 fewer lines and 6 fewer charts.
   - This removes visible content, so it needs your approval.
3. **Kurdish coverage.**
   - **Current state:** Home has 22 Kurdish-aware strings and Formation has 1. Nearly everything else is Arabic-only, so a Kurdish user sees an Arabic dashboard.
   - **Options:** translate about 300 strings as part of Phase 2, or log it as a separate translation task.

---

## Working well

- Every load has a skeleton and an `isLoading` guard, and Home has a real error state (`loadFailed`).
- Tables use `<caption>`, `scope="col"`, `.hr-tablewrap` and `.hr-num`, and the roster has a proper empty state.
- ApexCharts (~940 KB) is loaded only on Formation (`EnsureChartScriptsAsync`).
- The digital-presence data is cached for 2 minutes and shared by everyone who opens the page.
- Filter chips are real `<button>`s, and the legend rows that act as buttons handle the keyboard (`onkeydown`).
- Each page's headings are in order: one `h1`, then `h2` sections, then `h3` cards.

---

## Proposed fix pass

| Step | Items | Command |
|---|---|---|
| 1 | 1, 2, 3, 4, 12: ARIA states, chart text alternatives, chip state, meter roles | `/impeccable harden` |
| 2 | 5, 9, 13, 15: tokens, chart theming and reduced motion, colours, type sizes | `/impeccable polish` |
| 3 | 6, 7, 8: load cancellation, no blanking on reload, cached lookups | `/impeccable optimize` |
| 4 | 11: remove English jargon | `/impeccable clarify` |
| 5 | 14: detector findings | `/impeccable polish` |
| — | 10, the portal page, Kurdish: wait for your decisions | `/impeccable distill` |

**After the fixes:** build, run all 110 tests, and re-run the detector on all three files.

**Manual check for you (needs a login):**
- Tab through the hub chips and Formation tabs with a screen reader.
- Remove two filter chips quickly on Home and confirm the totals match the remaining filters.
- Switch theme on Formation and confirm the charts change colour.

---

## Phase 2 roadmap

| Batch | Pages | Status |
|---|---|---|
| **2A: Dashboards** | Home, FormationDashboard (PortalDashboard deleted) | Done |
| 2B: People views | EmployeeDirectory, EmployeeProfile, EmployeeGallery, BirthdaysPage | Not started |
| 2C: Tables and reports | Leaves, MonthlySalariesPage, AuditLogsPage, ReportsSlider, CanvaReportsPage, AdminControlPanel | Not started |
| 2D: Structure and maps | OrganizationalHierarchyPage, JobHierarchyPage, ResidencesMapPage | Not started |

---

## Fix log

### 2026-10-16: P1 items 1–5

| # | Change | Files |
|---|---|---|
| 1 | 34 ARIA state bindings now always write `"true"` or `"false"`, the pattern already used in the Phase 1 shell | `Home.razor` (9), `EmployeeDirectory.razor` (8), `WorkflowManagementPage.razor` (6), `FormationDashboard.razor` (5), `Users.razor` (3), `DsTabs.razor`, `DsButton.razor`, `DsIconButton.razor` |
| 2 | The age columns are now a list (`role="list"` / `role="listitem"`). Each item has screen-reader text ("band: N employees (p%)"), and the visual value, bar and label are `aria-hidden` | `Home.razor:728` |
| 3 | 12 chart containers (6 gauges, 6 ApexCharts) have `role="img"` and an `aria-label` built from the data they plot, using new helpers: `GaugeLabel`, `DistributionLabel`, `GenderChartLabel`, `AgePyramidLabel`, `GateChartLabel` | `FormationDashboard.razor` |
| 4 | The 5 roster filter chips have `aria-pressed` | `FormationDashboard.razor` |
| 5 | `var(--border)` → `var(--line)` (6) and `var(--radius-md)` → `var(--r-md)` (7). The same bug was found and fixed once in `Users.razor:538` | `FormationDashboard.razor`, `Users.razor` |

**Verification:**
- `dotnet build` passes. The 4 warnings are old and in files not touched here: `Program.cs` and `AdminLookupsPage.razor`.
- All 110 tests pass.
- The detector finds 1 issue per file, both expected: the P3 thick coloured edge and the width transition (item 14).
- Not checked in a browser: the pages need a login.

**Manual check (needs a login):**
- Screen reader: on Home, the analytics-hub chips should announce "pressed" / "not pressed".
- Screen reader: Formation tabs should announce "selected".
- Screen reader: the Home age chart should read each band with its count.
- Screen reader: each Formation chart should read its summary.
- Visual: the Formation stat tiles (honors and penalties, vehicles) should now show a border and rounded corners.

### 2026-10-16: P2 items 6–9 and 11–13

| # | Change | Files |
|---|---|---|
| 6 | Loads now run one at a time. A filter click during a load queues one more pass, and that pass reads the newest filters, so an older query can never finish last. `LoadDashboardAnalytics` is now a small queue around `LoadDashboardAnalyticsCore` | `Home.razor` |
| 7 | Skeletons appear only on the first load. On later reloads the KPI grid stays visible, dimmed (`.hr-kpi-grid.is-refreshing`, opacity .55, `--t-state` transition), and the section has `aria-busy` | `Home.razor`, `hr-ui.css` |
| 8 | Filter options and their counts (formations, statuses, grades, education, gender, digital presence, years of service) are built once by `BuildFilterOptionsAsync` and cached for 2 minutes. That's about 20 fewer queries per filter change. The female-gender lookup is now one cached helper, `FemaleGenderIdsAsync`, replacing 4 copies. "Refresh data" clears all three caches, so it always reads live figures | `Home.razor` |
| 9 | Chart colours now come from the tokens (`--series-1..6`, `--ok`, `--warn`, `--danger`, `--text`, `--text-muted`, `--line-soft`, `--surface`, `--series-on`, `--accent`), with no hex values left in chart options. Animation is off under `prefers-reduced-motion`. Charts redraw when `data-theme` changes or the OS colour scheme changes | `wwwroot/js/hr-formation-charts.js` |
| 11 | Removed the English labels: 18 in Home, 5 in Formation, plus the "Area Spline" badge. File-format names (Excel, CSV) and the cohort names (`NameEn`, which are data) were kept | `Home.razor`, `FormationDashboard.razor` |
| 12 | 14 share bars changed from `role="progressbar"` to `aria-hidden="true"`; each one's percentage is already visible text beside it | `Home.razor` (11), `FormationDashboard.razor` (3) |
| 13 | Removed 13 hex fallbacks such as `var(--series-4, #ec4899)` (the token is always defined). Fixed 4 cases of white text: on series colours it's now `--series-on`, which is dark in dark mode, and on the accent it's now `--text-on-accent`. The Home peak bar now uses `--warn` instead of hex values | `FormationDashboard.razor`, `Home.razor` |

**Verification:**
- `dotnet build` passes with no new warnings.
- All 110 tests pass.
- `node --check` passes on the chart script.
- The detector finds only the P3 items in both files. `hr-ui.css` has no new findings near the added rule.
- Not checked in a browser.

**Manual check (needs a login):**
- Remove several filter chips quickly on Home. The figures should stay visible and dimmed, then settle on totals that match the remaining filters.
- Click "Refresh data". The counts in the filter dropdowns should update too.
- On Formation, switch theme. The charts should change colour without reloading the page.
- With reduced motion turned on, the charts should appear without animating.
- In dark mode, the "higher degrees" and "contract staff" badges should be readable: dark text on light series colours.

### 2026-10-16: item 10, the deleted portal page, and P3 items 14–16

**Backup:** this repository isn't under git, so the files changed in this pass were copied to `.archive/2026-10-16-phase2a/` first. They are `Home.razor`, `FormationDashboard.razor`, `PortalDashboard.razor` and `hr-formation-charts.js`.

| # | Change | Files |
|---|---|---|
| — | **Deleted `PortalDashboard.razor`** (1,316 lines). It was mounted at `/portal-legacy`, nothing linked to it, and `/portal` is served by `EmployeeProfile.razor` | `Components/Pages/PortalDashboard.razor` |
| 10 | **Home:** removed the four executive cards that repeated the analytics hub: age, years of service, formations and grades. The section keeps the two views found nowhere else, the heatmap and the treemap, and its subtitle now describes them | `Home.razor` |
| 10 | **Formation:** removed the six ApexCharts (gender, education, age pyramid, grades, tenure, gate access) and the "refresh charts" button. Every one of them is shown in detail in the tabs. The 6 health gauges stay. Also removed: `RefreshChartsAsync`, the four `aria-label` helpers those charts used, and their drawing code in the script (717 → about 200 lines) | `FormationDashboard.razor`, `hr-formation-charts.js` |
| 14 | The tenure tiles' thick coloured edge is now a 0.5rem dot before the label. The `transition: width` on the cohort bar is removed. The detector now reports **0** issues on both pages | `FormationDashboard.razor`, `Home.razor` |
| 15 | All `font-size:11px` (10 places) and `10px` (3 places) now use `--fs-caption` | `FormationDashboard.razor` |
| 16 | Repeated inline styles are now classes in a new section 11z of `hr-ui.css`: `.hr-card-pad`, `.hr-card-pad-lg`, `.hr-filter-tag`, `.hr-filter-tag-remove`, `.hr-fd-grid`, `.hr-fd-gauge*` and `.hr-fd-tile*`. Inline styles fell from 288 → 230 on Home and 150 → 91 on Formation | `Home.razor`, `FormationDashboard.razor`, `hr-ui.css` |

**Two extra fixes from the same pass:**
- **The filter grids could overflow on phones.** The `minmax(22rem, 1fr)` grids were wider than a phone screen. `.hr-fd-grid` now uses `minmax(min(var(--fd-min), 100%), 1fr)`.
- **The remove (x) buttons on Home's filter tags were too small.** The tag icon is 12px with no padding. `.hr-filter-tag-remove::before` widens the clickable area to 32px, and the button now has a visible keyboard focus ring.

**Size:** Home 4,823 → 4,525 lines, and Formation 1,430 → 1,303 lines.

**Verification:**
- `dotnet build` passes with no new warnings.
- All 110 tests pass.
- `node --check` passes on the chart script.
- The detector finds 0 issues on Home and Formation. The 16 findings in `hr-ui.css` were already there, and none are in the new section.
- Not checked in a browser.

**Manual check (needs a login):**
- **Home:**
  - The chart section should show the heatmap and the treemap at full width.
  - The filter-tag x buttons should be easy to hit and should show a focus ring.
- **Formation:**
  - The page goes from the 6 gauges straight to the tabs.
  - The gauges still draw and still follow the theme.
  - The tenure tiles show a coloured dot.
  - On a phone, the grids stack to one column with no sideways scrolling.
- **`/portal-legacy`** should now return the Not Found page.

---

## Separate task: Kurdish coverage on the dashboards

Not done in this pass. Most of the text on Home and Formation is Arabic-only: about 300 strings, with 22 Kurdish-aware strings on Home and 1 on Formation. The work needs Sorani (ckb) translations, ideally reviewed by a native speaker, so it's tracked as its own task. Strings should move into `LanguageService` keys (`Lang.T`), not more inline `IsKurdish` ternaries.
