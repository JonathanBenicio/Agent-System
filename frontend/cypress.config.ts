import { defineConfig } from 'cypress'
import { createHmac } from 'node:crypto'

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:5000',
    specPattern: 'cypress/e2e/**/*.cy.{js,ts}',
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
    API_BASE_URL: 'http://localhost:5000',
  },
})
