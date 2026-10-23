// Run from API.HR.Node/: node ../design/check-vars.cjs
// Fails when a var(--x) without a fallback names a custom property no stylesheet,
// <style> block or setProperty() call declares. Such a declaration is dropped by
// the browser: radius falls to 0, colour is inherited, and nothing else reports it.
const fs = require('fs'), path = require('path');
const walk = d => fs.readdirSync(d, { withFileTypes: true }).flatMap(e => {
  const p = path.join(d, e.name);
  if (e.isDirectory()) return /^(bin|obj|lib|_archive|node_modules)$/.test(e.name) ? [] : walk(p);
  return /\.(razor|css|cs|js)$/.test(e.name) ? [p] : [];
});
const files = [...walk('Components'), ...walk('wwwroot')];
const def = new Set(), use = {};
const PSEUDO = /^(hover|focus|active|not|is|where|before|after|disabled|checked|first|last|nth)/;
for (const f of files) {
  const s = fs.readFileSync(f, 'utf8');
  for (const m of s.matchAll(/(?:^|[\s{;"'(])(--[a-zA-Z0-9-]+)\s*:([a-z-]*)/gm)) {
    if (!PSEUDO.test(m[2])) def.add(m[1]);
  }
  for (const m of s.matchAll(/setProperty\(\s*['"](--[a-zA-Z0-9-]+)/g)) def.add(m[1]);
  s.split('\n').forEach((l, i) => {
    for (const m of l.matchAll(/var\((--[a-zA-Z0-9-]+)\s*(,)?/g)) (use[m[1]] ??= []).push({ f: f.split(path.sep).join('/'), l: i + 1, fb: !!m[2] });
  });
}
let bad = 0;
for (const [t, u] of Object.entries(use)) {
  if (def.has(t)) continue;
  const nf = u.filter(x => !x.fb);
  if (nf.length) bad++;
  console.log(t.padEnd(22), 'uses', u.length, 'no-fallback', nf.length, [...new Set(nf.map(x => x.f + ':' + x.l))].slice(0, 8).join(' '));
}
process.exit(bad ? 1 : 0);
