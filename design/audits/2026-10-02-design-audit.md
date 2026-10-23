# Comprehensive design audit — API.HR.Node

Date: 2026-10-02. Skill: impeccable, technical audit playbook.

Source-path convention: `Pages/`, `Layout/`, `Common/`, `DesignSystem/` and `Account/` below are relative to `API.HR.Node/Components/`; `wwwroot/` is relative to `API.HR.Node/`; standalone `css/` and `hr-ui.css` are relative to `API.HR.Node/wwwroot/`. Line numbers refer to the current source, before any proposed changes.

## Executive Summary & Compliance Rating

**Implementation integrity verdict: FAIL for consistent adherence; the underlying system is coherent and product-specific.** Arabic RTL, semantic themes, configurable brands and density, employee avatars, and the roster's container-based grid establish a clear system. Its integration is inconsistent: an earlier stylesheet still contributes incompatible properties, and newer feedback code bypasses established tokens and dialog components.

**Audit health: 12/20 — Acceptable, significant work needed.** This is an engineering audit score, not a percentage of WCAG compliance or a conformance certification.

| Dimension | Score | Main evidence |
|---|---:|---|
| Accessibility | 2/4 | Unnamed select/dialog; malformed listbox; incomplete composite keyboard behavior; dark admin-reply contrast |
| Performance | 3/4 | Lazy thumbnails and bounded assets; org-chart wheel events invoke server work per event; frame performance not measured |
| Responsive design | 2/4 | Live checkbox overlap; invalid feedback breakpoint; optional data-grid card fallback loses content |
| Theming | 3/4 | 572 token contrast checks pass; feedback bypasses semantic colors |
| Implementation integrity | 2/4 | Conflicting base classes, control sizes and active/motion rules; extensive static inline styling |
| **Total** | **12/20** | **Acceptable** |

**20 grouped findings: 0 P0, 8 P1, 11 P2, 1 P3.** P1 means major difficulty or accessibility failure; P2 means a defect with a workaround, a latent component contract defect, or a material risk needing runtime validation; P3 means documentation polish. No unconditional task-blocking P0 was established.

Priority: fix the checkbox class collision and malformed mobile rule, then feedback contrast/semantics and hierarchy keyboard behavior. Consolidate component states and token usage afterward.

### Scope and verification

- Reviewed `PRODUCT.md`, `DESIGN.md`, the token JSON/generator/checks, active application CSS, shared components, layouts, preference/dialog/chart JavaScript, and page markup. Static scanning covered **101 Razor files**.
- Inspected the running application at `http://127.0.0.1:5058`. It redirected to sign-in. Inspected sign-in at 1920×945, 390×844 light, and 1440×1000 dark; measured DOM geometry and computed styles.
- Protected HR pages and the component playground were not available in this browser session. Findings below explicitly distinguish live evidence, source-confirmed defects, conditional behavior, and unmeasured risks. No authenticated screenshots, real-device touch tests, screen-reader session, full performance trace, or CLS measurement was performed.
- Mobile-width inspection used a desktop pointer (`pointer: coarse` remained false). Its 15px input measurements do **not** establish an iOS zoom defect; the shared `.hr-input` has a coarse-pointer 16px floor.
- Application code was not changed. The report and a reproducible source scan are the only audit deliverables. Proposed snippets below are recommendations, not applied or build-tested changes.

### Checks run

| Check | Result |
|---|---|
| `node design/build-tokens.cjs --check` | Generated CSS current |
| `node design/check-tokens.cjs` | **572 pairs, 11 brands × 2 themes; all pass** |
| `node design/check-classes.cjs` | **743 distinct used classes; 989 defined; none undefined** |
| Impeccable detector on Components + active component CSS | 17 warnings: 9 Inter uses, 8 width/height transitions |
| `node design/audits/2026-10-02-source-scan.cjs` | 1,546 inline style attributes across 73/101 Razor files; 162 direct `--b-*` references in non-generated CSS; no unresolved no-fallback `var()` references in the scanned active styles/markup |

Counts are inventory signals, not 1,546 proven defects. Inline data-driven widths, chart transforms, QR sizing, logos, and print-specific ink can be intentional. The no-fallback scan checks token existence, not meaning, cascade precedence, or arbitrary inline custom-property generation.

The detector's Inter warnings are **accepted exceptions**, because Inter is an explicit numeric typography commitment. The meter, split-bar, deck-meter and column-chart layout transitions are documented exceptions in `DESIGN.md`; none is counted as a defect without evidence of excessive updates. A detector warning or nonzero exit is not a confirmed failure by itself.

