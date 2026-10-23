A token-driven system for enterprise Arabic back-office software: right-to-left by default, purple by default, and personal to each employee, who picks their own brand color, font, mode, text size and density. Contrast clears WCAG AA in every combination and AAA for body text. The implementation lives in `wwwroot/hr-ui.css` and the Blazor components under `Components/DesignSystem/`; `components/bundle.css` here is the reference stylesheet they follow.

## Content fundamentals

- Write the interface in Arabic and lay it out right-to-left (`dir="rtl"` on the root). English appears only for codes, IDs, emails and technical values.
- Set every number, date, employee ID and amount in tabular figures (`hr-num`) so columns line up. Dates as `YYYY/MM/DD`; phone numbers in a `dir="ltr"` span.
- Buttons are verbs: "تقديم طلب", "تحديث بياناتي", "تصدير". Not nouns.
- Status is never carried by color alone. Every `ok`, `warn` or `danger` message pairs the color with a dot or icon and a word.
- Missing data is stated ("غير مُدخلة") with a way to fix it, never left blank or shown as a dash.

## Visual foundations

### Color: build with semantic tokens

UI code uses the semantic tokens below, never ramp steps or anchors directly. They're what make dark mode and the 11 brand palettes work without touching components.

| Token | Light | Dark | Use |
| :--- | :--- | :--- | :--- |
| `ground` | `#EEEEEE` | `b-950` | App backdrop behind cards |
| `surface` | `#FFFFFF` | `b-900` | Cards, panels, tables, inputs |
| `surface-2` | `b-50` | `b-950` | Table headers, wells, read-only fields |
| `text` | `b-900` | `b-50` | Body and headings (14.3:1 / 15.0:1) |
| `text-2` | `b-800` | `b-100` | Secondary values |
| `text-muted` | `b-600` | `b-300` | Labels, meta, placeholders (6.3:1 / 7.2:1) |
| `line` | `b-150` | `b-800` | Decorative dividers and card outlines |
| `line-control` | `b-400` | `b-400` | Edges of inputs and outline buttons (≥3:1) |
| `accent` / `on-accent` | `b-800` / white | `b-200` / `b-950` | Primary fills and the text on them |
| `accent-text` | `b-600` | `b-200` | Links, active tab, brand-colored text |
| `accent-soft` | `b-100` | `b-800` | Selected rows, active chips, hover |
| `focus` | `b-600` | `b-200` | 2px focus ring, with a `soft` halo outside it |

- The anchors `ink`, `mid`, `soft` and the fixed `ground` are brand identity. Use them on covers and marketing surfaces, not in UI components.
- `b-400` (Mid) is 5.2:1 on white and 4.5:1 on the ground, so control edges pass on both.
- Reserve `ok`, `warn` and `danger` for semantic states only. They don't change with the brand palette; each has its own dark value.
- Never write a hex value in a component. If a color you need has no token, the token is missing, not the rule.

### Personalization

Five preferences, each one attribute on `<html>`; `bundle.css` maps them to tokens. See the `AppearanceSettings` component for the UI and storage rules.

- **Brand color** (`data-brand`): purple (default, no attribute), `indigo`, `blue`, `teal`, `green`, `olive`, `gold`, `copper`, `crimson`, `rose`, `slate`. Each palette is a full 14-step ramp (`teal-25` … `teal-1000`) luminance-matched to the purple ramp step by step, so every contrast ratio above holds in every palette. Switching a palette re-points `b-*`; the semantic tokens follow.
- **Font** (`data-font`): `plex` IBM Plex Sans Arabic (default), `cairo` Cairo, `tajawal` Tajawal, `noto` Noto Sans Arabic, `alexandria` Alexandria. All five come from Google Fonts. The choice sets `--font-ui`; numbers stay in Inter.
- **Mode** (`data-theme`): `light` or `dark`; "system" follows `prefers-color-scheme`.
- **Text size** (`data-font-size`): `sm` 14px, `md` 16px, `lg` 18px, `xl` 20px root. Type and control heights are in rem, so everything scales together.
- **Density** (`data-density`): `compact`, `comfortable`, `spacious` → `control-h-compact`, `control-h`, `control-h-spacious`. Compact is ignored on touch screens.

Save preferences on the user's account and cache them in `localStorage` (`hr-prefs`); apply the cache in an inline `<head>` script before first paint. Only `theme.js` (extended to `window.hrPrefs`) writes these attributes.

### Typography

- Arabic UI text uses `--font-ui` through the `Arabic UI` styles: `display`, `h1`, `h2`, `h3`, `body` (15px, line-height 1.7), `label`, `caption`.
- Numbers, dates, IDs, English and code use `numeric` (Inter) through `num` and `num-lg`, with `font-variant-numeric: tabular-nums`.
- Arabic needs taller leading than Latin: keep body at 1.7 and headings at 1.3–1.5. Don't letter-space Arabic.
- Production pages load only the selected Arabic family plus Inter.

### Layout, direction and screens

- Use CSS logical properties only: `margin-inline`, `padding-inline`, `border-inline-start`, `inset-inline-start`. Never `left`/`right` or `margin-left`.
- Space on the 4pt rhythm: `s-1` (4px) through `s-16` (64px).
- Components respond to their own width with container queries, so the same component works in a full page, a drawer or a side panel. Page shells respond to the viewport. Both use the `breakpoint` tokens: `bp-sm` 480, `bp-md` 768, `bp-lg` 1024, `bp-xl` 1280, `bp-2xl` 1536.
- Phones: one column, the navigation becomes a drawer or a bottom tab bar of at most five items (see Navigation), tabs scroll sideways, tables turn into cards, and primary actions share a full-width row.
- Tap targets are at least 44px (`control-h`) on touch screens whatever the density.
- Test every screen at 390px, 768px and 1280px, in both modes, and at the `xl` text size.

