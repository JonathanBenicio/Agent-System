import { readFileSync, readdirSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve, relative } from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';

// Source inventory supplements OpenAPI: conditional protocols and hubs are separate.
const root = resolve(import.meta.dirname, '..');
const baseline = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim();
function files(dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap(e => e.isDirectory()
    ? files(resolve(dir, e.name)) : e.name.endsWith('.cs') ? [resolve(dir, e.name)] : []);
}
const routes = [];
for (const file of files(resolve(root, 'src/AgenticSystem.Api/Controllers'))) {
  const source = readFileSync(file, 'utf8');
  const declaration = source.search(/public\s+(?:(?:sealed|partial)\s+)*class\s+\w+Controller/);
  if (declaration < 0) continue;
  const controller = source.slice(declaration).match(/class\s+(\w+)Controller/)[1];
  const header = source.slice(0, declaration);
  const bases = [...header.matchAll(/\[Route\("([^"]+)"\)\]/g)].map(m => m[1].replace('[controller]', controller.toLowerCase()));
  for (const match of source.slice(declaration).matchAll(/\[Http(Get|Post|Put|Delete|Patch|Head|Options)(?:\("([^"]*)"\))?\]/g)) {
    const offset = declaration + match.index;
    const tail = source.slice(offset + match[0].length);
    const action = tail.match(/public\s+(?:async\s+)?([^\n]+?)\s+(\w+)\s*\(([\s\S]*?)\)\s*(?:\{|=>)/);
    if (!action) throw new Error('Unparsed action: ' + relative(root, file) + ':' + offset);
    const attrs = tail.slice(0, action.index);
    const line = source.slice(0, offset).split('\n').length;
    for (const base of bases) routes.push({
      method: match[1].toUpperCase(), path: '/' + [base, match[2]].filter(Boolean).join('/'),
      action: action[2], parameters: action[3].replace(/\s+/g, ' ').trim(),
      authorization: /AllowAnonymous/.test(attrs) ? 'anonymous' : /Authorize/.test(header + attrs) ? 'Authorize (roles/policy: see source)' : 'no Authorize attribute; inspect action',
      condition: source.includes('#if DEBUG || STAGING') ? 'build DEBUG or STAGING' : 'see runtime configuration in source',
      openApiHidden: /ApiExplorerSettings\(IgnoreApi\s*=\s*true\)/.test(attrs),
      source: relative(root, file).replaceAll('\\', '/'), line,
      sha256: createHash('sha256').update(source).digest('hex'),
    });
  }
}
routes.sort((a, b) => a.path.localeCompare(b.path) || a.method.localeCompare(b.method));
const duplicates = routes.filter((r, i) => routes.findIndex(x => x.method === r.method && x.path === r.path) !== i);
if (duplicates.length) throw new Error('Duplicate route: ' + JSON.stringify(duplicates));
const out = resolve(root, 'docs/backend'); mkdirSync(out, { recursive: true });
writeFileSync(resolve(out, 'endpoint-inventory.json'), JSON.stringify({ baseline, scope: 'Controller attributes; runtime OpenAPI and Program mappings must be checked separately', routes }, null, 2) + '\n');
const escape = s => s.replaceAll('|', '\\|').replaceAll('`', "'");
writeFileSync(resolve(out, 'endpoint-inventory.md'), '# Inventário de endpoints HTTP\n\nGerado com `node scripts/backend-contract-inventory.mjs`. Cada rota aponta para sua assinatura atual; parâmetros C# não substituem schema/obrigatoriedade de binding. Autorizações adicionais e flags exigem leitura da fonte. Endpoints fora do núcleo ainda não possuem prova integrada.\n\n' + routes.length + ' combinações método/rota, incluindo aliases. Consulte [contratos do núcleo](api-core.md) e [hubs/protocolos](transports.md).\n\n| Método | Rota | Ação/entrada C# | Autorização declarada | Fonte |\n|---|---|---|---|---|\n' + routes.map(r => `| ${r.method} | ${r.path} | ${escape(r.action + '(' + r.parameters + ')')} | ${r.authorization} | [${r.source.split('/').at(-1)}:${r.line}](../../${r.source}) |`).join('\n') + '\n');
console.log(JSON.stringify({ routes: routes.length, duplicateRoutes: duplicates.length }));