## Critical Errors & Token Violations

### 01 — P1: Checkbox-label width collision [live confirmed]

**Locations:** `API.HR.Node/wwwroot/css/hr-components.css:170`; `API.HR.Node/wwwroot/hr-ui.css:935`; `API.HR.Node/Components/Account/Pages/Login.razor:239`. Other affected label uses: `API.HR.Node/Components/Account/Pages/LoginWith2fa.razor:54`, `API.HR.Node/Components/Pages/DataImport.razor:122`, `:322`, `:518`.

**Pattern:** the earlier sheet sets `.hr-check { inline-size:18px; block-size:18px; }` for an input. The later sheet uses the same class for a checkbox **label**, without clearing those dimensions. Loading it last does not remove unmatched earlier declarations.

**Impact:** live label width was 18px; its text span was approximately 32.4px wide and 102px high on mobile, overflowing the 44px label box and entering the submit area. The same wrapping occurred on desktop.

**Fix:** scope the bare-checkbox rule to `input.hr-check`, and ensure label containers size naturally. Verify all five label uses. Category: structural/responsive/accessibility. Relevant standard: WCAG 1.4.10 reflow, subject to full zoom testing. Command: `$impeccable harden`.

### 02 — P1: Feedback mobile breakpoint is invalid CSS [source confirmed]

**Location:** `API.HR.Node/wwwroot/hr-ui.css:6440`.

**Pattern:** `@@media (max-width: 640px)` escaped as though it were inside Razor, despite being in a plain `.css` file.

**Impact:** browsers ignore this rule. The chip grid retains four columns, and the feedback footer does not switch to its intended mobile stack. Narrow modal space and large text make collision/overflow more likely; that final geometry needs an authenticated check.

**Fix:** use `@media`. Category: responsive/implementation integrity. Command: `$impeccable adapt`.

### 03 — P1: Admin replies fail normal-text contrast in dark mode [calculated]

**Location:** `API.HR.Node/Components/Pages/EmployeeProfile.razor:1495`.

**Pattern:** `style="color:#126957;"` on administrative reply text.

**Impact:** against default purple dark `--surface` (80% `#2a143e` + 20% `#1c0c2b`), the foreground measures **2.57:1**, below the **4.5:1** normal-text requirement. The inline literal survives theme and brand changes.

**Fix:** use `var(--ok)` if this is intentionally a success tone; use `var(--text)` if the content is ordinary correspondence. Extend contrast checks to actual component pairings. Category: accessibility/theming; WCAG 1.4.3. Command: `$impeccable colorize`.

### 04 — P1: Feedback dialogs do not fully implement their modal contract [source confirmed]

**Locations:** `API.HR.Node/Components/Pages/EmployeeFeedbacksPage.razor:397`, `:404`; `API.HR.Node/Components/Layout/EmployeeFeedbackFab.razor:36`, `:435`; shared reference: `API.HR.Node/Components/Common/Modal.razor:18`, `API.HR.Node/wwwroot/js/hr-dialogs.js:27`.

**Pattern:** the administration dialog has `role="dialog" aria-modal="true"` but no accessible name. Both custom feedback dialogs lack their own initial-focus and Escape handlers. The FAB's `OnAfterRenderAsync` only registers a global opener on first render.

**Impact:** assistive technology cannot identify the admin dialog; opening leaves focus outside the panel until another key action. The global dialog script does trap Tab and attempt focus restoration, so **missing Tab trapping is not a finding**. It does not provide initial focus or Escape dismissal for these components.

**Fix:** reuse `Modal`, preserving current content and business behavior, or add `aria-labelledby`, an element reference, open-transition focus, and Escape handling. Category: accessibility; WCAG 4.1.2 for the unnamed dialog, APG dialog behavior for focus/Escape. Command: `$impeccable harden`.

### 05 — P1: Feedback form controls lack names and field relationships [source confirmed]

**Locations:** `API.HR.Node/Components/Pages/EmployeeFeedbacksPage.razor:98`, `:146`; `API.HR.Node/Components/Layout/EmployeeFeedbackFab.razor:135`, `:157`, `:192`, `:226`.

**Pattern:** the status `<select>` has no label/ARIA name. The search relies on its placeholder. The attachment label has no `for`, and `InputFile` has no ID/name relationship. Required title/details markers are hidden from assistive technology, with no `required`/`aria-required`. Word-count help and file errors are not linked to their fields.

**Impact:** users cannot reliably identify the status or attachment input, and validation constraints are separated from the control they describe. The search placeholder may be a fallback name; it is not counted as definitively nameless.

