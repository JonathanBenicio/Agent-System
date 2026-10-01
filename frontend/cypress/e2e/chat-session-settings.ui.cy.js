const userId = 'chat-123-owner'
const tenantId = 'admin'
let authHeaders
let validationToken

describe('Chat, seleção de modelo e retomada de sessão', () => {
  let existingSessionIds = new Set()
  let createdSessionId
  let keyId
  let keyName
  let skillId
  let skillName

  beforeEach(() => {
    createdSessionId = undefined
    keyId = undefined
    skillId = undefined
    cy.task('createValidationJwt', { userId, tenantId }).then(token => {
      validationToken = token
      authHeaders = { Authorization: `Bearer ${token}`, 'X-Tenant-Id': tenantId }
      keyName = `chat-123-ui-${Date.now()}`
      return cy.request({
        method: 'POST', url: '/api/admin/llm/providers/OpenAI/keys', headers: authHeaders,
        body: { name: keyName, apiKey: 'synthetic-chat-123-ui-secret', isDefault: false }
      }).then(response => {
        keyId = response.body.id
        return cy.request({ method: 'POST', url: `/api/admin/llm/providers/OpenAI/keys/${keyId}/test`, headers: authHeaders })
      }).then(response => {
        expect(response.body.success).to.equal(true)
        return cy.request({ method: 'POST', url: `/api/admin/llm/providers/OpenAI/keys/${keyId}/discover-models`, headers: authHeaders })
      }).then(response => {
        expect(response.body.discoveredModels).to.include('validation-model')
        return cy.request({ method: 'POST', url: `/api/admin/llm/providers/OpenAI/keys/${keyId}/default`, headers: authHeaders })
      }).then(() => {
        return cy.request({ method: 'PUT', url: '/api/chat/configuration', headers: authHeaders,
          body: { provider: 'OpenAI', model: 'gpt-4o-mini' } })
      }).then(() => {
        skillId = `chat-123-ui-skill-${Date.now()}`
        skillName = `Chat 123 UI Skill ${Date.now()}`
        return cy.request({ method: 'POST', url: '/api/agent/skills', headers: authHeaders,
          body: { id: skillId, name: skillName, domain: 'general', type: 'Instruction', systemPromptFragment: 'CHAT_123_UI_SKILL' } })
      }).then(() => {
        return cy.request({ method: 'GET', url: '/api/session?limit=100', headers: authHeaders })
      }).then(response => {
        existingSessionIds = new Set(response.body.map(item => item.id))
        cy.visit('/', {
          onBeforeLoad(window) {
            window.localStorage.setItem('agentic_auth_token', token)
            window.localStorage.setItem('agentic-knowledge-storage', JSON.stringify({
              state: { activeWorkspaceId: tenantId }, version: 0
            }))
          }
        })
      })
    })
  })

  afterEach(() => {
    if (createdSessionId) cy.request({ method: 'DELETE', url: `/api/session/${encodeURIComponent(createdSessionId)}`, headers: authHeaders, failOnStatusCode: false })
    cy.request({ method: 'PUT', url: '/api/chat/configuration', headers: authHeaders,
      body: { provider: 'OpenAI', model: 'gpt-4o-mini' }, failOnStatusCode: false })
    if (keyId) cy.request({ method: 'DELETE', url: `/api/admin/llm/providers/OpenAI/keys/${encodeURIComponent(keyId)}`, headers: authHeaders, failOnStatusCode: false })
    if (skillId) cy.request({ method: 'DELETE', url: `/api/agent/skills/${encodeURIComponent(skillId)}`, headers: authHeaders, failOnStatusCode: false })
  })

  it('salva provider/model pela UI, conversa e reabre o histórico no mesmo tenant', () => {
    cy.contains('IA CORE WORKSPACE', { timeout: 20000 }).should('be.visible')
    cy.intercept('PUT', '**/api/chat/configuration').as('saveChatSelection')
    cy.get('#chat-provider').select('OpenAI')
    cy.get('#chat-model').select('validation-model')
    cy.wait('@saveChatSelection')

    cy.request({ method: 'GET', url: '/api/chat/configuration', headers: authHeaders }).then(response => {
      expect(response.body.preferredProvider).to.equal('OpenAI')
      expect(response.body.preferredModel).to.equal('validation-model')
    })

    cy.contains('button', 'Parâmetros').click()
    cy.contains('label', 'SPECIALIST AGENT').parent().find('select').as('agentSelect')
    cy.get('@agentSelect').find('option').should('have.length.greaterThan', 1)
    cy.get('@agentSelect').select(1)
    cy.contains('button', 'Parâmetros').click()

    cy.get('textarea[placeholder="Envie uma mensagem..."]').type('chat-123-ui-primeiro-turno')
    cy.get('textarea[placeholder="Envie uma mensagem..."]').type('{enter}')
    cy.contains('Resposta validada pelo provider local: chat-123-ui-primeiro-turno', { timeout: 20000 }).should('be.visible')

    cy.request({ method: 'GET', url: '/api/session?limit=100', headers: authHeaders }).then(response => {
      const created = response.body.find(item => !existingSessionIds.has(item.id))
      expect(created, 'session created by the UI').to.exist
      createdSessionId = created.id
      cy.get('textarea[placeholder="Envie uma mensagem..."]').type('chat-123-ui-segundo-turno')
      cy.get('textarea[placeholder="Envie uma mensagem..."]').type('{enter}')
      cy.contains('Resposta validada pelo provider local: chat-123-ui-segundo-turno', { timeout: 20000 }).should('be.visible')
      cy.reload()
      cy.contains('IA CORE WORKSPACE', { timeout: 20000 }).should('be.visible')
      cy.request({ method: 'GET', url: '/api/session?limit=100', headers: authHeaders }).then(list => {
        expect(list.body.some(item => item.id === createdSessionId), 'resumable session is listed').to.equal(true)
      })
      cy.get(`[data-session-id="${createdSessionId}"]`, { timeout: 20000 }).click()
      cy.contains('chat-123-ui-primeiro-turno', { timeout: 10000 }).should('be.visible')
      cy.contains('chat-123-ui-segundo-turno').should('be.visible')
      cy.get('#chat-provider').should('have.value', 'OpenAI')
      cy.get('#chat-model').should('have.value', 'validation-model')
      cy.visit('/ai')
      cy.contains('Sua preferência', { timeout: 20000 }).should('be.visible')
      cy.contains(keyName).should('be.visible')
      cy.get('body').should('not.contain', 'synthetic-chat-123-ui-secret')
      cy.visit('/skills')
      cy.get(`[data-skill-id="${skillId}"]`, { timeout: 20000 }).contains('button', 'Desativar').click()
      cy.get(`[data-skill-id="${skillId}"]`).contains('Desativada').should('be.visible')
    })
  })
})
