# DataTable

The workspace for lists of employees, requests and documents: search, saved views, filters, sort, multi-select with bulk actions, and pagination; on phones every row becomes a card.

The consumer provides the columns (label, key, sortable or not, which one is the "person" column), the rows, the total count, the saved views, and handlers for search, sort, page, selection and bulk actions. Sorting, paging and filtering run on the server; the table only reflects state.

## Anatomy

1. **Toolbar**: title with the total count, search (`hr-input`), saved-view select, "تصفية", "تصدير" (hidden on phones), one primary action.
2. **Bulk bar**: appears only while rows are selected, on `accent-soft`, announces the count through `aria-live="polite"`. Destructive bulk actions use `hr-btn--danger` and confirm in a modal.
3. **Header**: sticky, `surface-2`, `label`-size text. Sortable columns are buttons inside `th` with `aria-sort` (`ascending`, `descending`, `none`); one sorted column at a time.
4. **Rows**: first column checkbox, second the person (avatar, name, email). IDs, dates and amounts in `hr-num`. Status as `StatusBadge`. Last column a single overflow menu, never a row of icons. Hover `surface-2`, selected `accent-soft`.
5. **Footer**: "عرض 1–25 من 2,330" and the pager; the current page carries `aria-current="page"`.

## Density

Row height follows `--ctl` + 12px: compact 50px, comfortable 56px, spacious 62px at a 16px root. HR admins get compact by default on desktop; everyone else comfortable. Compact never applies on touch screens.

## Screens

- **640px and up** (container): a real table. When columns exceed the width, the table scrolls sideways inside its card and the header stays pinned.
- **Under 640px**: the header row is visually hidden (still read by screen readers), each row is a card: checkbox, person and menu on the first line, then every other cell as a "label: value" line from its `data-label`. Every cell other than the checkbox, person and menu must carry `data-label`.

## Rules

- Don't put more than 7 visible columns on desktop; let users add the rest through column settings in a saved view.
- Empty, loading and error states replace the `tbody`, keep the toolbar, and say what to do next.
- Never truncate names; wrap them. Truncate only emails, with the full value in `title`.
