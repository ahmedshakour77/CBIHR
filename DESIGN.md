# Design System — API.HR.Node

<!-- impeccable:design-tokens 1 -->

An enterprise Arabic RTL design system for a Blazor Server back office. Token-driven,
theme-aware, brand-switchable, with no external CSS framework at runtime.

---

## 1. Where the system lives

| File | Role | Edit it? |
| :--- | :--- | :--- |
| `design/tokens.json` | **The only hand-written source of token values.** Colour, type, spacing, radius, shadow, control, layout, z-index, motion. | Yes — this is the source of truth |
| `design/build-tokens.cjs` | Generates the CSS from the JSON. `node design/build-tokens.cjs`, or `--check` in CI. | Only to change how generation works |
| `design/check-tokens.cjs` | Resolves the token graph for every brand × theme and asserts the contrast pairs below. Exits non-zero on a regression. | Only to add a check |
| `design/check-classes.cjs` | Finds `hr-*` class names used in markup that no stylesheet defines. | Only to add a check |
| `design/check-vars.cjs` | Finds `var(--x)` reads with no fallback whose property is never declared — the browser drops those declarations silently. Exits non-zero. | Only to add a check |
| `wwwroot/css/hr-tokens.css` | Generated. Every token, both themes, all eleven palettes. | **No — regenerated on every build of the JSON** |
| `wwwroot/css/hr-components.css` | The personalization layer (`[data-brand]`, `[data-font]`, `[data-font-size]`, `[data-density]`) plus the newer `hr-*__*` BEM components. | Yes |
| `wwwroot/hr-ui.css` | The component layer the application is built from: ~600 `hr-*` classes. | Yes |
| `wwwroot/app.css` | Blazor framework defaults, form-validation styling, nav-mode layout. | Yes |
| `wwwroot/js/hr-prefs.js` | Applies every preference to `<html>` before first paint; persists to `localStorage('hr-prefs')` and mirrored cookies. | Yes |

**The link order in `App.razor` is load-bearing** and is commented there:

```
hr-tokens.css → app.css → hr-components.css → hr-ui.css → API.HR.Node.styles.css
```

Twenty base class names exist in both `hr-components.css` and `hr-ui.css`
(`hr-btn`, `hr-card`, `hr-input`, `hr-table`, `hr-badge`, `hr-avatar`, `hr-tabs`,
`hr-pager`, `hr-rail`, `hr-main`, `hr-scrim`, `hr-check`, `hr-crumbs`, `hr-dot`,
`hr-num`, `hr-skip`, `hr-sr`, `hr-swatch`, `hr-tab`, `hr-timeline`). The application
uses the `hr-ui.css` variants — 392 `.hr-card`, 144 `.hr-btn-sm`, 116 `.hr-input` —
so `hr-ui.css` loads last and wins those collisions. Reversing the order silently
resizes every button and card in the product. When a component is migrated to the
BEM form, delete its `hr-ui.css` rule rather than relying on order.

`wwwroot/js/theme.js` and `wwwroot/js/preferences.js` are superseded by `hr-prefs.js`
and are not loaded. `hr-prefs.js` re-exports `window.hrTheme` and `window.hrPreferences`
for callers such as `CommandPalette.razor`.

---

## 2. Brand anchors

| Anchor | Token | Light | Role |
| :--- | :--- | :--- | :--- |
| **Ink** | `--b-800` | `#3B1E54` | Primary accent, headings, solid fills |
| **Mid** | `--b-400` | `#7C609D` | Control edges, interactive borders, icons |
| **Soft** | `--b-200` | `#D4BEE4` | Rails, chips, hover tints, the focus halo |
| **Ground** | `--ground` | `#EEEEEE` | App canvas, inset wells. Neutral in every brand |

The ramp runs `--b-25` … `--b-1000` along one hue line. `--b-500` is the Oklab
midpoint of Mid and `--b-600`, so no two steps collapse into each other.

**Mid is `#7C609D`, not `#9B7EBD`.** `--line-control` is the edge of every input,
secondary button and chip, and much of the product sits on the `#EEEEEE` ground
rather than on white. `#9B7EBD` measured 2.96:1 there — under WCAG 1.4.11's 3:1
floor for non-text contrast. `#7C609D` measures 4.51:1 on ground and 5.23:1 on white.

