/**
 * @tags @smoke @api @agents
 * Testes de API para gerenciamento de agentes
 * Contrato alinhado com AgentController.cs / AgentModels.cs
 */

describe('Agents API', () => {
  const baseUrl = Cypress.env('API_BASE_URL') || ''
  const testAgent = {
    name: 'CypressTestAgent',
    description: 'Agent criado por teste Cypress',
    tier: 2,
    domain: 'testing',
    allowedTools: ['test-automation', 'quality-assurance'],
    instructions: 'Agent de teste automatizado',
    configuration: {}
  }

  beforeEach(() => {
    cy.request({
      method: 'DELETE',
      url: `${baseUrl}/api/agent/agents/${encodeURIComponent(testAgent.name)}`,
      failOnStatusCode: false
    })
  })

  it('@smoke deve listar todos os agentes', () => {
    cy.request('GET', `${baseUrl}/api/agent/agents`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.be.an('array')
      if (response.body.length > 0) {
        validateAgentInfo(response.body[0])
      }
    })
  })

  it('@smoke deve criar novo agente', () => {
    cy.request({
      method: 'POST',
      url: `${baseUrl}/api/agent/agents`,
      body: testAgent
    }).then((response) => {
      expect(response.status).to.eq(201)
      expect(response.body.name).to.eq(testAgent.name)
      expect(response.body.description).to.eq(testAgent.description)
      expect(response.body.tier).to.eq(testAgent.tier)
      expect(response.body.isActive).to.be.a('boolean')
      validateAgentInfo(response.body)
    })
  })

  it('@regression deve filtrar agentes por tier', () => {
    cy.request('POST', `${baseUrl}/api/agent/agents`, testAgent)
    cy.request('GET', `${baseUrl}/api/agent/agents/tier/2`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.be.an('array')
      response.body.forEach(agent => {
        expect(agent.tier).to.eq(2)
      })
    })
  })

  it('@regression deve atualizar agente existente', () => {
    cy.request('POST', `${baseUrl}/api/agent/agents`, testAgent)
    const updatedAgent = { ...testAgent, description: 'Descrição atualizada' }
    cy.request({
      method: 'PUT',
      url: `${baseUrl}/api/agent/agents/${encodeURIComponent(testAgent.name)}`,
      body: updatedAgent
    }).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body.description).to.eq('Descrição atualizada')
      validateAgentInfo(response.body)
    })
  })

  it('@api deve excluir agente', () => {
    cy.request('POST', `${baseUrl}/api/agent/agents`, testAgent)
    cy.request({
      method: 'DELETE',
      url: `${baseUrl}/api/agent/agents/${encodeURIComponent(testAgent.name)}`
    }).then((response) => {
      expect(response.status).to.eq(204)
    })
  })

  it('@error deve retornar 400 para agente inválido', () => {
    const invalidAgent = { name: '', tier: 999 }
    cy.request({
      method: 'POST',
      url: `${baseUrl}/api/agent/agents`,
      body: invalidAgent,
      failOnStatusCode: false
    }).then((response) => {
      expect(response.status).to.eq(400)
    })
  })
})

/**
 * Valida shape de AgentInfo retornado pelo backend
 */
function validateAgentInfo(agent) {
  expect(agent).to.have.property('name').that.is.a('string')
  expect(agent).to.have.property('description').that.is.a('string')
  expect(agent).to.have.property('tier').that.is.a('number')
  expect(agent).to.have.property('domain').that.is.a('string')
  expect(agent).to.have.property('isActive').that.is.a('boolean')
  expect(agent).to.have.property('createdAt').that.is.a('string')
  expect([0, 1, 2, 3]).to.include(agent.tier)
}
