#!/usr/bin/env node
/*
 * design/check-tokens.cjs
 *
 * Resolves the token graph the way the browser would, for every brand and
 * both themes, and asserts the contrast pairs the system promises.
 *
 *     node design/check-tokens.cjs
 *
 * It reads the generated CSS, not the JSON, so it catches a stale build as
 * well as a bad value.
 */
'use strict';

const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..');
const CSS = path.join(ROOT, 'API.HR.Node', 'wwwroot', 'css', 'hr-tokens.css');
const COMPONENTS = path.join(ROOT, 'API.HR.Node', 'wwwroot', 'css', 'hr-components.css');
const css = fs.readFileSync(CSS, 'utf8');

/* ---- colour maths ---- */
const srgb = v => (v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4));
const parse = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
const lum = h => { const c = parse(h).map(v => srgb(v / 255));
  return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]; };
const contrast = (a, b) => { const x = lum(a), y = lum(b);
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
const hex = rgb => '#' + rgb.map(v => Math.max(0, Math.min(255, Math.round(v)))
  .toString(16).padStart(2, '0')).join('');

/* ---- read the declaration blocks we care about ---- */
function block(re) {
  const m = css.match(re);
  if (!m) throw new Error('block not found: ' + re);
  const out = {};
  for (const d of m[1].matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)) out[d[1]] = d[2].trim();
  return out;
}
const light = block(/:root, \[data-theme="light"\] \{([\s\S]*?)\n\}/);
const dark = block(/\n\[data-theme="dark"\] \{([\s\S]*?)\n\}/);

/* the alternate palettes and the [data-brand] remaps */
const palettes = block(/points the --b-\* ramp at one of these\. \*\/\n:root \{([\s\S]*?)\n\}/);
const brandMap = {};
for (const m of fs.readFileSync(COMPONENTS, 'utf8')
  .matchAll(/html\[data-brand="(\w+)"\] \{([^}]*)\}/g)) {
  brandMap[m[1]] = Object.fromEntries([...m[2].matchAll(/(--[\w-]+):\s*var\((--[\w-]+)\)/g)]
    .map(d => [d[1], d[2]]));
}

/* ---- resolve one cascade ---- */
function resolve(theme, brand) {
  const vars = { ...palettes, ...light };
  if (theme === 'dark') Object.assign(vars, dark);
  if (brand !== 'purple') for (const [k, v] of Object.entries(brandMap[brand])) vars[k] = vars[v];

  const seen = new Set();
  function value(expr, depth = 0) {
    if (depth > 20) throw new Error('var() cycle in ' + expr);
    return expr.replace(/var\((--[\w-]+)\)/g, (_, name) => {
      if (!(name in vars)) { seen.add(name); return 'UNDEFINED'; }
      return value(vars[name], depth + 1);
    });
  }
  const out = {};
  for (const k of Object.keys(vars)) out[k] = value(vars[k]);

  /* flatten the two-stop color-mix() forms the system uses */
  for (const [k, v] of Object.entries(out)) {
    const m = v.match(/^color-mix\(in srgb, (#[0-9a-f]{6}) (\d+)%, (#[0-9a-f]{6}|transparent)\)$/i);
    if (!m) continue;
    const p = +m[2] / 100;
    if (m[3].toLowerCase() === 'transparent') { out[k] = { over: m[1], alpha: p }; continue; }
    out[k] = hex(parse(m[1]).map((v2, i) => v2 * p + parse(m[3])[i] * (1 - p)));
  }
  return { vars: out, missing: [...seen] };
}

/* ---- the promises the system makes ---- */
const CHECKS = [
  ['body text',            'text',         ['ground', 'surface'], 4.5],
  ['secondary text',       'text-2',       ['ground', 'surface'], 4.5],
  ['muted text',           'text-muted',   ['ground', 'surface'], 4.5],
  ['accent text',          'accent-text',  ['ground', 'surface'], 4.5],
  ['text on accent fill',  'text-on-accent', ['accent'],          4.5],
  ['control edge',         'line-control', ['ground', 'surface', 'surface-raised'], 3],
  ['focus ring',           'focus',        ['ground', 'surface'], 3],
  ['success',              'ok',           ['ground', 'surface'], 4.5],
  ['warning',              'warn',         ['ground', 'surface'], 4.5],
  ['danger',               'danger',       ['ground', 'surface'], 4.5],
  ['series label',         'series-on',    ['series-1', 'series-2', 'series-3', 'series-4', 'series-5', 'series-6'], 4.5],
  ['tinted label',         'accent-ink',   ['accent-tint', 'accent-tint-2'], 4.5],
];

const brands = ['purple', ...Object.keys(brandMap)];
let failures = 0, missing = new Set(), checked = 0;
const worst = {};   // label -> the lowest ratio seen across every brand and theme

for (const theme of ['light', 'dark']) {
  for (const brand of brands) {
    const { vars, missing: gaps } = resolve(theme, brand);
    gaps.forEach(g => missing.add(g));
    for (const [label, fg, bgs, min] of CHECKS) {
      for (const bg of bgs) {
        const a = vars[`--${fg}`], b = vars[`--${bg}`];
        if (typeof a !== 'string' || typeof b !== 'string'
            || !a.startsWith('#') || !b.startsWith('#')) continue;
        checked++;
        const ratio = contrast(a, b);
        const slot = `${label} (${theme})`;
        if (!(slot in worst) || ratio < worst[slot]) worst[slot] = ratio;
        if (ratio < min) {
          failures++;
          console.log(`FAIL  ${theme.padEnd(5)} ${brand.padEnd(8)} ${label} on ${bg}: `
            + `${a} / ${b} = ${ratio.toFixed(2)}:1 (need ${min}:1)`);
        }
      }
    }
  }
}

/* ramp steps must stay distinguishable from their neighbours */
const STEPS = [25, 50, 100, 150, 200, 300, 400, 500, 600, 700, 800, 900, 950, 1000];
const fams = [...new Set(Object.keys(palettes).map(k => k.replace(/-\d+$/, '').slice(2)))]
  .concat('b');
for (const fam of fams) {
  const src = fam === 'b' ? light : palettes;
  let prev = null, prevStep = null;
  for (const step of STEPS) {
    const v = src[`--${fam}-${step}`];
    if (!v || !v.startsWith('#')) continue;
    if (prev) {
      const d = Math.hypot(...parse(v).map((c, i) => c - parse(prev)[i]));
      if (d < 8) {
        failures++;
        console.log(`FAIL  ramp ${fam}-${prevStep} -> ${fam}-${step}: `
          + `${prev} / ${v} differ by ${d.toFixed(1)}/441 - the step is invisible`);
      }
      if (lum(v) > lum(prev)) {
        failures++;
        console.log(`FAIL  ramp ${fam}-${step} (${v}) is lighter than ${fam}-${prevStep} (${prev})`);
      }
    }
    prev = v; prevStep = step;
  }
}

if (missing.size) {
  failures += missing.size;
  console.log('FAIL  undefined custom properties: ' + [...missing].join(', '));
}

console.log('\nworst case across every brand, per pair:');
for (const [k, v] of Object.entries(worst).sort((a, b) => a[1] - b[1]))
  console.log('  ' + k.padEnd(30) + v.toFixed(2) + ':1');

console.log(`\n${checked} contrast pairs across ${brands.length} brands x 2 themes, `
  + `${fams.length} ramps - ${failures ? failures + ' FAILED' : 'all pass'}`);
process.exit(failures ? 1 : 0);