### Radii

Radius grows with the surface: `r-xs` for badges and tags, `r-sm` for inputs and buttons, `r` for cards and controls, `r-md` for drawers and panels, `r-lg` for modals and dialogs, `r-xl` for callouts, `r-full` for pill badges and avatars.

### Elevation

`shadow-xs` (inputs, buttons), `shadow-sm` (cards), `shadow-md` (hover, sticky headers, menus), `shadow-lg` (drawers, modals), `shadow-pop` (command palette, toasts). Light shadows are tinted with brand ink `rgba(28, 12, 43, …)`; dark ones are near-black. Separate surfaces with a `line` outline first and a shadow second.

### Motion

Durations match the distance travelled:

| Token | Duration | Use |
| :--- | :--- | :--- |
| `--t-tap` | 90ms | Active / pressed states |
| `--t-state` | 140ms | Hover, focus ring expansion |
| `--t-move` | 220ms | Tab shifts, list reordering |
| `--t-enter` | 260ms | Drawer and modal entrance |
| `--t-exit` | 170ms | Drawer and modal exit |

Default curve is `--ease`, `cubic-bezier(0.2, 0.7, 0.25, 1)`; `--ease-out` and `--ease-in` cover entrances and exits. Everything stops under `prefers-reduced-motion`.

## The employee interface

Employees see a self-service shell, not the admin console. Keep it to what they own and what they can ask for.

- **Home**: greeting, the `EmployeeProfile` header, the four KPI tiles, "يحتاج انتباهك" (expiring documents, missing data, requests waiting on them), and three quick actions: request leave, request a letter, update my data.
- **My profile**: `EmployeeProfile`, read-first. Editing opens a drawer per section, and changes to verified fields (name, certificates, bank account) become a request to HR, not a direct write.
- **My requests**: a `DataTable` of their own requests with status badges; on phones, cards.
- **Payslips and documents**: download lists, sensitive values masked until revealed, every reveal logged.
- **Settings**: `AppearanceSettings`.

Show employees no bulk actions, no other people's data except their manager's name and contact, and no admin terminology. Managers get the same shell plus an approvals inbox; HR admins get the full console with compact density by default.

## Navigation

Pick one navigation pattern per app, by audience and number of destinations:

| Pattern | Use when | Destinations |
| :--- | :--- | :--- |
| `SideNav` (vertical) | HR admins and managers; deep apps with groups and sub-pages | 7 or more, grouped |
| `TopNav` (horizontal) | Employee self-service on desktop; shallow apps | up to 6 |
| `FloatingNav` (floating) | The employee app on phones, kiosks, focused portals | 3 to 5 |

- Every pattern puts the logo at the inline start, marks the current page with `aria-current="page"` and `accent` styling, and starts the page with a "تخطَّ إلى المحتوى" skip link.
- Sizes come from the `layout` tokens (`nav-w` 264px, `nav-w-rail` 72px, `topbar-h` 64px, `topbar-h-sm` 56px, `dock-h` 64px) and stacking from `zIndex` (`z-nav`, `z-scrim`, `z-drawer`). Never hard-code either.
- Below 768px, SideNav and TopNav collapse into a 56px bar with a menu button that opens a drawer; FloatingNav becomes a bottom tab bar. Nothing in the navigation is hover-only.
- A common employee setup: TopNav on desktop and FloatingNav on phones, showing the same five destinations in the same order.

## Components

| Component | In this system | Use for | Accessibility contract |
| :--- | :--- | :--- | :--- |
| `DsButton` | `Button` | Buttons, actions, icon triggers | Visible focus ring; real disabled state; icon-only needs `aria-label` |
| `DsStatusBadge` | `StatusBadge` | Employee, request and document status | Dot plus word, never color alone |
| `AppearanceSettings` | `AppearanceSettings` | The user's appearance preferences | Native radio groups with legends |
| `EmployeeProfile` | `EmployeeProfile` | The employee's own record | ARIA tabs with arrow-key navigation; masked sensitive fields |
| `DataTable` | `DataTable` | Lists of employees, requests, documents | `aria-sort`, labeled checkboxes, live count of selection |
| `SideNav` | `SideNav` | Vertical app navigation | Labeled `nav`, `aria-current`, modal drawer on phones, rail tooltips on focus |
| `TopNav` | `TopNav` | Horizontal app navigation | Labeled `nav`, `aria-current`, modal drawer under 1024px |
| `FloatingNav` | `FloatingNav` | Floating dock / bottom tab bar | Labeled `nav`, `aria-current`, never hides while focused |
| `DsInput` | in `bundle.css` (`hr-input`) | Text, search, number fields | Labeled; `line-control` edge |
| `DsSelect` | in `bundle.css` (`hr-input`) | Dropdown filters | Accessible options list |
| `DsDrawer` | not yet | Flyout panels | `role="dialog"`, `aria-modal="true"`, focus trap, `Escape` closes |
| `DsModal` / `Modal` | not yet | Dialogs, confirmations | Accessible scrim, keyboard dismissal |
| `DsToast` | not yet | Temporary feedback | `aria-live="polite"`; duplicates announced once |
| `DsAlert` | not yet | In-flow notices | Status icon paired with status color |
| `CommandPalette` | not yet | `Ctrl+K` navigation | Full keyboard navigation |

## Iconography

Line icons, 24px grid, 2px stroke, round caps, drawn in `currentColor` so they take `text-muted`, `accent-text` or a status color from their context. Every icon on a surface meets 3:1. A status icon always travels with its label; an icon-only button always has an `aria-label`. No emoji in the UI.
