# FloatingNav

A floating dock of up to five destinations: a centered pill above the content on desktop and a bottom tab bar on phones, the pattern people know from mobile apps.

Use it for the employee app on phones and kiosks, and for focused employee portals on desktop. The consumer provides exactly 3 to 5 destinations (label, icon, href, optional count), the current route and, for the top floating bar, the logo, notifications and user.

## Spec

| Part | Size | Tokens |
| :--- | :--- | :--- |
| Dock height | 64px plus the device safe-area inset | `dock-h` |
| Distance from the bottom edge | 24px desktop, 8px phones | `s-6`, `s-2` |
| Desktop item | pill, icon and label side by side | `r-full` |
| Phone item | 20% of the width, icon above a 11px label | `r` |
| Surface | `surface`, `line` edge, `shadow-pop` | `z-nav` |
| Top floating bar | 16px from the edges, `r-lg`, `shadow-md` | `s-4` |

## States

- Desktop: the current item is a solid `accent` pill with `on-accent` text; the others are `text-2`.
- Tablet: only the current item shows its label; the others are icons with their label kept for screen readers.
- Phone: every item shows its label under the icon; the current one sits on `accent-soft`.
- The dock slides away when the page scrolls down and comes back on scroll up, and never hides while it holds keyboard focus.
- Page content gets bottom padding of `dock-h` plus 40px so the last row is never under the dock.

## Screens

| Width | Behavior |
| :--- | :--- |
| 1024px and up | Centered pill with labels |
| 768–1023px | Centered pill, labels only on the current item |
| under 768px | Full-width bottom tab bar, labels under icons |

## Accessibility

- `<nav aria-label="التنقل الرئيسي">` around the list; current item with `aria-current="page"`.
- Counts are read out ("2 جديد"); icon-only states keep a visually hidden label.
- Respect `prefers-reduced-motion`: no slide, just show or hide.

Five is the limit. If a sixth destination is needed, the fifth becomes "المزيد" and opens a bottom sheet.