---

## 3. Colour architecture

Rules never name a ramp step directly; they name a semantic token, and the semantic
layer is the only thing that flips between themes:

```
--surface --surface-raised --surface-sunken --surface-inset --surface-tint --ground
--line --line-soft --line-strong --line-control
--text --text-2 --text-3 --text-muted --text-on-accent
--accent --accent-hover --accent-active --accent-quiet --accent-tint --accent-tint-2
--accent-ink --accent-text --focus --ring
--ok --warn --danger  (+ -tint and -line for each)
```

Every one of these is expressed against the ramp — `var(--b-800)`, or a `color-mix()`
of two steps — so a brand change reaches surfaces, lines and washes, not just the
accent. Dark mode holds no literal purple.

### Contrast, verified not asserted

`node design/check-tokens.cjs` resolves the graph for **11 brands × 2 themes** and
asserts 572 pairs. Current state: all pass.

| Pair | Floor | Worst case across all brands |
| :--- | :--- | :--- |
| Body text on ground and surface | 4.5:1 | 14.22:1 |
| Secondary text | 4.5:1 | 12.02:1 |
| Muted text and `--accent-text` | 4.5:1 | 6.22:1 |
| Text on a solid accent fill | 4.5:1 | 10.76:1 |
| `--line-control` on ground, surface and raised surface | 3:1 (SC 1.4.11) | 4.48:1 light, 6.69:1 dark |
| Focus ring on ground and surface | 3:1 | 6.22:1 |
| `--ok` / `--warn` / `--danger` on ground and surface | 4.5:1 | 5.10:1 |

The check also asserts every ramp is monotonic in luminance and that no two adjacent
steps are within 8/441 RGB distance.

### Status colour

`--ok #1D6B53`, `--warn #8A5A11`, `--danger #A8203C` (dark: `#5FCFA6`, `#E0A94A`,
`#F98BA0`). They do not follow `[data-brand]` and are always paired with an icon and
a word, so meaning never rides on colour alone.

`--cbi-copper` is the one accent outside the brand ramp and the status hues: the CBI
identity copper, used for data-import highlights and the unified employee number.
`copper-600` in light (7.3:1 on white), `copper-300` in dark.

### Data series

`--series-1` … `--series-6` (violet, sky, emerald, amber, red, slate) are the categorical
palette for charts, stacked bars and their legend swatches — never a status, which
stays on `--ok`/`--warn`/`--danger`. Each is paired light/dark, and `--series-on` is the
text drawn on a series fill; `check-tokens` asserts it at 4.5:1 on all six (worst
5.02:1 light, 10.28:1 dark). A chart that needs a sequential ramp (a heatmap) uses the
brand ramp `--b-100` … `--b-900` instead, so it follows the brand.

---

## 4. Theming and personalization

Everything is an attribute on `<html>`, written by `hr-prefs.js` before first paint
and server-rendered in `App.razor` from the mirrored cookies so there is no flash.

| Attribute | Values | Effect |
| :--- | :--- | :--- |
| `data-theme` | `light` `dark` | Flips the semantic layer. **Omitted** when the preference is "system" — `@media (prefers-color-scheme: dark)` in `hr-tokens.css` then paints the right theme on the first frame |
| `data-brand` | 11 palettes: purple (default, attribute omitted), indigo, blue, teal, green, olive, gold, copper, crimson, rose, slate | Repoints `--b-25…--b-1000` at that palette. Everything downstream follows |
| `data-font` | `plex` `cairo` `tajawal` `noto` `alexandria` | Swaps `--font-ui`. `hr-prefs.js` fetches only the chosen family |
| `data-font-size` | `sm` `md` `lg` `xl` | Root `font-size` 87.5 / 100 / 112.5 / 125%. Spacing, control heights and layout are all rem, so the whole interface scales |
| `data-density` | `compact` `comfortable` `spacious` | Control height and the spacing scale. `compact` reverts to the comfortable control height under `(pointer: coarse)` |
| `data-nav-mode` | `side` `top` `floating` | Shell layout, in `app.css` |