**Fix:** add persistent or screen-reader labels; link helper/error IDs using `aria-describedby`; expose required and invalid states. Category: accessibility; WCAG 1.3.1, 3.3.2, 4.1.2 as applicable. Command: `$impeccable harden`.

### 06 — P1: Multi-select exposes a listbox containing checkboxes instead of options [source confirmed]

**Locations:** `API.HR.Node/Components/Common/MultiSelectDropdown.razor:18`, `:60`, `:114`.

**Pattern:** `role="listbox" aria-multiselectable="true"` owns labels and native checkboxes, with no `role="option"`/`aria-selected` children. No listbox selection/navigation model is implemented. Escape is handled only inside the panel; opening does not focus it and closing does not restore trigger focus.

**Impact:** screen readers receive a selection widget whose children do not match its advertised role. Keyboard users encounter ordinary checkbox behavior while assistive technology expects a listbox.

**Fix:** preserve native checkboxes, replace the listbox with a named group, and remove the listbox popup claim. Link the trigger to the panel. Manage focus on open and Escape close. Category: accessibility; WCAG 4.1.2/ARIA role ownership. Command: `$impeccable harden`.

### 07 — P1: Hierarchy card nests another control inside a button role [source confirmed]

**Locations:** `API.HR.Node/Components/Pages/JobHierarchyPage.razor:617`, `:622`, `:623`, `:694`.

**Pattern:** a whole card is `role="button" tabindex="0"`, with an inner collapse `<button>`. The card handles Enter only. Its inner button stops click propagation, but does not stop keydown propagation.

**Impact:** nested control semantics can be flattened or confusing to assistive technology. Space does not activate the card; Enter from the collapse button bubbles to the card handler and can also open the detail drawer.

**Fix:** make the card a noninteractive container; use a dedicated native detail button beside the collapse button. Do not replace it with a native `<button>` wrapping the existing nested control. Category: accessibility; WCAG 2.1.1/4.1.2 and native button expectations. Command: `$impeccable harden`.

### 08 — P1: Feedback chip and upload edges use a decorative border token [calculated/source confirmed]

**Locations:** `API.HR.Node/wwwroot/hr-ui.css:6306` (chip), `:6354` (dropzone); compare `design/tokens.json` definitions of `line` and `line-control`.

**Pattern:** interactive boundaries use `--line`, documented as decorative-only. Light default line `#e3d8ec` measures **1.37:1 on white** and **1.13:1 on the inset fill**. The low-contrast chip fill also does not provide a strong substitute edge.

**Impact:** the boundary that identifies the clickable area is difficult to see. Text contrast passing does not validate a control's boundary. Disabled controls and purely decorative separators are separate cases.

**Fix:** use `--line-control` for interactive edges and semantic accent tokens for selected state. Category: accessibility/theming; WCAG 1.4.11 where the edge is necessary to identify the control. Command: `$impeccable colorize`.

## Accessibility & Polishing Notes

### 09 — P2: Tabs and radio chips omit composite keyboard behavior

**Locations:** `API.HR.Node/Components/DesignSystem/DsTabs.razor:4`, `:8`, `:12`; `API.HR.Node/Components/Layout/EmployeeFeedbackFab.razor:117`, `:121`. Source confirmed.

Tabs have no roving tabindex, arrow/Home/End handler, `aria-controls`, or instance-specific ID prefix; two instances sharing keys would duplicate IDs. Feedback radios are buttons with radio roles, without native or implemented arrow navigation. All remain reachable with Tab and clickable, so missing APG conventions alone is not described as a demonstrated keyboard task blocker.

Use native radio inputs for chips. For tabs, implement instance IDs, one tab stop, direction-aware navigation, focused-tab refs, and matching labelled panels at the caller. Category: accessibility/consistency. Command: `$impeccable harden`.

### 10 — P2: Selection and theme states are incompletely announced

**Locations:** `API.HR.Node/Components/DesignSystem/DsDataGrid.razor:53`, `:60`; `API.HR.Node/Components/Pages/EmployeeFeedbacksPage.razor:108`; `API.HR.Node/Components/Account/Pages/Login.razor:108`; `API.HR.Node/wwwroot/js/hr-prefs.js:155`.

Density and feedback type filters communicate selection through class changes but omit `aria-pressed`. The live theme toggle retained the Arabic label “switch to dark mode” after switching to dark, even though its next action switched to light.

Add pressed state to filter/density controls and use a neutral theme-action label or synchronize it to the resolved theme. Category: accessibility/states. Command: `$impeccable clarify`.

### 11 — P2: Optional grid card template can make all rows disappear on mobile [conditional contract defect]

