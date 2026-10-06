import { defineConfig } from 'cypress'
import { createHmac } from 'node:crypto'

if (process.env.RUN_API_TESTS === 'true' && !process.env.API_URL) throw new Error('RUN_API_TESTS requires API_URL and a prepared backend/provider/database.')

export default defineConfig({
  e2e: {
    baseUrl: 'http://127.0.0.1:5193',
    specPattern: process.env.RUN_API_TESTS === 'true' ? 'cypress/e2e/**/*.cy.{js,ts}' : 'cypress/e2e/*.mock.cy.js',
    supportFile: false,
    video: false,
    setupNodeEvents(on) {
      on('task', {
        createValidationJwt({ userId, tenantId }: { userId: string; tenantId: string }) {
          const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString('base64url')
          const unsigned = `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({
            iss: 'AgenticSystem', aud: 'AgenticSystem', sub: userId, tenant_id: tenantId,
            exp: Math.floor(Date.now() / 1000) + 3600
          })}`
          const secret = process.env.BACKEND_VALIDATION_JWT_SECRET
            ?? 'documentation-validation-jwt-secret-local-only-2026'
          return `${unsigned}.${createHmac('sha256', secret).update(unsigned).digest('base64url')}`
        }
      })
    },
  },
  env: {
    API_BASE_URL: process.env.API_URL || 'http://127.0.0.1:5188',
  },
})