`data-contrast="high"` is wired in CSS but nothing currently sets it.

---

## 5. Typography

- **Arabic UI:** IBM Plex Sans Arabic (400–700), with Cairo, Tajawal, Noto Sans
  Arabic and Alexandria selectable. One family is fetched per session.
- **Figures:** Inter with `font-variant-numeric: tabular-nums` for IDs, dates and
  counts, so columns of numbers do not shift width. Inter is a deliberate,
  documented brand commitment for this role.
- **Direction:** native `dir="rtl"`. Logical properties throughout — `padding-inline`,
  `border-inline-start`, `inset-inline-end`. No mirrored physical values.
- Type roles ship as classes generated from the JSON: `.display .h1 .h2 .h3 .body
  .label .caption .num .num-lg`.
- Where a role class cannot go on the element, the size comes from the scale tokens:
  `--fs-caption` .75 · `--fs-label` .8125 · `--fs-control` .875 · `--fs-body` .9375 ·
  `--fs-lead` 1 · `--fs-h3` 1.0625 · `--fs-title` 1.125 · `--fs-h2` 1.25 · `--fs-h1` 1.5 ·
  `--fs-num-lg` 1.75 · `--fs-display` 2 (rem). Never write a literal `font-size`.
  `--fs-caption` (12px at the default text size) is the floor: nothing is set smaller,
  avatar initials and table headers included.
- Form fields carry an absolute `16px` floor under `(pointer: coarse)`. iOS zooms the
  viewport when it focuses a field under 16px and never zooms back; `1rem` would
  shrink with the text-size preference and reintroduce the jump.

---

## 6. Spacing, geometry, elevation

**Spacing** — 4pt rhythm in rem: `--s-1` 0.25 … `--s-16` 4rem.

**Radius** — grows with the surface:

| Token | Size | Applied to |
| :--- | :--- | :--- |
| `--r-xs` | 6px | Tags |
| `--r-sm` | 8px | Badges |
| `--r` | 10px | Buttons, inputs, controls |
| `--r-md` | 12px | Panels |
| `--r-lg` | 14px | Cards, drawers |
| `--r-xl` | 18px | Modals, callouts |
| `--r-full` | 999px | Chips, avatars, meters |

**Elevation** — `--shadow-xs` through `--shadow-pop`. Every shadow carries an offset
and a soft blur, tinted with `rgba(28,12,43,…)` rather than neutral black, so a card
never reads grey against the lavender ground.

**Layers** — one scale, never invent a z-index: `--z-sticky` 10, `--z-popover` 30,
`--z-nav`/`--z-rail` 40, `--z-header` 50, `--z-scrim` 60, `--z-drawer` 70, `--z-modal` 80,
`--z-modal-stacked` 82, `--z-palette` 85, `--z-toast` 90. A layer one above its own
backdrop is written `calc(var(--z-…) + 1)`, never a new number. Every overlay in the
product — dock, notification panel, drawers, dialogs, command palette, toasts — sits on
this scale; the floating dock used to sit at 900 and covered every modal's buttons.

### Page width

`--content-max` is **`none`**: a page grows to the full width of the shell. Four
containers resolve through it and must never hardcode a width of their own —
`.hr-main` (hr-components.css), `.hr-page` (hr-ui.css), and `.hr-portal-container`
and `.hr-portal-header-inner` (app.css). The portal used to cap at 1200px and the
back office at 1440px; they are now the same width as each other by construction.
To reintroduce a cap, set that one token to a length — nothing else changes.

Width is never the thing that keeps text readable. `.hr-measure` (68ch) and
`.hr-measure-sm` (52ch) do that, and every page subtitle already carries one.
Full-bleed is for tables, grids and dashboards; prose keeps its measure.

Two deliberate exceptions stay capped because they are compositions rather than
pages: the workforce-statistics deck (`82rem`) and the pyramid canvas (`66rem`),
both read at 2–4 m on a wall display where stretching to 4K would break the layout.

### Card grids

`.hr-grid-cards` auto-fills at `minmax(18rem, 1fr)` — the default, and right for
most grids. `.hr-grid-cards-quad`, added alongside it inside a `.hr-cardsview`,
states an exact rhythm instead: **1 card on a phone, 2 on a tablet, 4 on a desktop**.

