# TopNav

Horizontal navigation across the top: for employee self-service and any app with six or fewer top-level destinations.

The consumer provides the logo and product name, up to six destinations (label, icon, href, optional count), the current route, the signed-in user, the notification count, and optionally the second-level links for the current section.

## Spec

| Part | Size | Tokens |
| :--- | :--- | :--- |
| Bar height | 64px (56px on phones) | `topbar-h`, `topbar-h-sm` |
| Inline padding | 20px (8px on phones) | `s-5`, `s-2` |
| Link | 18px icon, 8px gap, full bar height, 2px bottom indicator | `s-2` |
| Icon buttons | 44px square | `--ctl` |
| Sub-nav | pill links 36px tall, `accent-soft` when current | `r-full` |
| Surface | `surface`, `line` bottom edge; `shadow-md` once the page scrolls | `z-nav` |

Order along the bar, from the inline-start edge: logo and name, the destinations, then at the inline end search, notifications, settings and the user.

## States

- Link rest `text-2`; hover shows a `line-control` indicator; current page `accent-text` with a 2px `accent` indicator and `aria-current="page"`.
- The bar sticks to the top; add `data-scrolled` after the first pixel of scroll to raise it with `shadow-md`.
- The notification dot is `danger` with a 2px `surface` ring; the button's label carries the count ("الإشعارات، 3 غير مقروءة").

## Screens

| Width | Behavior |
| :--- | :--- |
| 1280px and up | Everything visible, search as a field with its shortcut |
| 1024–1279px | Search and user name shrink to icons |
| 768–1023px | Links move into a drawer opened by a menu button at the inline start |
| under 768px | 56px bar: menu, logo, name, notifications, avatar; the sub-nav scrolls sideways |

## Accessibility

- The destination list sits in `<nav aria-label="التنقل الرئيسي">`; the sub-nav is a second `<nav>` with its own label ("أقسام الصفحة").
- The drawer follows the same modal rules as SideNav's: focus in, Tab trapped, `Escape` closes, focus returns.

More than six destinations don't fit: move to SideNav instead of adding a "المزيد" menu.
