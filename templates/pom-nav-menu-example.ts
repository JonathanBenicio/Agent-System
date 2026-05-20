import { type Page, type Locator } from '@playwright/test'

/**
 * Exemplo de Component Object Model para um Menu de Navegação.
 * Pode ser instanciado sozinho ou dentro de outras Page Classes (composição).
 */
export class NavigationMenu {
  readonly page: Page
  readonly container: Locator

  // Seletores específicos do menu
  readonly homeLink: Locator
  readonly dashboardLink: Locator
  readonly profileLink: Locator
  readonly settingsLink: Locator
  readonly logoutButton: Locator

  constructor(page: Page) {
    this.page = page

    // 1. O contêiner principal do componente. Restringe o escopo de busca.
    this.container = page.locator('nav.main-navigation')

    // 2. Elementos internos encadeados a partir do contêiner.
    // Isso garante que se houver outro botão "Logout" na tela, o teste não vai falhar.
    this.homeLink = this.container.getByRole('link', { name: 'Home' })
    this.dashboardLink = this.container.getByRole('link', { name: 'Dashboard' })
    this.profileLink = this.container.getByRole('link', { name: 'Profile' })
    this.settingsLink = this.container.getByRole('link', { name: 'Settings' })
    this.logoutButton = this.container.getByRole('button', { name: 'Logout' })
  }

  // 3. Ações do Componente
  async goToDashboard() {
    await this.dashboardLink.click()
  }

  async goToProfile() {
    await this.profileLink.click()
  }

  async goToSettings() {
    await this.settingsLink.click()
  }

  async logout() {
    await this.logoutButton.click()
  }

  // 4. Verificação de estado do componente
  async isMenuVisible(): Promise<boolean> {
    return this.container.isVisible()
  }
}