It keys off a **container query, not the viewport**, and that is load-bearing.
The side rail takes `--nav-w` (16.5rem) plus its gutter out of the content column
from 1024px up, so a viewport rule would add columns at exactly the width where
the space available to the grid drops by a third. Measured against the container,
the same rule works in all three nav modes: with the rail the fourth column
arrives at a 1440px viewport, without it at 1280px.

Each step caps the grid at a whole number of `--card-w` (26rem) and centres the
remainder, so a full-bleed page never stretches a card past the width it was
drawn at — on a 2560px monitor the row is four 416px cards, centred, not four
620px ones. Any fixed-column card grid should follow the same shape.

---

## 7. Motion

Distance-matched, never one duration reused everywhere:

| Token | Value | For |
| :--- | :--- | :--- |
| `--t-tap` | 90ms | Pressed and active states |
| `--t-state` | 140ms | Hover, focus ring, colour |
| `--t-move` | 220ms | Tab shifts, list reordering |
| `--t-enter` | 260ms | Drawer and modal entrance |
| `--t-exit` | 170ms | Exit — 65% of entrance, so dismissal feels immediate |

`--ease` `cubic-bezier(.2,.7,.25,1)` for state; `--ease-out` `cubic-bezier(.16,1,.3,1)`
for arrival; `--ease-in` `cubic-bezier(.5,0,.9,.3)` for departure.
**Reduced motion removes travel, not change.** The OS setting and the in-app preference
(`[data-reduced-motion]` on `<html>`) share one set of rules in `hr-ui.css` §2:
- Keyframes stop.
- Transitions keep only colour, opacity and shadow, so hover tints still ease while
  anything moving on `transform` or `width` jumps into place.
- Overlays (drawers, modals, toasts, popovers, the command palette, scrims, the dock
  sheet) fade in over `--t-state` instead of sliding, so opening one is still seen.
- `.hr-spinner` keeps turning at half speed: a frozen spinner reads as a stall.

A new overlay class joins the fade list there.

**The one authored moment:** a record's photo *develops*. When the original has decoded
behind its skeleton, `.hr-preview-media img` comes into focus from blurred,
desaturated and faint, over 420ms on `--ease-out`. This is the directory record and
the gallery preview. Opening a record to see a person is the moment the roster earns;
nothing else in the app gets an entrance like it. Reduced motion keeps the fade and
drops the focus pull.

Press feedback is colour and shadow, never a transform that moves neighbouring content.

**Known exception:** progress meters (`.hr-meter-fill`, `.hr-split-a/b`,
`.hr-deck-meter-fill`, `.hr-horiz-fill`, and the `.hr-stack-segment` cohort bar) transition `width`, which lays out. They animate once when a
value changes and the split bars are layout siblings that `scaleX` cannot express.
Accepted deliberately; do not copy the pattern into a list or a hot path. The column
bars of the dashboard charts (`.hr-col-bar`, `height`) fall under the same exception,
as do two of the home dashboard's marks: `.hr-viz-pyr-bar` (`inline-size`; its count rides the
bar's end) and `.hr-viz-col-bar` (`block-size`; its value sits on the column's top). Both move
only when the filter changes, and their plots carry `contain: layout` so the reflow stays inside.
The home dashboard's ranked bars and record meters do not need the exception: they fill their
track and are revealed to length by `clip-path` (`--v`, 0..1), which repaints without layout.
Copy that pattern for any new bar whose length carries no neighbour with it.

Home dashboard motion has one subject, the page re-counting after a filter change: bars glide,
changed figures settle (`.home-tick`, keyed by value so only a changed figure plays), a chip
added from a chart pops into the bar, and "All fields" drops in. Under reduced motion each is a
plain `hr-fade-in`, following the shared overlay rule in `hr-ui.css`.

---

## 8. Component inventory

`Components/DesignSystem/`:

