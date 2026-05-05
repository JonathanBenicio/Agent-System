import { defineConfig } from 'cypress'

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:5000',
    specPattern: 'cypress/e2e/**/*.cy.{js,ts}',
    supportFile: false,
    video: false,
  },
  env: {
    API_BASE_URL: 'http://localhost:5000',
  },
})
