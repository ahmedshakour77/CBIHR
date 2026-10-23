#!/usr/bin/env node
/*
 * design/build-tokens.cjs
 *
 * Generates API.HR.Node/wwwroot/css/hr-tokens.css from design/tokens.json.
 * tokens.json is the only place a token value is written by hand; run this
 * after every edit to it:
 *
 *     node design/build-tokens.cjs          write the CSS
 *     node design/build-tokens.cjs --check  fail if the CSS is out of date
 *
 * A value of "{other-token}" becomes var(--other-token), so a token can be
 * expressed against the ramp and follow both the theme and the chosen brand.
 */
'use strict';

const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..');
const SRC = path.join(ROOT, 'design', 'tokens.json');
const OUT = path.join(ROOT, 'API.HR.Node', 'wwwroot', 'css', 'hr-tokens.css');

const json = JSON.parse(fs.readFileSync(SRC, 'utf8'));

/* {token} -> var(--token). Anything else passes through untouched. */
const ref = v => String(v).replace(/\{([a-z0-9-]+)\}/gi, 'var(--$1)');
const decl = (name, value) => `  --${name}: ${ref(value)};`;

/* A token value is either a string or { light, dark }. */
const pair = t => (t.value && typeof t.value === 'object') ? t.value : null;
const light = t => (pair(t) ? pair(t).light : t.value);
const darkOf = t => (pair(t) ? pair(t).dark : null);

/* The brand ramp and the ten alternate palettes are plain ramp steps; every
   other colour token is semantic and may carry a dark value. */
const isRampStep = n => /^[a-z]+-(?:25|50|100|150|200|300|400|500|600|700|800|900|950|1000)$/.test(n);
const colour = json.color.tokens;
const ramp = colour.filter(t => isRampStep(t.name));
const semantic = colour.filter(t => !isRampStep(t.name));

const shadowLight = json.shadow.tokens.map(t => decl(t.name, t.value.light));
const shadowDark = json.shadow.tokens.map(t => decl(t.name, t.value.dark));

const section = (title, lines) =>
  lines.length ? `\n  /* ${title} */\n${lines.join('\n')}` : '';

const out = [];

out.push('/* API.HR.Node design tokens.');
out.push(' * GENERATED FROM design/tokens.json BY design/build-tokens.cjs - DO NOT EDIT.');
out.push(' * Edit the JSON and re-run: node design/build-tokens.cjs');
out.push(' */');
out.push('');

/* ---------- light ---------- */
out.push(':root, [data-theme="light"] {');
out.push('  color-scheme: light;');
out.push(section('brand ramp - the live palette; [data-brand] remaps these',
  ramp.filter(t => t.name.startsWith('b-')).map(t => decl(t.name, light(t)))));
out.push(section('semantic tokens - every rule in the system resolves through these',
  semantic.map(t => decl(t.name, light(t)))));
out.push(section('elevation', shadowLight));
out.push('}');
out.push('');

/* ---------- dark ----------
   Emitted twice on purpose. The attribute selector serves an explicit
   choice; the media query serves "follow the system", so a visitor whose
   OS is dark gets a dark first paint instead of a white flash before
   hr-prefs.js runs. :root:not([data-theme="light"]) outranks the
   attribute rule but can never fire against an explicit light choice. */
const darkBody = [
  '  color-scheme: dark;',
  section('only the semantic layer flips; the ramp is the same, so a brand change carries into dark mode',
    semantic.filter(darkOf).map(t => decl(t.name, darkOf(t)))),
  section('elevation', shadowDark),
];
out.push('[data-theme="dark"] {', ...darkBody, '}');
out.push('');
out.push('@media (prefers-color-scheme: dark) {');
out.push('  :root:not([data-theme="light"]) {');
out.push(...darkBody.map(b => b.split('\n').map(l => l && '  ' + l).join('\n')));
out.push('  }');
out.push('}');
out.push('');

/* ---------- alternate brand palettes ---------- */
out.push('/* Alternate brand palettes. html[data-brand] in hr-components.css');
out.push('   points the --b-* ramp at one of these. */');
out.push(':root {');
const families = [...new Set(ramp.map(t => t.name.replace(/-\d+$/, '')))].filter(f => f !== 'b');
for (const fam of families) {
  out.push(`  /* ${fam} */`);
  out.push(ramp.filter(t => t.name.startsWith(fam + '-')).map(t => decl(t.name, light(t))).join('\n'));
}
out.push('}');
out.push('');

/* ---------- scalar scales ---------- */
out.push(':root {');
for (const [key, title] of [
  ['spacing', 'spacing'], ['fontSize', 'type scale'], ['radius', 'radius'], ['control', 'control height'],
  ['layout', 'layout'], ['zIndex', 'layers'], ['breakpoint', 'breakpoints'],
  ['motion', 'motion'],
]) {
  const group = json[key];
  if (!group || !group.tokens) continue;
  out.push(section(group.note ? `${title} - ${group.note}` : title,
    group.tokens.map(t => decl(t.name, t.value))));
}
out.push(section('type families',
  Object.entries(json.type.families).map(([k, v]) => decl('font-' + k, v))));
out.push('}');
out.push('');

/* ---------- type styles ---------- */
out.push('/* Type styles. One class per role; never restate a size in a component. */');
for (const group of json.type.groups) {
  for (const s of group.styles) {
    const props = [
      `font-family: var(--font-${group.family})`,
      `font-size: ${s.fontSize}`,
      `line-height: ${s.lineHeight}`,
      `font-weight: ${s.fontWeight}`,
    ];
    if (group.family === 'numeric') props.push('font-variant-numeric: tabular-nums');
    out.push(`.${s.name} { ${props.join('; ')}; }`);
  }
}
out.push('');

const css = out.filter(l => l !== '').join('\n').replace(/\n{3,}/g, '\n\n') + '\n';

if (process.argv.includes('--check')) {
  const current = fs.existsSync(OUT) ? fs.readFileSync(OUT, 'utf8') : '';
  if (current !== css) {
    console.error('hr-tokens.css is out of date. Run: node design/build-tokens.cjs');
    process.exit(1);
  }
  console.log('hr-tokens.css is up to date.');
} else {
  fs.writeFileSync(OUT, css);
  const count = (css.match(/^\s+--/gm) || []).length;
  console.log(`wrote ${path.relative(ROOT, OUT)} - ${count} declarations`);
}