| Component | Accessibility |
| :--- | :--- |
| `DsButton`, `DsIconButton` | Focus ring, disabled semantics. Every `.hr-btn-icon` — 40px, 32px `-sm` or 28px `-xs` drawn — has a 44px hit area from an invisible `::before`, in absolute px so it does not shrink with the text-size preference. Text buttons at `-sm` and `-xs` get the same ring on a coarse pointer only, so mouse clicks in dense rows never land on a neighbour. The focus ring never changes a control's radius |
| `DsDataGrid` | Sortable headers with `aria-sort` |
| `DsDrawer` | `role="dialog"`, `aria-modal`, focus trap, Escape |
| `DsAlert` | Status icon paired with the colour |
| `DsToast` | `aria-live="polite"`, does not steal focus |
| `DsStatusBadge` | Text plus colour, never colour alone |
| `DsTabs` | Roving tabindex |
| `DsEmptyState`, `DsSkeleton` | Empty and loading states |
| `CommandPalette` | `Ctrl+K`, full keyboard navigation, theme toggle |
| `.hr-thread` / `.hr-track` / `.hr-reply` (CSS, `/my-feedback`) | A submission as a thread: type plate, title, `<time>`-stamped meta; a three-step status track (`aria-current="step"`, done steps filled, final step `--ok`, closed step neutral); the staff reply on `--accent-tint` with `--accent-ink` (asserted at 4.5:1 in `check-tokens`). Long text clamps to three lines behind an `aria-expanded` toggle. Status counts live in the filter tabs (`hr-tab-badge`), never as standalone stat tiles |
| `.hr-gate-pill` (CSS, directory / profile / vehicles) | One pill per gate a vehicle may pass. Allowed on `--ok-tint`/`--ok`; blocked muted **and** struck through, so access never rides on colour alone |
| `Components/Stats/Viz*` (home dashboard) | Chart controls are buttons with `aria-pressed`; every value is printed beside its mark; the heatmap is a `<table>` with scoped headers. See *Dashboard charts* below |
| `FloatingDock` (Layout) | Floating nav mode. Four role-checked shortcuts from `NavCatalog` plus a "كل الأقسام" tab that opens a `role="dialog"` panel with every section the user may open; when the current page is not a shortcut, that tab shows it. Frosted surface (`backdrop-filter`, opaque fallback). Logout lives in the panel footer, not on the bar. Phones: full-width tab bar |

### Dashboard charts

`Components/Stats/` holds the home dashboard's charts, rendered on the server with no chart
library; their styles are the `.hr-viz-*` block in `hr-components.css`. Pick the form from the
shape of the data:

| Component | For |
| :--- | :--- |
| `VizBars` | A field with many values, ranked (formations, units, titles). Top 8, then "show all" |
| `VizDonut` | Two to six values (gender, marital status). The tail past five folds into "أخرى" |
| `VizWaffle` | A field dominated by one or two values, read as "how many in a hundred" (religion) |
| `VizStack` | Parts of a whole in a fixed order (generations) |
| `VizColumns` | An ordered scale (service bands, hire years, leave balances) |
| `VizPyramid` | Age bands by gender, one shared scale for both sides |
| `VizHeatmap` | Grade × stage density; every cell prints its count |
| `VizTreemap` | Headcount by formation, squarified in C#; becomes `VizBars` in a panel under 30rem |
| `VizForecast` | Ten-year retirement columns, supervisory share drawn inside each |
| `VizMeters` | Record completeness; the gap opens the list of who is missing it |

- Every value is printed; nothing rides on colour alone.
- Categories take `--series-*`, and a field keeps its colours across charts (`SeriesOf`: gender
  is sky for men and violet for women in the donut and the pyramid alike).
- Density and size are mixes of `--accent` into `--surface`, so the ramps read correctly in
  both themes. Never use raw `--b-*` steps as a fill: they are fixed, so in dark mode they
  glare or invert (the "dimmed" step came out brighter than the normal one). Bars and columns
  take three roles defined at the top of the `.hr-viz-*` block: `--viz-fill` (55% accent;
  3.5:1 on the light surface, 3.9:1 on the dark), `--viz-fill-strong` (the accent: peak,
  selection, the forecast's supervisory share) and `--viz-fill-quiet` (22%: unpicked values
  once something is picked).
