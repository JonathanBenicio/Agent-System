/**
 * @tags @smoke @api @gateway
 * Testes de API para Gateway Dashboard
 */

describe('Gateway API', () => {
  const baseUrl = Cypress.env('API_BASE_URL') || ''

  it('@smoke deve retornar dashboard com métricas', () => {
    cy.request('GET', `${baseUrl}/api/admin/gateway/dashboard`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.have.property('totalAgents').that.is.a('number')
      expect(response.body).to.have.property('totalTools').that.is.a('number')
      expect(response.body).to.have.property('totalPlugins').that.is.a('number')
      expect(response.body).to.have.property('activeServices').that.is.a('number')
      expect(response.body.totalAgents).to.be.at.least(0)
    })
  })

  it('@smoke deve listar serviços do gateway', () => {
    cy.request('GET', `${baseUrl}/api/admin/gateway/services`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.be.an('array')
      if (response.body.length > 0) {
        const service = response.body[0]
        expect(service).to.have.property('name').that.is.a('string')
        expect(service).to.have.property('enabled').that.is.a('boolean')
      }
    })
  })

  it('@regression deve retornar health report', () => {
    cy.request('GET', `${baseUrl}/api/admin/gateway/health`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.have.property('status').that.is.a('string')
      expect(['Healthy', 'Degraded', 'Unhealthy']).to.include(response.body.status)
    })
  })

  it('@regression deve retornar relatório de custos', () => {
    cy.request('GET', `${baseUrl}/api/admin/gateway/costs`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.have.property('totalCost').that.is.a('number')
      expect(response.body.totalCost).to.be.at.least(0)
    })
  })

  it('@error deve retornar 404 para serviço inexistente', () => {
    cy.request({
      method: 'POST',
      url: `${baseUrl}/api/admin/gateway/services/servico-inexistente/enable`,
      failOnStatusCode: false
    }).then((response) => {
      expect(response.status).to.eq(404)
    })
  })
})
