# Frontend validation

`npm ci && npx playwright install --with-deps chromium firefox && npm test` starts an isolated Vite server on loopback 5194 and runs Chromium/Firefox UI tests with mocked API responses. Playwright owns and shuts down that server; an occupied port fails rather than reusing an unrelated process. These runs exercise the actual React application, not a real API, PostgreSQL or LLM provider.

API integration is an explicit separate mode: prepare the backend/database/provider and memberships first, set `RUN_API_TESTS=true` and `API_URL`, then select `--project="API Tests"`. No API integration specs are currently discovered in this directory. No backend is implicitly started by the default CI job.

Legacy real browser scenarios require `REAL_E2E=true`, `E2E_API_KEY` for an active tenant-associated principal, and a configured API reached through `VITE_API_PROXY_TARGET` (or an explicitly supplied frontend `BASE_URL`). The fixture logs in through `/api/auth/login` and retains the HttpOnly cookie; it never injects or persists an API key in localStorage. These scenarios are opt-in and are not evidence from the mock CI run.

From `frontend`, `npm run cy:run` starts Vite on loopback 5193, runs `*.mock.cy.js` and shuts down its process group in `finally`. Real Cypress API/UI scenarios require `RUN_API_TESTS=true`, `API_URL`, configured authentication/membership/provider/database and `BACKEND_VALIDATION_JWT_SECRET` for the validation issuer where applicable. They are not silently counted as passing in the default mock suite.

Chat XSS checks first require the response content to be visible before asserting absence of active script/iframe nodes. Timeout checks use Playwright's virtual clock across the product's 120-second timeout. Citation tests prove that optional incoming REST/history fields survive client mapping; the present backend emission/persistence of citations remains outside this validation (#93/#110).

SQL diagnostics under `tests/backend-validation` require `BACKEND_VALIDATION_COMPOSE_PROJECT` and an exclusive `BACKEND_VALIDATION_DATABASE` matching `review_pr152_*`. The helper validates the Compose file publishes PostgreSQL on loopback 55432, refuses 5432 and uses that project's postgres service/database. Start the API with the same variables; `start-api.ps1` records `api-target.json`, and both Node diagnostics and the C# harness fail if that manifest does not match their SQL target. The helper does not start or remove services or databases.
