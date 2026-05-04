/**
 * @tags @smoke @api @plugins
 * Testes de API para MCP Plugins
 */

describe('Plugins API', () => {
  const baseUrl = Cypress.env('API_BASE_URL') || ''

  it('@smoke deve listar plugins carregados', () => {
    cy.request('GET', `${baseUrl}/api/admin/plugins`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.be.an('array')
    })
  })

  it('@regression deve listar tools de plugins', () => {
    cy.request('GET', `${baseUrl}/api/admin/plugins/tools`).then((response) => {
      expect(response.status).to.eq(200)
      expect(response.body).to.be.an('array')
    })
  })

  it('@error deve retornar 400 para plugin inválido', () => {
    const invalidPlugin = { type: 'invalid-type', name: '', command: '' }
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
