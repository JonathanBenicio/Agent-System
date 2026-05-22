import { Page, Locator, expect } from '@playwright/test';

export class AgentsPage {
  readonly page: Page;
  readonly searchInput: Locator;
  readonly newAgentButton: Locator;
  readonly agentCards: Locator;

  constructor(page: Page) {
    this.page = page;
    this.searchInput = page.locator('input[placeholder="Buscar agents..."]');
    this.newAgentButton = page.locator('button:has-text("Novo Agent")');
    this.agentCards = page.locator('.bg-zinc-900.border.border-zinc-800.rounded-xl');
  }

  /**
   * Navega para a página de listagem de agentes.
   */
  async goto() {
    await this.page.goto('/agents');
  }

  /**
   * Filtra agentes por texto.
   */
  async searchAgent(name: string) {
    await this.searchInput.waitFor({ state: 'visible' });
    await this.searchInput.fill(name);
  }

  /**
   * Filtra agentes por Tier clicando nos botões de filtro.
   */
  async filterByTier(tierLabel: string) {
    const filterBtn = this.page.locator(`button:has-text("${tierLabel}")`);
    await filterBtn.click();
  }

  /**
   * Inicia um chat dedicado com o agente correspondente pelo card.
   */
  async startChatWith(agentName: string) {
    // Localiza o card do agente pelo título
    const agentCard = this.agentCards.filter({
      has: this.page.locator(`h3:has-text("${agentName}")`),
    });
    await agentCard.waitFor({ state: 'visible' });
    
    // Clica no botão de chat direto
    const chatBtn = agentCard.locator('button[title="Chat direto"]');
    await chatBtn.click();
  }

  /**
   * Cria um novo agente via modal de formulário
   */
  async createAgent(spec: { name: string; description: string; domain: string; tier: number }) {
    await this.newAgentButton.click();
    
    // Preenche os campos do formulário no modal
    await this.page.locator('input[id="name"]').fill(spec.name);
    await this.page.locator('textarea[id="description"]').fill(spec.description);
    await this.page.locator('input[id="domain"]').fill(spec.domain);
    await this.page.locator('select[id="tier"]').selectOption(spec.tier.toString());
    
    // Clica em Salvar
    await this.page.locator('button:has-text("Salvar")').click();
  }
}
