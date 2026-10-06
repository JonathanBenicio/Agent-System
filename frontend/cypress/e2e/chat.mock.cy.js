describe('Chat UI com API mockada', () => {
  beforeEach(() => {
    cy.intercept('**/hubs/**', { forceNetworkError: true })
    cy.intercept('**/api/**', request => {
      const path = new URL(request.url).pathname
      if (!path.startsWith('/api/')) { request.continue(); return }
      let body = []
      if (path === '/api/auth/session') body = { userId: 'cypress-user', tenantId: 'cypress-tenant', roles: ['Owner'] }
      if (path === '/api/chat/configuration') body = {
        providers: [{ name: 'Ollama', models: ['mock-model'], defaultModel: 'mock-model' }],
        defaultProvider: 'Ollama', preferredProvider: 'Ollama', preferredModel: 'mock-model', canManageTenant: true,
      }
      if (path === '/api/chat') body = { success: true, content: 'Resposta Cypress visível', agentName: 'ChiefAgent', agentTier: 0, sessionId: 'mock-session' }
      request.reply({ statusCode: 200, body })
    })
    cy.visit('/')
  })
  it('renderiza resposta REST com os nomes atuais do contrato', () => {
    cy.get('textarea[placeholder="Envie uma mensagem..."]').type('Olá Cypress{enter}')
    cy.contains('.chat-markdown', 'Resposta Cypress visível').should('be.visible')
    cy.contains('ChiefAgent').should('be.visible')
  })
})
