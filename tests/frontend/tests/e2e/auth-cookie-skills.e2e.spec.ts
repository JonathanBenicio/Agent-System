import { test, expect, type Page } from '@playwright/test'

const key = 'browser-review-synthetic-api-key'
const identity = {
  userId: '00000000-0000-0000-0000-000000001400',
  tenantId: 'review-a',
  roles: ['Owner'],
}
const skill = {
  id: 'stored-preview', name: 'Skill compartilhada', domain: 'general',
  type: 'Instruction', isEnabled: true, isSystem: false, canManage: true,
}
const payload = '# Conteúdo seguro\n\n<img src=x onerror="globalThis.__skillXss=true">\n\n'
  + '<script>globalThis.__skillXss=true</script>\n\n[Link perigoso](javascript:globalThis.__skillXss=true)'

async function mockApi(page: Page) {
  page.on('pageerror', error => console.error('Browser error:', error.message))
  const state = { failLogout: false }
  const authHeaders: Record<string, string>[] = []
  await page.addInitScript(() => {
    localStorage.removeItem('agentic_auth_token')
    localStorage.setItem('agentic_api_key', 'old-client-credential')
    ;(window as Window & { __skillXss: boolean }).__skillXss = false
  })
  await page.route('**/hubs/**', route => route.abort())
  await page.route('**/api/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (!path.startsWith('/api/')) {
      await route.continue()
      return
    }
    authHeaders.push(request.headers())
    const cookieAuthenticated = request.headers().cookie?.includes(`agentic_api_key=${key}`) ?? false
    if (path === '/api/auth/session') {
      await route.fulfill({ status: cookieAuthenticated ? 200 : 401, json: identity })
    } else if (path === '/api/auth/login') {
      expect(request.postDataJSON().apiKey).toBe(key)
      await route.fulfill({
        json: { success: true, ...identity },
        headers: { 'set-cookie': `agentic_api_key=${key}; Path=/; HttpOnly; Secure; SameSite=Strict` },
      })
    } else if (path === '/api/auth/logout') {
      await route.fulfill({
        status: state.failLogout ? 500 : 200,
        json: { success: !state.failLogout },
        headers: state.failLogout ? {} : {
          'set-cookie': 'agentic_api_key=; Path=/; HttpOnly; Secure; SameSite=Strict; Expires=Thu, 01 Jan 1970 00:00:00 GMT',
        },
      })
    } else if (path === '/api/chat/configuration') {
      await route.fulfill({ json: {
        providers: [{ name: 'Ollama', models: ['review-model'], defaultModel: 'review-model' }],
        defaultProvider: 'Ollama', preferredProvider: 'Ollama', preferredModel: 'review-model',
        canManageTenant: true,
      } })
    } else if (path === '/api/agent/skills/all') {
      await route.fulfill({ json: [skill] })
    } else if (path === '/api/agent/skills/stored-preview') {
      await route.fulfill({ json: { ...skill, systemPrompt: payload } })
    } else {
      await route.fulfill({ json: [] })
    }
  })
  return { state, authHeaders }
}

async function login(page: Page) {
  await page.goto('/')
  await page.getByRole('button', { name: 'Chave de API', exact: true }).click()
  await page.getByPlaceholder('agentic_live_key_xxx...').fill(key)
  await page.getByRole('button', { name: 'Acessar Sistema' }).click()
  await expect(page.getByRole('heading', { name: 'Autenticação AgenticSystem' })).toBeHidden()
}

test('cookie login restores on reload without persisting or forwarding the API key', async ({ page, context }) => {
  const { authHeaders } = await mockApi(page)
  await login(page)
  const cookie = (await context.cookies()).find(item => item.name === 'agentic_api_key')
  expect(cookie?.httpOnly).toBe(true)
  expect(cookie?.secure).toBe(true)
  expect(await page.evaluate(() => localStorage.getItem('agentic_api_key'))).toBeNull()
  expect(await page.evaluate(() => document.cookie)).not.toContain(key)
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Autenticação AgenticSystem' })).toBeHidden()
  await expect(page.getByTitle('Sair do sistema')).toBeVisible()
  expect(authHeaders.length).toBeGreaterThan(1)
  for (const headers of authHeaders) {
    expect(headers['x-api-key']).toBeUndefined()
    expect(headers.authorization).toBeUndefined()
  }
})

test('logout deletes the cookie and returns to the login screen', async ({ page, context }) => {
  await mockApi(page)
  await login(page)
  const logout = page.waitForResponse(response => response.url().endsWith('/api/auth/logout'))
  await page.getByTitle('Sair do sistema').click()
  expect((await logout).ok()).toBe(true)
  await expect(page.getByRole('heading', { name: 'Autenticação AgenticSystem' })).toBeVisible()
  expect((await context.cookies()).find(item => item.name === 'agentic_api_key')).toBeUndefined()
})

test('failed logout retains authentication and shows an error instead of false success', async ({ page, context }) => {
  const { state } = await mockApi(page)
  await login(page)
  state.failLogout = true
  await page.getByTitle('Sair do sistema').click()
  await expect(page.getByRole('alert')).toHaveText('Não foi possível sair. Tente novamente.')
  await expect(page.getByRole('heading', { name: 'Autenticação AgenticSystem' })).toBeHidden()
  expect((await context.cookies()).find(item => item.name === 'agentic_api_key')).toBeDefined()
})

test('stored skill preview renders Markdown but never executes HTML or javascript links', async ({ page }) => {
  await mockApi(page)
  await login(page)
  await page.getByRole('link', { name: 'Skills', exact: true }).click()
  await page.getByTitle('Editar Habilidade').click()
  await expect(page.getByPlaceholder('Escreva as diretrizes Markdown de sistema que definem a habilidade...')).toHaveValue(payload)
  await expect(page.getByRole('heading', { name: 'Conteúdo seguro', exact: true })).toBeVisible()
  await expect(page.locator('img[src="x"]')).toHaveCount(0)
  expect(await page.evaluate(() => (window as Window & { __skillXss: boolean }).__skillXss)).toBe(false)
  expect(await page.getByRole('link', { name: 'Link perigoso' }).getAttribute('href')).not.toMatch(/^javascript:/i)
})
