import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const cypress = require('cypress');
const port = 5193;
const origin = `http://127.0.0.1:${port}`;
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', String(port), '--strictPort'], { env: { ...process.env, ...(process.env.API_URL ? { VITE_API_PROXY_TARGET: process.env.API_URL } : {}) }, stdio: 'inherit', detached: process.platform !== 'win32' });
let serverExited = false;
server.once('exit', () => { serverExited = true; });
const stop = () => {
  if (!serverExited) {
    if (process.platform === 'win32') spawn('taskkill', ['/pid', String(server.pid), '/T', '/F']);
    else process.kill(-server.pid, 'SIGTERM');
  }
};
process.once('SIGINT', () => { stop(); process.exit(130); });
process.once('SIGTERM', () => { stop(); process.exit(143); });
try {
  const deadline = Date.now() + 60000;
  while (true) {
    if (serverExited) throw new Error('Vite exited before Cypress could connect.');
    try { if ((await fetch(origin)).ok) break; } catch { /* Wait for Vite startup. */ }
    if (Date.now() > deadline) throw new Error('Vite startup timed out.');
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  const args = process.argv.slice(2);
  const specIndex = args.indexOf('--spec');
  const spec = specIndex >= 0 ? args[specIndex + 1] : undefined;
  const result = await cypress.run({ config: { baseUrl: origin }, ...(spec ? { spec } : {}) });
  process.exitCode = 'totalFailed' in result ? (result.totalFailed ? 1 : 0) : 1;
} finally { stop(); }
