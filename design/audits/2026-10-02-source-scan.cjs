const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
function files(dir) {
  return fs.readdirSync(dir, {withFileTypes:true}).flatMap(e => e.isDirectory() ? files(path.join(dir,e.name)) : [path.join(dir,e.name)]);
}
const razor = files(path.join(root,'API.HR.Node/Components')).filter(f=>f.endsWith('.razor'));
const css = ['hr-ui.css','app.css','css/hr-components.css','css/hr-tokens.css'].map(f=>path.join(root,'API.HR.Node/wwwroot',f));
const source = razor.map(f=>({file:path.relative(root,f),text:fs.readFileSync(f,'utf8')}));
const styles = css.map(f=>({file:path.relative(root,f),text:fs.readFileSync(f,'utf8').replace(/\/\*[\s\S]*?\*\//g,'')}));
const definitions = new Set(styles.flatMap(s=>Array.from(s.text.matchAll(/(--[\w-]+)\s*:/g),m=>m[1])));
const uses = [...source,...styles];
const unresolved = uses.flatMap(s=>Array.from(s.text.matchAll(/var\(\s*(--[\w-]+)\s*\)/g),m=>({token:m[1],file:s.file}))).filter(u=>!definitions.has(u.token));
const tally = regex => source.reduce((n,s)=>n+Array.from(s.text.matchAll(regex)).length,0);
function luminance(rgb) {
  const linear = rgb.map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4;});
  return linear[0]*.2126+linear[1]*.7152+linear[2]*.0722;
}
const rgb = hex => hex.match(/[a-f0-9]{2}/gi).map(x=>parseInt(x,16));
function contrast(a,b) { const x=luminance(a),y=luminance(b);return +((Math.max(x,y)+.05)/(Math.min(x,y)+.05)).toFixed(2); }
const darkSurface=rgb('2a143e').map((v,i)=>v*.8+rgb('1c0c2b')[i]*.2);
const output = {
  razorFiles:razor.length,
  inlineStyleAttributes:tally(/\bstyle\s*=/g),
  razorFilesWithInlineStyles:source.filter(s=>/\bstyle\s*=/.test(s.text)).length,
  rawRampReferencesInComponentCSS:styles.slice(0,3).reduce((n,s)=>n+Array.from(s.text.matchAll(/var\(--b-\d+\)/g)).length,0),
  unresolvedNoFallbackReferences:[...new Map(unresolved.map(u=>[u.file+u.token,u])).values()],
  mostInlineStyles:source.map(s=>({file:s.file,count:Array.from(s.text.matchAll(/\bstyle\s*=/g)).length})).sort((a,b)=>b.count-a.count).slice(0,8),
  additionalContrastPairs:{
    adminNotesOnPurpleDarkSurface:contrast(rgb('126957'),darkSurface),
    dangerDropdownHoverOnPurpleDarkSurface:contrast(rgb('dc2626'),darkSurface),
    decorativeLineOnWhite:contrast(rgb('e3d8ec'),rgb('ffffff')),
    decorativeLineOnInset:contrast(rgb('e3d8ec'),rgb('ede6f3'))
  }
};
console.log(JSON.stringify(output,null,2));
