#!/usr/bin/env node
/*
 * design/check-classes.cjs
 *
 * Finds `hr-*` class names used in markup that no stylesheet defines.
 * An undefined class is silent: the element renders as an unstyled shell and
 * nothing in the build, the browser console, or the page reports it.
 *
 *     node design/check-classes.cjs
 */
'use strict';

const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..');
const APP = path.join(ROOT, 'API.HR.Node');
const SHEETS = [
  'wwwroot/css/hr-tokens.css',
  'wwwroot/app.css',
  'wwwroot/css/hr-components.css',
  'wwwroot/hr-ui.css',
];

/* every class name any stylesheet defines, including scoped .razor.css */
const defined = new Set();
function collect(file) {
  const css = fs.readFileSync(file, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
  for (const m of css.matchAll(/\.(-?[_a-zA-Z][\w-]*)/g)) defined.add(m[1]);
}
for (const s of SHEETS) collect(path.join(APP, s));

function walk(dir, test) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(e => {
    const p = path.join(dir, e.name);
    return e.isDirectory() ? walk(p, test) : (test(e.name) ? [p] : []);
  });
}
for (const f of walk(path.join(APP, 'Components'), n => n.endsWith('.razor.css'))) collect(f);

/* A <style> element inside a .razor file is real CSS, but it only exists in
   the page while that component is rendered. So a class it defines counts as
   defined for markup in the same file, and for nowhere else: a class used in
   one page and styled only in another page's <style> block is a bug that
   shows up only when the other page happens to have loaded first. */
function localDefinitions(text) {
  const local = new Set();
  for (const block of text.matchAll(/<style\b[^>]*>([\s\S]*?)<\/style>/gi)) {
    const css = block[1].replace(/\/\*[\s\S]*?\*\//g, '');
    for (const m of css.matchAll(/\.(-?[_a-zA-Z][\w-]*)/g)) local.add(m[1]);
  }
  return local;
}

/* every hr-* class used in markup, with where it is used */
const used = new Map();
for (const file of walk(path.join(APP, 'Components'), n => n.endsWith('.razor'))) {
  const text = fs.readFileSync(file, 'utf8');
  const rel = path.relative(APP, file).replace(/\\/g, '/');
  const local = localDefinitions(text);
  text.split(/\r?\n/).forEach((line, i) => {
    for (const attr of line.matchAll(/class="([^"]*)"/g)) {
      // drop Razor expressions; keep the literal class tokens around them
      for (const name of attr[1].replace(/@\([^)]*\)|@[\w.]+/g, ' ').split(/\s+/)) {
        if (!/^hr-[\w-]+$/.test(name)) continue;
        if (local.has(name)) continue;
        if (!used.has(name)) used.set(name, []);
        used.get(name).push(`${rel}:${i + 1}`);
      }
    }
  });
}

/* Classes that exist only as selectors for script, never for styling.
   Each one names the code that queries it; add to this list only with that. */
const SCRIPT_HOOKS = new Set([
  'hr-pref-toggle',   // wwwroot/js/lang.js, CommandPalette.razor "act-prefs"
  'hr-org-card',      // wwwroot/js/hr-orgchart.js: drag-to-pan ignores presses on a card
]);

const orphans = [...used.entries()].filter(([n]) => !defined.has(n) && !SCRIPT_HOOKS.has(n))
  .sort((a, b) => b[1].length - a[1].length);

for (const [name, sites] of orphans) {
  console.log(`UNDEFINED  .${name}  (${sites.length} use${sites.length > 1 ? 's' : ''})`);
  for (const s of sites.slice(0, 5)) console.log('             ' + s);
  if (sites.length > 5) console.log(`             … and ${sites.length - 5} more`);
}

console.log(`\n${used.size} distinct hr-* classes in markup, `
  + `${defined.size} defined in CSS - `
  + (orphans.length ? `${orphans.length} UNDEFINED` : 'none undefined'));
process.exit(orphans.length ? 1 : 0);