**Locations:** `API.HR.Node/Components/DesignSystem/DsDataGrid.razor:112`, `:133`, `:182`; `API.HR.Node/wwwroot/hr-ui.css:5061`.

Below 768px CSS unconditionally hides `.hr-table-scroll`, while the component renders cards only if `CardTemplate != null`. A nonempty grid without that optional template therefore has no visible rows. The sole current caller, `ComponentPlayground.razor:245`, supplies it, so this is a latent shared-component defect, not a demonstrated production outage.

Add a `has-card-template` modifier and condition the table-hiding rule on it; keep a scrollable table fallback otherwise. Category: responsive/implementation integrity. Command: `$impeccable adapt`.

### 12 — P2: Density tokens do not control incumbent button/input sizes

**Locations:** `API.HR.Node/wwwroot/css/hr-components.css:27`, `:47`; `API.HR.Node/wwwroot/hr-ui.css:672`, `:735`, `:746`, `:804`.

The early layer uses `--ctl` for control block size. The later layer sets fixed `min-height` values on the same physical axis, overriding it. A default button measured 40px at comfortable density despite a 44px comfortable token. Small text buttons are 32px; root “sm” reduces rem geometry further. Icon hit-area pseudo-elements mitigate their own targets, not ordinary small text buttons.

Use one block-size policy driven by control tokens, and a coarse-pointer floor that remains at least 44 CSS px at every text-size preference. Category: responsive/design-system adherence. Command: `$impeccable adapt`.

### 13 — P2: Reduced-motion rules freeze the spinner despite the documented exception

**Locations:** `API.HR.Node/wwwroot/css/hr-components.css:39`; `API.HR.Node/wwwroot/hr-ui.css:84`, `:145`, `:157`, `:767`.

The early `.hr-root * { animation:none !important }` removes the spinner's animation name. The later exception restores only duration and iteration count, so the spinner stays frozen. Two global near-zero-duration rules also obscure the intended policy.

Consolidate reduced-motion handling; remove ornamental animation and transitions intentionally, preserve static state cues, and restore the spinner's complete animation only if keeping that documented exception. Category: accessibility/state feedback. Command: `$impeccable animate`.

### 14 — P2: Tokens are bypassed by recurring geometry, type-role and palette choices

**Locations:** `API.HR.Node/wwwroot/hr-ui.css:41`, `:54`, `:165`, `:703`; `API.HR.Node/Components/Pages/DataImport.razor:1006`, `:1439`; `API.HR.Node/Components/Pages/OrganizationalHierarchyPage.razor:316`, `:326`; `API.HR.Node/Components/Pages/JobHierarchyPage.razor:634`; `API.HR.Node/wwwroot/css/hr-components.css:158`.

Examples: radius 14px/4px/0.75rem instead of radius tokens; `z-index:1` instead of `--z-sticky`; duration `0.25s` instead of motion tokens; density and radius modifier values written outside token JSON; 162 direct brand-ramp references in component CSS. `.hr-h2` uses `--fs-h3` and `.hr-h3` uses `--fs-body`, unlike generated `.h2`/`.h3` roles.

These choices prevent coherent adjustment and let semantically equivalent headings/components diverge. Move static presentation into reusable classes and canonical tokens; keep genuinely data-driven chart geometry inline. Review density variants as a deliberate schema extension. Category: implementation integrity/theming/type scale. Commands: `$impeccable typeset`, `$impeccable document`.

### 15 — P2: Feedback styling forms a second theme-independent palette

**Locations:** `API.HR.Node/wwwroot/hr-ui.css:6151`, `:6155`, `:6179`, `:6201`, `:6318`, `:6324`, `:6332`, `:6375`, `:6403`, `:6436`.

The FAB, chips, upload icon and success/referral styling hardcode green, teal and yellow plus separate shadows/radius/durations. They remain those colors across all 11 brand preferences. The custom dark variants reduce some contrast problems but do not restore token adherence.

Map neutral actions to accent tokens and success to `--ok`/`--ok-tint`. If a separate feedback identity is intentional, define semantic light/dark tokens in JSON and include their actual contrast pairs in the checker. Category: theming/polish. Command: `$impeccable colorize`.

### 16 — P2: Earlier active transform survives the later no-movement button policy

**Locations:** `API.HR.Node/wwwroot/css/hr-components.css:48`; `API.HR.Node/wwwroot/hr-ui.css:690`.

`transform:translateY(1px)` is never reset by the later active rule; setting `box-shadow` does not remove it. Thus buttons still move on press despite the documented policy. This transform is visual movement, **not** a layout reflow or measured CLS failure. Hover selectors also exclude `:disabled` but not `[aria-disabled="true"]`; links using that attribute require explicit activation suppression.

