import { readFileSync, readdirSync, existsSync, statSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
const root = resolve(import.meta.dirname, '..');
// Copied external references contain links relative to their original website.
const excluded = /(?:^|\/)(?:old|completed|referencia-externa|node_modules|bin|obj|\.git)(?:\/|$)/;
function walk(dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap(e => {
    const path = resolve(dir, e.name), rel = relative(root, path).replaceAll('\\', '/');
    if (excluded.test(rel)) return [];
    return e.isDirectory() ? walk(path) : e.name.endsWith('.md') ? [path] : [];
  });
}
const files = ['README.md', 'AGENTS.md', 'CONSOLIDATED_DOCS.md', 'src/README.md']
  .map(p => resolve(root, p)).filter(existsSync)
  .concat(...['docs', 'templates', 'conductor', '.github/ISSUE_TEMPLATE'].map(d => walk(resolve(root, d))));
let checked = 0; const broken = [];
for (const file of files) {
  const source = readFileSync(file, 'utf8').replace(/```[\s\S]*?```/g, '');
  for (const match of source.matchAll(/\[[^\]]*\]\(([^\n)]+)\)/g)) {
    let target = match[1].trim().replace(/^<|>$/g, '').split(/\s+"/)[0];
    if (/^(?:https?:|mailto:|#)/i.test(target) || /[{}<>]|\.\.\./.test(target)) continue;
    checked++;
    if (/^file:/i.test(target)) { broken.push({ file: relative(root, file), target, reason: 'nonportable file URL' }); continue; }
    target = decodeURIComponent(target.split('#')[0]);
    if (target && !existsSync(resolve(dirname(file), target))) broken.push({ file: relative(root, file), target, reason: 'missing path' });
  }
}
console.log(JSON.stringify({ files: files.length, checked, broken }, null, 2));
if (broken.length) process.exitCode = 1;
