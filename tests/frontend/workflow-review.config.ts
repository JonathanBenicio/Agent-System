import { defineConfig, devices } from '@playwright/test'
export default defineConfig({ testDir: './tests/e2e', testMatch: 'workflow-review.e2e.spec.ts', workers: 1, timeout: 30000,
 use: { ...devices['Desktop Chrome'], baseURL: 'http://127.0.0.1:5195' }, reporter: 'line', outputDir: './test-results/workflow-review',
 webServer: { command: 'node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5195 --strictPort',
   url: 'http://127.0.0.1:5195', reuseExistingServer: false, cwd: '../../frontend' } })