Consolidate state definitions, explicitly reset inherited transforms where intended, and keep disabled hover behavior consistent. Category: states/implementation integrity. Command: `$impeccable polish`.

### 17 — P2: Custom icon controls bypass the shared hit-area treatment

**Locations:** `API.HR.Node/wwwroot/hr-ui.css:5456`, `:4959`, `:4982`, `:4424`, `:4664`; `API.HR.Node/Components/Common/PasswordField.razor` for a separate reveal implementation.

The sign-in reveal measured 36×36px and does not carry `.hr-btn-icon`. Density, grid-clear, notification and toast buttons similarly use their own smaller geometry. The shared icon-control 44px pseudo-element does not apply to these selectors. Smaller text preferences can further reduce rem-sized targets.

Reuse `DsIconButton` where practical, or set actual minimum hit dimensions with spacing that avoids overlapping adjacent controls. Consider focus clipping inside `.hr-density-selector { overflow:hidden }` in runtime testing.

**44×44 is the project's touch target policy and the WCAG enhanced criterion; it is not the universal WCAG 2.2 AA minimum.** AA uses 24×24 with spacing/other exceptions. This finding is not a blanket AA failure for every control under 44px. Category: responsive/accessibility. Command: `$impeccable adapt`.

### 18 — P2: Feedback detail layout lacks constraints for long user content [risk, not live reproduced]

**Locations:** `API.HR.Node/Components/Pages/EmployeeFeedbacksPage.razor:449`, `:458`, `:468`; `API.HR.Node/wwwroot/hr-ui.css:4930`.

The title row and attachment row are unwrapped flex layouts; filename/title/description containers do not use the existing `.hr-wrap-any` helper. Long filenames, reference IDs or uninterrupted URLs can force a narrow modal wider. Grid search has a fixed 240px minimum and no narrow-container fallback.

Use `min-inline-size:0`, `overflow-wrap:anywhere`, wrapping flex rows, and `min(15rem,100%)`/container constraints for search. Validate with real long inputs at 320px and 200% zoom. Category: responsive/hardening. Command: `$impeccable harden`.

### 19 — P2: Org-chart wheel zoom sends a server invocation for every wheel event [source-confirmed traffic risk]

**Location:** `API.HR.Node/wwwroot/js/hr-orgchart.js:58`.

The non-passive wheel handler prevents default and immediately invokes `OnJsWheelZoom` for every event. On Blazor Server, a trackpad burst can create repeated network/server updates; no coalescing or outstanding-call guard exists. Actual frame loss and latency were not measured. Its fixed node widths and scrollable canvas are deliberate chart geometry, not automatically responsive defects.

Accumulate wheel intent, coalesce display updates, and keep at most one server invocation outstanding; preferably apply local transform feedback and reconcile server state at a bounded cadence. Preserve native scrolling/touch behavior when no zoom modifier is requested. Category: performance/responsive input. Command: `$impeccable optimize`.

### 20 — P3: Design-system examples advertise an obsolete anchor

**Locations:** `API.HR.Node/Components/Pages/ComponentPlayground.razor:416`; `API.HR.Node/wwwroot/hr-ui.css:8`; compare `design/tokens.json` b-400.

The playground and CSS header still label Mid as `#9B7EBD`; the canonical value is `#7C609D`. Developers copying the example can reintroduce the rejected low-contrast anchor. Treat displayed hex values as documentation, not a runtime hardcoded-color failure.

Update the examples from canonical JSON and reconcile radius-role descriptions in `DESIGN.md` with token usage notes. Category: documentation. Command: `$impeccable document`.

## Recommended Code Changes / Diffs

These are focused proposed changes. Preserve the existing factual copy, permissions, validation, data access and actions. Regenerate CSS only through `design/build-tokens.cjs` after JSON edits.

### A. Correct the two structural CSS defects

```diff
--- API.HR.Node/wwwroot/css/hr-components.css
+++ API.HR.Node/wwwroot/css/hr-components.css
@@
-.hr-check { inline-size: 18px; block-size: 18px; accent-color: var(--accent); cursor: pointer; }
+input.hr-check { inline-size: 18px; block-size: 18px; accent-color: var(--accent); cursor: pointer; }

--- API.HR.Node/wwwroot/hr-ui.css
+++ API.HR.Node/wwwroot/hr-ui.css
@@
-@@media (max-width: 640px) {
+@media (max-width: 640px) {
```

