import { defineConfig, devices } from '@playwright/test'
export default defineConfig({ testDir: './tests/e2e', testMatch: 'agent-contracts.e2e.spec.ts', workers: 1, timeout: 30000, reporter: 'list',
 outputDir: './test-results/agent-contracts', use: { baseURL: 'http://127.0.0.1:5196' },
 projects: [{ name: 'agent-contracts', use: { ...devices['Desktop Chrome'] } }],
 webServer: { command: 'node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5196 --strictPort',
 url: 'http://127.0.0.1:5196', reuseExistingServer: false, cwd: '../../frontend' } })
