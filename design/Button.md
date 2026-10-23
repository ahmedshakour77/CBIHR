# Button

Triggers an action; one primary button per view, the rest secondary or ghost.

The consumer provides the label (a verb in Arabic: "تقديم طلب", not "طلب"), an optional leading icon, and the handler. Height follows the density preference through `--ctl` (38/44/50px at a 16px root) and grows with the font-size preference.

- `hr-btn--primary`: `accent` fill, `on-accent` text. The one action the page exists for.
- `hr-btn--secondary`: `surface` with a `line-control` edge. Everything else.
- `hr-btn--ghost`: `accent-text` only. Low-weight actions inside cards ("عرض الكل").
- `hr-btn--danger`: `danger` edge and text, never a solid red fill. Reject, delete, archive; always confirm in a modal.
- `hr-btn--icon`: square, needs `aria-label`.
- `hr-btn--sm`: inside table rows and toolbars.

Don't put two primary buttons side by side. Don't disable a button without saying why nearby. On phones, header action buttons stretch to share the row.