Avoid solving the checkbox issue solely by adding another global override; narrowing the incompatible earlier selector addresses its cause. If labels should wrap at larger text sizes, let them expand in block size naturally.

### B. Restore semantic colors and accessible control edges

```diff
--- API.HR.Node/Components/Pages/EmployeeProfile.razor
+++ API.HR.Node/Components/Pages/EmployeeProfile.razor
@@
-<span class="hr-cell-strong" style="color:#126957;">@fb.AdminNotes</span>
+<span class="hr-cell-strong" style="color:var(--ok);">@fb.AdminNotes</span>
```

Replace feedback-chip styling as a set (including the literal dark override), rather than appending rules that lose to it:

```css
.hr-feedback-chip {
    border: 1px solid var(--line-control);
    border-radius: var(--r-md);
    background: var(--surface-inset);
    color: var(--text-2);
    gap: var(--s-1);
    padding: var(--s-3) var(--s-2);
    transition: background-color var(--t-state) var(--ease),
                border-color var(--t-state) var(--ease),
                color var(--t-state) var(--ease);
}
.hr-feedback-chip:hover { border-color: var(--accent); background: var(--surface); }
.hr-feedback-chip.is-active {
    border-color: var(--accent);
    background: var(--accent-tint);
    color: var(--accent-ink);
    font-weight: 700;
    box-shadow: inset 0 0 0 1px var(--accent);
}
.hr-feedback-dropzone { border-color: var(--line-control); }
```

### C. Make grid fallback behavior conditional and expose density state

```diff
--- API.HR.Node/Components/DesignSystem/DsDataGrid.razor
+++ API.HR.Node/Components/DesignSystem/DsDataGrid.razor
@@
-<div class="@ComputedGridClass" @attributes="AdditionalAttributes">
+<div class="@ComputedGridClass @(CardTemplate is not null ? "has-card-template" : "")"
+     aria-busy="@Loading.ToString().ToLowerInvariant()" @attributes="AdditionalAttributes">
@@
 <button type="button"
         class="hr-density-btn @(Density == GridDensity.Compact ? "is-active" : "")"
+        aria-pressed="@((Density == GridDensity.Compact).ToString().ToLowerInvariant())"
         @onclick="() => SetDensity(GridDensity.Compact)"
```

Apply equivalent `aria-pressed` to Comfortable. A busy state should accompany a persistent loading/status message if users need loading announcements.

```diff
--- API.HR.Node/wwwroot/hr-ui.css
+++ API.HR.Node/wwwroot/hr-ui.css
@@
 @media (max-width: 768px) {
-    .hr-table-scroll {
+    .hr-datagrid-wrap.has-card-template .hr-table-scroll {
         display: none;
     }
-    .hr-table-cards {
+    .hr-datagrid-wrap.has-card-template .hr-table-cards {
         display: flex;
     }
 }
+ .hr-datagrid-wrap .hr-table-scroll {
+     max-inline-size: 100%;
+     overflow-x: auto;
+ }
```

### D. Correct feedback names and field relationships

```razor
<label class="hr-sr" for="feedback-search">البحث في مشاركات الموظفين</label>
<input id="feedback-search" type="search" class="hr-input"
       placeholder="بحث باسم الموظف، رقمه الوظيفي، عنوان المشاركة، أو الكود المرجعي..."
       @bind="searchTerm" @bind:event="oninput" @onkeyup="HandleSearchKeyUp" />

<label class="hr-sr" for="feedback-status-filter">حالة المشاركة</label>
<select id="feedback-status-filter" class="hr-input"
        value="@selectedStatusFilterString" @onchange="HandleStatusChanged">
    @* Keep the existing options. *@
</select>

@* Add to the existing description textarea; retain its binding and placeholder. *@
<textarea id="fb-desc" class="hr-input hr-feedback-textarea" rows="5"
          aria-required="true" aria-describedby="feedback-description-help"
          aria-invalid="@((CurrentWordCount > 0 && !IsWordCountValid).ToString().ToLowerInvariant())"
          @bind="descriptionInput" @bind:event="oninput"></textarea>
<div id="feedback-description-help" class="hr-feedback-helper">
    @* Keep the existing word-count helper branches. *@
</div>

<label class="hr-label" for="feedback-attachment">ملف مرفق داعم</label>
<InputFile id="feedback-attachment" OnChange="HandleFileSelected"
           accept=".pdf,.doc,.docx,.png,.jpg,.jpeg,.webp"
           aria-describedby="feedback-attachment-help feedback-attachment-error"
           class="hr-feedback-file-input" />
<p id="feedback-attachment-help" class="hr-meta">ملف واحد داعم · الحد الأقصى 2 MB</p>
<p id="feedback-attachment-error" class="hr-text-danger">@fileErrorMessage</p>
```

