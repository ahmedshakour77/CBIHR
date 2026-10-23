# SideNav

Vertical navigation fixed to the inline-start edge (the right in Arabic): the default shell for HR admins, managers and any app with more than six destinations.

The consumer provides the logo and product name, the groups of destinations (label, icon, href, optional count, optional children), the current route, the signed-in user, and a handler for search (`Ctrl K` opens the command palette).

## Spec

| Part | Size | Tokens |
| :--- | :--- | :--- |
| Width, expanded | 264px | `nav-w` |
| Width, collapsed rail | 72px | `nav-w-rail` |
| Width, phone drawer | 320px, max 85% of screen | `nav-w-drawer` |
| Item height | 44px (38 compact, 50 spacious) | `--ctl` from `control-h*` |
| Item | 20px icon, 12px gap, 12px inline padding, `r-sm` corners | `s-3`, `r-sm` |
| Group label | 12px, `text-muted` | `caption` |
| Count badge | 20px pill, `accent` / `on-accent`, Inter | `r-full` |
| Surface | `surface`, `line` edge on the content side | `z-nav` |

Anatomy, top to bottom: header (logo 36px, product name, collapse button), search trigger, groups ("خدماتي", "المؤسسة"), then a footer pinned to the bottom with Settings and the user.

## States

- Rest `text-2` with `text-muted` icon; hover `surface-2`; current page `accent-soft` fill, `accent-text` label and icon, weight 600, and `aria-current="page"`.
- A parent with children is a `button` with `aria-expanded` and a chevron that turns down when open. One level of nesting only. Open the parent of the current page by default.
- Collapsed rail: icons only, a divider replaces group labels, counts move to the icon's corner, the label appears as a tooltip on hover and keyboard focus. The user's choice is saved with their preferences.

## Screens

| Width | Behavior |
| :--- | :--- |
| 1024px and up | Expanded; the user can collapse it to the rail |
| 768–1023px | Rail by default, no collapse button |
| under 768px | Hidden. A 56px top bar (`topbar-h-sm`) shows a menu button, logo and notifications; the menu opens the nav as a drawer from the inline-start edge over a scrim |

## Accessibility

- `<nav aria-label="التنقل الرئيسي">`, destinations as links in lists labeled by their group heading.
- A "تخطَّ إلى المحتوى" skip link is the first focusable element on the page.
- The phone drawer is a modal dialog while open: focus moves into it, Tab is trapped, `Escape` and the scrim close it, and focus returns to the menu button.
- The collapse button has `aria-expanded` and a label that says what it will do.

Don't mix SideNav and TopNav links in one app; a TopNav-style bar on top of a SideNav holds only search, notifications and the user.
