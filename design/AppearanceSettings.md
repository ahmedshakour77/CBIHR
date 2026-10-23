# AppearanceSettings

The user's own appearance preferences: brand color (11 palettes), UI font (5 Arabic families), light/dark/system mode, text size (4 steps) and density (3 steps).

Every choice is one attribute on `<html>`, and `bundle.css` does the rest. Nothing else in the UI changes its code:

| Preference | Attribute | Values | Default |
| :--- | :--- | :--- | :--- |
| Brand color | `data-brand` | `indigo` `blue` `teal` `green` `olive` `gold` `copper` `crimson` `rose` `slate` (absent = purple) | purple |
| Font | `data-font` | `plex` `cairo` `tajawal` `noto` `alexandria` | `plex` |
| Mode | `data-theme` | `light` `dark` (the "system" choice resolves to one of these from `prefers-color-scheme`) | system |
| Text size | `data-font-size` | `sm` 14px, `md` 16px, `lg` 18px, `xl` 20px root | `md` |
| Density | `data-density` | `compact` `comfortable` `spacious` | `comfortable` |

The consumer provides the saved preferences and a save handler. Store them on the user's account (for example `GET/PUT /api/me/preferences`) so they follow the employee across devices, and cache them in `localStorage` under `hr-prefs`. Apply the cached copy in an inline script in `<head>`, before the first paint, so the page never flashes the default purple. Extend `theme.js` into one `window.hrPrefs` with `get()`, `set(partial)` and `reset()`; keep `hrTheme.toggle()` as a shortcut that calls it.

Why brand switching is safe: each of the 10 extra palettes is luminance-matched to the purple ramp step by step, so every contrast figure in the tokens holds for every palette. Status colors (`ok`, `warn`, `danger`) never change with the brand. With the green or crimson brand, status still reads correctly because every status carries a dot and a word.

Load only the chosen font from Google Fonts in production (one `<link>` swapped by `hrPrefs`), not all five. Compact density falls back to comfortable on touch screens (`pointer: coarse`), so tap targets never drop under 44px.

Group the options exactly as shown: color, font, mode, text size, density. Show a live sample beside the form on wide screens and below it on phones. Save on change; no "Apply" button.