Also expose title required state, keep visible existing required text, and show focus around the transparent upload input's parent:

```css
.hr-feedback-dropzone:focus-within { outline: 2px solid var(--focus); outline-offset: 2px; }
```

### E. Reuse the existing modal implementation

For the admin detail view, replace its custom outer shell while retaining all existing body/footer content:

```razor
@if (activeFeedback is not null)
{
    <Modal IsOpen="true" Size="lg"
           Title="@($"تفاصيل المشاركة: {activeFeedback.ReferenceNumber}")"
           OnClose="CloseDetailModal">
        <Body>
            @* Existing submitter banner, detail fields, attachment, status and reply controls. *@
        </Body>
        <Footer>
            @* Existing Cancel and Save buttons, unchanged bindings. *@
        </Footer>
    </Modal>
}
```

Use the same lifecycle for the FAB's form/success views; do not introduce a second nested modal. Keep the `isSubmitting` guard on closure. For a minimal repair instead, add a heading ID/name, `tabindex="-1"`/`@ref`, focus on every closed→open transition, and an Escape handler.

### F. Use native radio semantics for feedback type

```razor
<div class="hr-feedback-chips" role="radiogroup" aria-label="نوع المشاركة">
    @foreach (var type in availableTypes)
    {
        var isSelected = selectedType == type.Key;
        <label class="hr-feedback-chip @(isSelected ? "is-active" : "")">
            <input type="radio" class="hr-sr" name="feedback-type" value="@type.Key"
                   checked="@isSelected" @onchange="() => selectedType = type.Key" />
            <Icon Name="@type.Icon" Size="xs" />
            <span>@type.Label</span>
        </label>
    }
</div>
```

```css
.hr-feedback-chip:has(input:focus-visible) {
    outline: 2px solid var(--focus);
    outline-offset: 2px;
}
```

Native grouped radios supply arrow/Space behavior. Keep a visible focus indicator even though the actual input is visually hidden.

For the multi-select, use the analogous native-checkbox group approach:

```diff
--- API.HR.Node/Components/Common/MultiSelectDropdown.razor
+++ API.HR.Node/Components/Common/MultiSelectDropdown.razor
@@
-            aria-haspopup="listbox">
+            aria-controls="@panelId">
@@
-        <div class="hr-popover hr-ms-panel" @onkeydown="OnPanelKeyDown">
+        <div id="@panelId" class="hr-popover hr-ms-panel" @onkeydown="OnPanelKeyDown">
@@
-            <div class="hr-ms-list hr-scroll" role="listbox" aria-multiselectable="true">
+            <div class="hr-ms-list hr-scroll" role="group" aria-label="@Title">
```

Add a stable `panelId` alongside `searchId`; retain the existing `aria-expanded`. Add element references and focus search on open / trigger on Escape close. Removing the false listbox role is one part of the repair, not a complete focus implementation.

### G. Consolidate primary-button states and density

Replace the existing light/dark ramp-specific primary rules with semantic rules:

```css
.hr-btn-primary { background: var(--accent); color: var(--on-accent); box-shadow: var(--shadow-xs); }
.hr-btn-primary:hover:not(:disabled):not([aria-disabled="true"]) { background: var(--accent-hover); }
.hr-btn-primary:active:not(:disabled):not([aria-disabled="true"]) { background: var(--accent-active); }
.hr-btn:active { transform: none; }
```

Replace physical `min-height` declarations with one token-driven block-size policy; apply consistently to size/icon variants and form fields. Keep dense desktop variants if required, but use an absolute touch floor:

```css
.hr-btn { min-block-size: var(--ctl); }
.hr-input, .hr-select, .hr-textarea { min-block-size: var(--ctl); }
@media (pointer: coarse) {
    .hr-btn, .hr-btn.hr-btn-sm, .hr-btn.hr-btn-xs,
    .hr-input, .hr-input.hr-input-sm, .hr-select, .hr-textarea {
        min-block-size: max(44px, var(--ctl));
    }
    .hr-auth-reveal, .hr-density-btn, .hr-table-search-clear,
    .hr-notif-action-btn, .hr-toast-close {
        min-inline-size: 44px;
        min-block-size: 44px;
    }
}
```

Remove or align later conflicting physical sizes. Increasing absolute reveal/clear buttons also requires matching input-end padding and an overlap check; these rules are a design-system starting point, not a complete control-layout patch.

### H. Repair motion and content overflow

