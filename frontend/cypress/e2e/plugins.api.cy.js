/**
 * @tags @smoke @api @plugins
 * Testes de API para MCP Plugins
 * Contrato alinhado com MCPPluginController.cs
 */

describe('Plugins API', () => {
  const baseUrl = Cypress.env('API_BASE_URL') || ''

  it('@smoke deve listar plugins carregados', () => {
    cy.request('GET', `${baseUrl}/api/admin/plugins`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.be.an('array')
      if (response.body.length > 0) {
        const plugin = response.body[0]
        expect(plugin).to.have.property('id').that.is.a('string')
        expect(plugin).to.have.property('name').that.is.a('string')
        expect(plugin).to.have.property('isEnabled').that.is.a('boolean')
        expect(plugin).to.have.property('toolCount').that.is.a('number')
        expect(plugin).to.have.property('status').that.is.a('string')
      }
    })
  })

  it('@regression deve listar tools de plugins', () => {
    cy.request('GET', `${baseUrl}/api/admin/plugins/tools`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.be.an('array')
    })
  })

  it('@regression deve retornar 404 para plugin inexistente', () => {
    cy.request({
      method: 'GET',
      url: `${baseUrl}/api/admin/plugins/plugin-inexistente`,
      failOnStatusCode: false
    }).then((response) => {
      expect(response.status).to.eq(404)
    })
  })

  it('@error deve retornar 400 para plugin sem command nem endpoint', () => {
    const invalidPlugin = { name: 'invalid', command: '', endpoint: '' }
    cy.request({
      method: 'POST',
      url: `${baseUrl}/api/admin/plugins/load`,
      body: invalidPlugin,
      failOnStatusCode: false
    }).then((response) => {
      expect(response.status).to.eq(400)
    })
  })
})
