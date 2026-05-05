/**
 * @tags @smoke @api @gateway
 * Testes de API para Gateway Dashboard
 * Contrato alinhado com GatewayController.cs
 */

describe('Gateway API', () => {
  const baseUrl = Cypress.env('API_BASE_URL') || ''

  it('@smoke deve retornar dashboard consolidado', () => {
    cy.request('GET', `${baseUrl}/api/admin/gateway/dashboard`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.have.property('health')
      expect(response.body).to.have.property('costs')
      expect(response.body).to.have.property('services').that.is.an('array')
      expect(response.body).to.have.property('metrics')
      expect(response.body).to.have.property('timestamp').that.is.a('string')
    })
  })

  it('@smoke deve listar serviços do gateway', () => {
    cy.request('GET', `${baseUrl}/api/admin/gateway/services`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.be.an('array')
      if (response.body.length > 0) {
        const service = response.body[0]
        expect(service).to.have.property('name').that.is.a('string')
        expect(service).to.have.property('isEnabled').that.is.a('boolean')
        expect(service).to.have.property('isHealthy').that.is.a('boolean')
        expect(service).to.have.property('successRate').that.is.a('number')
      }
    })
  })

  it('@regression deve retornar health report', () => {
    cy.request('GET', `${baseUrl}/api/admin/gateway/health`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.have.property('overallHealthy').that.is.a('boolean')
      expect(response.body).to.have.property('totalServices').that.is.a('number')
      expect(response.body).to.have.property('healthyServices').that.is.a('number')
      expect(response.body).to.have.property('unhealthyServices').that.is.a('number')
      expect(response.body).to.have.property('services').that.is.an('array')
      expect(response.body).to.have.property('timestamp').that.is.a('string')
    })
  })

  it('@regression deve retornar relatório de custos', () => {
    cy.request('GET', `${baseUrl}/api/admin/gateway/costs`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.have.property('totalCost').that.is.a('number')
      expect(response.body.totalCost).to.be.at.least(0)
    })
  })

  it('@regression deve retornar serviço específico por nome', () => {
    cy.request('GET', `${baseUrl}/api/admin/gateway/services`).then((listResponse) => {
      if (listResponse.body.length > 0) {
        const name = listResponse.body[0].name
        cy.request('GET', `${baseUrl}/api/admin/gateway/services/${name}`).then((response) => {
          expect(response.status).to.eq(200)
          expect(response.body).to.have.property('name', name)
        })
      }
    })
  })

  it('@error deve retornar 404 para serviço inexistente', () => {
    cy.request({
      method: 'GET',
      url: `${baseUrl}/api/admin/gateway/services/servico-inexistente`,
      failOnStatusCode: false
    }).then((response) => {
      expect(response.status).to.eq(404)
    })
  })
})