Replace the duplicate reduced-motion blocks with a single coherent policy. If retaining the documented slow spinner, reset its entire animation after the global reset:

```css
@media (prefers-reduced-motion: reduce) {
    .hr-root *, .hr-root *::before, .hr-root *::after {
        animation: none !important;
        transition: none !important;
        scroll-behavior: auto !important;
    }
    .hr-root .hr-spinner {
        animation: hr-spin 1.4s linear infinite !important;
    }
}
```

Static final-state colors, labels and status text must remain visible. Apply an equivalent intentional policy if the separate app reduced-motion attribute becomes user-selectable.

Use existing `.hr-wrap-any` on text containers in feedback details and a named attachment-row class:

```css
.hr-feedback-attachment { display: flex; align-items: center; flex-wrap: wrap; gap: var(--s-3); }
.hr-feedback-attachment-info { flex: 1 1 12rem; min-inline-size: 0; overflow-wrap: anywhere; }
.hr-table-search { min-inline-size: min(15rem, 100%); max-inline-size: 22.5rem; }
```

For hierarchy cards, use a noninteractive wrapper with sibling native buttons for detail and collapse. A small handler change that adds Space to the outer card does not solve nested semantics or key bubbling.

## Positive Findings and Deliberate Exceptions

- Token source/generation is reproducible, current and contrast-tested across all 22 brand/theme combinations. Worst checked control-edge contrast is 4.48:1 light and 6.69:1 dark; checked muted text is 6.22:1 light and 7.35:1 dark.
- Root RTL and logical properties are established; the roster's fixed-count grid uses container queries to account for sidebar space.
- Main layout has a skip link; shared Modal/Drawer label and focus their panels; global dialog code traps Tab and restores focus.
- Shared buttons use native disabled semantics and busy state; icon buttons require accessible names. Focus-visible styles exist globally; this audit does not claim that focus rings are universally absent.
- Employee thumbnails use lazy loading/async decoding and fixed avatar frames. The auth hero has intrinsic dimensions. No image-induced CLS was demonstrated.
- Grid/search and chart data values may appropriately stay inline. Brand logos, printed black/white output, and a white QR backing are legitimate exceptions; the QR backing supports scannability.
- No runtime use of the obsolete header-dropdown danger class was found. Its hardcoded red hover is technical debt, not counted as a current user-visible contrast failure.

## Recommended Actions and Validation

1. **P1 — `$impeccable harden`:** checkbox collision, feedback dialog/forms, native selection semantics and hierarchy actions.
2. **P1/P2 — `$impeccable adapt`:** invalid breakpoint, grid fallback, density and hit targets.
3. **P1/P2 — `$impeccable colorize`:** admin reply contrast, feedback control edges and semantic feedback palette.
4. **P2 — `$impeccable animate`:** consolidate reduced-motion policy and spinner feedback.
5. **P2 — `$impeccable optimize`:** coalesce org-chart wheel traffic after measuring a trackpad interaction.
6. **P2/P3 — `$impeccable typeset` and `$impeccable document`:** align type roles and canonical examples, document any deliberate token exceptions.
7. **Final — `$impeccable polish`:** one bounded visual/state pass after functional repairs.

After implementing, rerun the token generation/contrast/class checks. Add focused regression coverage for CSS parsing, the checkbox selector collision, and a nonempty grid without a card template. Exercise authenticated pages at 320/390, 768/1024 and 1440px; test small/XL text, compact/spacious density, side/top/floating navigation, both themes, keyboard and a real coarse-pointer device. Check modal open/close/restore, radio arrows, tab arrows/Home/End, hierarchy Enter/Space, selected/disabled/hover/focus states, and long attachment names/URLs. Use a reduced-motion emulation and a screen-reader session; measure CLS/network/render behavior before making performance claims.

You can ask me to run these one at a time, all at once, or in any order you prefer. Re-run `$impeccable audit` after fixes to see your score improve.

### Standards referenced

- [WCAG text contrast minimum](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html): normal text requires 4.5:1, with large-text and other exceptions.
- [WAI-ARIA modal dialog pattern](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/): accessible name, focus movement, containment and Escape behavior.
- [WCAG 2.2 target size minimum](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html): AA 24×24 with exceptions; distinguish from the project's 44px policy.
- [WCAG non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html): required visual control information uses a 3:1 threshold.
- [WAI-ARIA tabs pattern](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/): tab stops, keyboard conventions and tab/panel relationships.
- [WAI-ARIA radio pattern](https://www.w3.org/WAI/ARIA/apg/patterns/radio/): selection and group keyboard conventions.