- A selected value shows by fill, weight and `aria-pressed` together.
- A chart that picks values for a field draws `stats.Context[field]`: every other filter
  applied, its own not. Picking a bar keeps its siblings on screen, dimmed, and a picked
  value is never folded into "أخرى" or cut below "show all" (`Viz.Fold`).
- Every repeated chart element carries `@key` by value, so a click re-renders around the
  pressed control and keyboard focus stays on it.
- Chart text is written in Arabic and Sorani (`Lang.IsKurdish`, injected for the whole folder
  by `Components/Stats/_Imports.razor`). Data values show as stored.
- Number ranges are written "25 إلى 29", never "25 – 29": the bidi algorithm reverses a
  dash-separated range in RTL.
- Charts sit in `.home-panel`, a size container, so each adapts to its panel, not the viewport.

The data comes from `Services/Dashboard`. `WorkforceSnapshotService` loads the non-archived
workforce once and shares it for two minutes; `WorkforceStats.Compute` filters and counts it in
memory, so the general filter and every chart click re-count without a query. Facet counts
ignore their own field, so a dropdown shows what picking a value would leave.
`HomeDashboardPreview` (tests) renders the page over synthetic data to
`HOME_PREVIEW_OUT` for a look without a database or a login.

`Components/Common/Modal.razor` is the dialog. `wwwroot/js/hr-dialogs.js` gives every
`[role="dialog"][aria-modal="true"]` in the app — this one, `ConfirmDialog`, drawers,
the palette, hand-built dialogs — a Tab trap and returns focus to the opener on close.
No component has to opt in; a new dialog only needs those two attributes. Text fields, selects and textareas are
plain elements carrying `.hr-input` / `.hr-select` / `.hr-textarea`; there is no
`DsInput` or `DsSelect` component.

---

## 9. Working on this system

1. Change a value in `design/tokens.json`.
2. `node design/build-tokens.cjs`
3. `node design/check-tokens.cjs` — must exit 0.
4. Never declare a token in `hr-ui.css` or `hr-components.css`, and never write a raw
   hex in a component. A hardcoded colour will not follow the theme or the brand.
5. Adding a component: put it in `hr-components.css` in BEM form. If it replaces an
   `hr-ui.css` class, delete that class in the same change.

### Invented class names

`node design/check-classes.cjs` lists every `hr-*` class in markup that no stylesheet
defines. **Nothing else reports these.** The build passes, the console is silent, and
the element renders as an unstyled shell — a `hr-table-wrap` that does not scroll, a
`hr-badge-success` with no colour, a `hr-table-num` whose dates are not tabular. Run
it before shipping a page. A class defined in a page's own `<style>` block counts
as defined for that page only; a class that exists purely as a script selector goes
in the checker's `SCRIPT_HOOKS` list with the code that queries it. Variant classes
built in C# (`DsAlert`, `DsStatusBadge`, `DsButton`) are invisible to the checker —
keep them on the vocabulary below.

The vocabulary is small and consistent; check it before inventing a name:

| Role | Correct | Do **not** write |
| :--- | :--- | :--- |
| Scrolling table wrapper | `hr-tablewrap` | `hr-table-wrap`, `hr-table-container` |
| Semantic variant suffix | `-ok` `-warn` `-danger` `-accent` | `-success` `-info` `-neutral` `-outline` |
| Tabular figures | `hr-num` | `hr-table-num` |
| Emphasised cell / ID | `hr-cell-strong` | `hr-id-badge` |
| Body link | `hr-link` | `hr-link-subtle`, `hr-link-text` |
| Row action group | `hr-rowactions` | inline `display:flex` |
| Search field with glyph | `hr-search` + `hr-search-icon` | `hr-field-input-wrap`, `hr-input-with-icon` |
| Filter toolbar | `hr-filterbar` > `hr-filterbar-row` > `hr-field hr-filter-grow` \| `hr-filter-item` \| `hr-filter-reset` | `hr-filterbar-grow` |
| Bordered button | `hr-btn-secondary` | `hr-btn-outline` |

A table with more columns than a laptop is wide gets `hr-table-wide` on the `<table>`
alongside `hr-tablewrap` on its container, so the scroll gives before the columns do.
