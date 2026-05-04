/**
 * @tags @smoke @api @agents
 * Testes de API para gerenciamento de agentes
 */

describe('Agents API', () => {
  const baseUrl = Cypress.env('API_BASE_URL') || ''
  const testAgent = {
    name: 'CypressTestAgent',
    tier: 2,
    domain: 'testing',
    temperature: 0.7,
    capabilities: ['test-automation', 'quality-assurance']
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
        cy.validateAgentSchema(response.body[0])
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
      expect(response.body.tier).to.eq(testAgent.tier)
      cy.validateAgentSchema(response.body)
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
    const updatedAgent = { ...testAgent, temperature: 0.5 }
    cy.request({
      method: 'PUT',
      url: `${baseUrl}/api/agent/agents/${encodeURIComponent(testAgent.name)}`,
      body: updatedAgent
    }).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body.temperature).to.eq(0.5)
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
    cy.request({
      method: 'GET',
      url: `${baseUrl}/api/agent/agents/${encodeURIComponent(testAgent.name)}`,
      failOnStatusCode: false
    }).then((response) => {
      expect(response.status).to.eq(404)
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

Cypress.Commands.add('validateAgentSchema', (agent) => {
  expect(agent).to.have.property('name').that.is.a('string')
  expect(agent).to.have.property('tier').that.is.a('number')
  expect(agent).to.have.property('domain').that.is.a('string')
  expect(agent).to.have.property('temperature').that.is.a('number')
  expect(agent.temperature).to.be.within(0, 2)
  expect([0, 1, 2, 3]).to.include(agent.tier)
})
